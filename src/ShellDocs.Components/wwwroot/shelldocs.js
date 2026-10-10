window.ShellDocs = window.ShellDocs || {};

/* Blazor raises enhancedload through Blazor.addEventListener, not as a DOM event.
   blazor.web.js loads after this file, so attach once it has run. */
window.shelldocsOnEnhancedLoad = (function () {
    var pending = [];
    var attached = false;
    function attach() {
        if (attached || !window.Blazor || typeof window.Blazor.addEventListener !== 'function') return;
        attached = true;
        window.Blazor.addEventListener('enhancedload', function () {
            pending.forEach(function (fn) { fn(); });
        });
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', attach);
    }
    window.addEventListener('load', attach);
    return function (fn) {
        pending.push(fn);
        attach();
    };
})();

// Cmd/Ctrl+K opens <SearchDialog> via the DotNetObjectReference it registers on first render.
window.shelldocsSearch = (function () {
    var dotnet = null;
    function isModK(e) {
        return (e.key === 'k' || e.key === 'K') && (e.metaKey || e.ctrlKey);
    }
    function onKeydown(e) {
        if (!isModK(e)) return;
        e.preventDefault();
        openInternal();
    }
    function openInternal() {
        if (dotnet) { dotnet.invokeMethodAsync('OpenFromJs'); }
    }
    return {
        init: function (dotnetRef) {
            dotnet = dotnetRef;
            document.addEventListener('keydown', onKeydown);
            return {
                dispose: function () {
                    document.removeEventListener('keydown', onKeydown);
                    dotnet = null;
                }
            };
        },
        open: openInternal
    };
})();

/* Theme. <html class="dark"> is the source of truth: whoever flips it (a
   ThemeToggle, or a component library's own toggle) gets saved under
   'shelldocs-theme' and pushed to every subscribed ThemeToggle's ThemeState.
   Enhanced navigation strips the class the head script set and the saved theme
   is re-applied straight after, so the observer sees no net change. */
window.shelldocsTheme = (function () {
    var KEY = 'shelldocs-theme';
    var root = document.documentElement;
    var subscribers = [];

    function isDark() { return root.classList.contains('dark'); }

    function apply() {
        var saved = null;
        try { saved = localStorage.getItem(KEY); } catch (e) {}
        var systemDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        root.classList.toggle('dark', (saved || (systemDark ? 'dark' : 'light')) === 'dark');
    }

    function set(dark) { root.classList.toggle('dark', !!dark); }

    apply();
    var lastDark = isDark();

    function onChange() {
        var dark = isDark();
        if (dark === lastDark) return;
        lastDark = dark;
        try { localStorage.setItem(KEY, dark ? 'dark' : 'light'); } catch (e) {}
        subscribers.slice().forEach(function (ref) {
            ref.invokeMethodAsync('ThemeChanged', dark).catch(function () {});
        });
        if (window.shelldocsIframes) window.shelldocsIframes.postTheme(dark);
    }

    if (window.MutationObserver) {
        new MutationObserver(onChange).observe(root, { attributes: true, attributeFilter: ['class'] });
    }

    // Delegated, so the toggle works on static hosts too.
    document.addEventListener('click', function (e) {
        var btn = e.target.closest && e.target.closest('[data-theme-toggle]');
        if (btn) set(!isDark());
    });

    window.shelldocsOnEnhancedLoad(apply);

    return {
        isDark: isDark,
        set: set,
        apply: apply,
        // ThemeToggle passes a DotNetObjectReference; the handle unsubscribes it.
        subscribe: function (dotnetRef) {
            subscribers.push(dotnetRef);
            return {
                dispose: function () {
                    var i = subscribers.indexOf(dotnetRef);
                    if (i >= 0) subscribers.splice(i, 1);
                }
            };
        }
    };
})();
window.shelldocsApplyTheme = window.shelldocsTheme.apply;

/* <IframePreview>: shelldocs.js sets each [data-iframe-preview] iframe's src, with
   ?theme=light|dark, when it scrolls into view (data-lazy="visible"), on the Run
   button ("click") or at once ("none"), and posts theme changes to loaded frames. */
window.shelldocsIframes = (function () {
    var observer = null;

    function theme() { return document.documentElement.classList.contains('dark') ? 'dark' : 'light'; }

    function load(host) {
        if (!host || host.getAttribute('data-loaded') === 'true') return;
        var frame = host.querySelector('iframe');
        var src = host.getAttribute('data-src');
        if (!frame || !src) return;
        var url = new URL(src, document.baseURI);
        url.searchParams.set('theme', theme());
        frame.src = url.href;
        host.setAttribute('data-loaded', 'true');
    }

    function watch(host) {
        if (!window.IntersectionObserver) { load(host); return; }
        observer = observer || new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                observer.unobserve(entry.target);
                load(entry.target);
            });
        }, { rootMargin: '200px 0px' });
        observer.observe(host);
    }

    function init(scope) {
        if (!scope || !scope.querySelectorAll) return;
        var hosts = Array.prototype.slice.call(scope.querySelectorAll('[data-iframe-preview]'));
        if (scope.matches && scope.matches('[data-iframe-preview]')) hosts.unshift(scope);
        hosts.forEach(function (host) {
            if (host.__shelldocsIframe) return;
            host.__shelldocsIframe = true;
            var lazy = host.getAttribute('data-lazy');
            if (lazy === 'none') load(host);
            else if (lazy !== 'click') watch(host);
        });
    }

    function postTheme(dark) {
        var message = { type: 'shelldocs-theme', theme: dark ? 'dark' : 'light' };
        document.querySelectorAll('[data-iframe-preview][data-loaded="true"] iframe').forEach(function (frame) {
            try { frame.contentWindow.postMessage(message, new URL(frame.src).origin); } catch (e) {}
        });
    }

    document.addEventListener('click', function (e) {
        var run = e.target.closest && e.target.closest('[data-iframe-run]');
        if (run) load(run.closest('[data-iframe-preview]'));
    });

    return { init: init, load: load, postTheme: postTheme };
})();

/* Shiki highlighting. The source <pre> belongs to Blazor (or to a markup block
   Blazor tracks), so it is never replaced or edited: it gets [data-shiki="source"]
   (hidden by CSS) and Shiki's output goes into a JS-owned sibling
   [data-shiki-output]. Replacing the source used to leave Blazor updating a
   detached node — stale code after navigation — and could throw on replaceChild.
   Each pass re-renders an output whose source text changed and drops orphans. */

// Fence languages Shiki knows under another name; XAML is highlighted as XML.
var LANG_ALIASES = { xaml: 'xml', axaml: 'xml', cs: 'csharp', 'c#': 'csharp' };

function langOf(codeEl) {
    var cls = (codeEl.className || '').split(/\s+/);
    for (var i = 0; i < cls.length; i++) {
        if (cls[i].indexOf('language-') === 0) return cls[i].substring(9);
    }
    return null;
}

function shikiOutputOf(preEl) {
    var next = preEl.nextElementSibling;
    return next && next.hasAttribute('data-shiki-output') ? next : null;
}

function highlightOne(preEl) {
    if (!preEl || !window.__shiki || !preEl.parentNode || preEl.hasAttribute('data-shiki-output')) return;
    var code = preEl.querySelector('code');
    if (!code) return;
    var declared = langOf(code);
    var lang = LANG_ALIASES[declared] || declared;
    if (!lang) return;
    /* Languages Shiki hasn't loaded stay as plain text. */
    if (!window.__shiki.getLoadedLanguages().includes(lang)) return;

    var source = code.textContent;
    var existing = shikiOutputOf(preEl);
    if (existing && existing.__shikiSource === source) return;

    try {
        var html = window.__shiki.codeToHtml(source, {
            lang: lang,
            themes: { light: 'github-light', dark: 'github-dark' },
            defaultColor: false
        });
        var tpl = document.createElement('template');
        tpl.innerHTML = html.trim();
        var output = tpl.content.firstElementChild;
        if (!output) return;
        output.setAttribute('data-shiki-output', '');
        output.__shikiSource = source;
        if (existing) existing.parentNode.insertBefore(output, existing.nextSibling);
        else preEl.parentNode.insertBefore(output, preEl.nextSibling);
        if (existing) existing.remove();
        preEl.setAttribute('data-shiki', 'source');
    } catch (e) { /* skip on grammar error */ }
}

function removeOrphanedShikiOutputs() {
    document.querySelectorAll('[data-shiki-output]').forEach(function (output) {
        var prev = output.previousElementSibling;
        if (!prev || prev.getAttribute('data-shiki') !== 'source') output.remove();
    });
}

window.shelldocsHighlight = function () {
    if (!window.__shiki) return;
    removeOrphanedShikiOutputs();
    document.querySelectorAll('pre:not([data-shiki-output]) > code[class*="language-"]')
        .forEach(function (code) { highlightOne(code.parentElement); });
};

window.shelldocsHighlightElement = function (preEl) { highlightOne(preEl); };

/* Blazor updates the source text in place (e.g. navigating between pages that
   reuse the same component) and adds new code on circuit swaps — re-run the
   pass, batched per frame. Our own output insertions don't match the trigger. */
(function () {
    if (!window.MutationObserver) return;
    var scheduled = false;
    function schedule() {
        if (scheduled) return;
        scheduled = true;
        requestAnimationFrame(function () { scheduled = false; window.shelldocsHighlight(); });
    }
    function touchesSource(node) {
        var el = node.nodeType === 1 ? node : node.parentElement;
        if (!el) return false;
        if (el.closest && el.closest('pre[data-shiki="source"]')) return true;
        return !!(el.matches && (el.matches('code[class*="language-"]') || el.querySelector('code[class*="language-"]')));
    }
    new MutationObserver(function (records) {
        for (var r = 0; r < records.length; r++) {
            var rec = records[r];
            if (rec.type === 'characterData') {
                if (touchesSource(rec.target)) return schedule();
                continue;
            }
            for (var n = 0; n < rec.addedNodes.length; n++) {
                if (touchesSource(rec.addedNodes[n])) return schedule();
            }
            if (rec.removedNodes.length) {
                for (var m = 0; m < rec.removedNodes.length; m++) {
                    var removed = rec.removedNodes[m];
                    if (removed.nodeType === 1 && removed.getAttribute && removed.getAttribute('data-shiki') === 'source') return schedule();
                }
            }
        }
    }).observe(document.documentElement, { childList: true, characterData: true, subtree: true });
})();

window.shelldocsCopyCode = function (button) {
    var block = button.closest('.shelldocs-codeblock');
    if (!block) return;
    var code = block.querySelector('pre code');
    if (!code) return;
    var text = code.innerText;
    var writeText = navigator.clipboard && navigator.clipboard.writeText
        ? navigator.clipboard.writeText(text)
        : Promise.reject(new Error('clipboard unavailable'));

    writeText.then(function () {
        button.classList.add('copied');
        setTimeout(function () { button.classList.remove('copied'); }, 1400);
    }).catch(function () {
        var range = document.createRange();
        range.selectNodeContents(code);
        var sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
    });
};

window.shelldocsToc = {
    attach: function (listEl, ids) {
        if (!listEl || !ids || ids.length === 0) return null;
        var bar = listEl.querySelector('.toc-bar');

        // Walk up for the first scrollable ancestor — the TOC slot — so we can
        // scroll ONLY it (not the window) to keep the active link visible.
        var scrollHost = (function () {
            var p = listEl.parentElement;
            while (p) {
                var oy = getComputedStyle(p).overflowY;
                if (oy === 'auto' || oy === 'scroll') return p;
                p = p.parentElement;
            }
            return null;
        })();

        function keepVisible(link) {
            if (!scrollHost || !link) return;
            var lr = link.getBoundingClientRect();
            var cr = scrollHost.getBoundingClientRect();
            var pad = 24;
            if (lr.top < cr.top + pad) {
                scrollHost.scrollBy({ top: lr.top - cr.top - pad, behavior: 'smooth' });
            } else if (lr.bottom > cr.bottom - pad) {
                scrollHost.scrollBy({ top: lr.bottom - cr.bottom + pad, behavior: 'smooth' });
            }
        }

        var visible = new Set();
        var currentActive = null;

        function update() {
            // Pick the topmost visible heading. If none visible (between sections),
            // pick the last heading whose top is above the viewport top.
            var pick = null;
            if (visible.size > 0) {
                var top = Infinity;
                visible.forEach(function (id) {
                    var el = document.getElementById(id);
                    if (!el) return;
                    var t = el.getBoundingClientRect().top;
                    if (t < top) { top = t; pick = id; }
                });
            } else {
                var scrollY = window.pageYOffset || 0;
                for (var i = 0; i < ids.length; i++) {
                    var el = document.getElementById(ids[i]);
                    if (!el) continue;
                    var t = el.getBoundingClientRect().top + scrollY;
                    if (t <= scrollY + 120) pick = ids[i];
                    else break;
                }
            }
            if (!pick) pick = ids[0];
            if (pick === currentActive) return;

            if (currentActive) {
                var prev = listEl.querySelector('a[data-toc-id="' + currentActive + '"]');
                if (prev) prev.classList.remove('active');
            }
            var next = listEl.querySelector('a[data-toc-id="' + pick + '"]');
            if (next) {
                next.classList.add('active');
                if (bar) {
                    var li = next.parentElement;
                    bar.style.transform = 'translateY(' + li.offsetTop + 'px)';
                    bar.style.height = li.offsetHeight + 'px';
                }
                keepVisible(next);
            }
            currentActive = pick;
        }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (e) {
                if (e.isIntersecting) visible.add(e.target.id);
                else visible.delete(e.target.id);
            });
            update();
        }, { rootMargin: '-80px 0px -70% 0px', threshold: 0 });

        ids.forEach(function (id) {
            var el = document.getElementById(id);
            if (el) observer.observe(el);
        });

        // Initial position — no scroll event yet, so we compute manually.
        setTimeout(update, 0);

        return {
            dispose: function () { observer.disconnect(); }
        };
    },

    scrollTo: function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        if (window.history && window.history.replaceState) {
            window.history.replaceState(null, '', '#' + id);
        }
    }
};

/* Chrome interactivity that must work on static hosts, where no Blazor
   runtime exists to run @onclick. Delegated document-level listeners survive
   enhanced-nav DOM swaps; state lives in data-* attributes / CSS classes
   that the server already renders with the correct initial values. */
window.shelldocsChrome = (function () {
    function onSidebarClick(e) {
        var btn = e.target.closest('.sidebar-section-toggle');
        if (!btn) return;
        var section = btn.closest('.sidebar-section');
        if (!section) return;
        var open = section.getAttribute('data-open') === 'true';
        var next = open ? 'false' : 'true';
        section.setAttribute('data-open', next);
        btn.setAttribute('aria-expanded', next);
        var shell = section.querySelector(':scope > .sidebar-section-shell');
        if (shell) shell.setAttribute('data-open', next);
    }

    // Dropdowns — .pkg (PackageSelector), .ver (VersionSelector), .preview-menu
    // (preview ⋯ menu): the trigger toggles [data-open]; outside click, Escape,
    // or picking an option closes it.
    var DROPDOWN = '.pkg, .ver, .preview-menu';
    var OPEN_DROPDOWN = '.pkg[data-open="true"], .ver[data-open="true"], .preview-menu[data-open="true"]';
    var TRIGGER = '.pkg-trigger, .ver-trigger, .preview-menu-trigger';
    var MENU = '.pkg-menu, .ver-menu, .preview-menu-list';
    var OPTION = '.pkg-option, .ver-option, .preview-menu-item';
    var CHEVRON = '.pkg-chevron, .ver-chevron';

    function onDropdownClick(e) {
        var trigger = e.target.closest(TRIGGER);
        if (trigger) {
            var root = trigger.closest(DROPDOWN);
            if (!root) return;
            var isOpen = root.getAttribute('data-open') === 'true';
            closeAllDropdowns(root);
            setDropdown(root, !isOpen);
            return;
        }

        var option = e.target.closest(OPTION);
        if (option) {
            var owner = option.closest(DROPDOWN);
            if (owner) setDropdown(owner, false);
            return;
        }

        if (!e.target.closest(MENU)) closeAllDropdowns(null);
    }

    function onDropdownKeydown(e) {
        if (e.key !== 'Escape') return;
        var open = document.querySelector(OPEN_DROPDOWN);
        if (!open) return;
        closeAllDropdowns(null);
        var trigger = open.querySelector(TRIGGER);
        if (trigger) trigger.focus();
    }

    function setDropdown(root, open) {
        root.setAttribute('data-open', open ? 'true' : 'false');
        var trigger = root.querySelector(TRIGGER);
        if (trigger) trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
        var chevron = root.querySelector(CHEVRON);
        if (chevron) chevron.classList.toggle('open', open);
    }

    function closeAllDropdowns(except) {
        document.querySelectorAll(OPEN_DROPDOWN).forEach(function (d) {
            if (d !== except) setDropdown(d, false);
        });
    }

    /* Preview frames: Preview | Code tabs + copy. The chosen tab is remembered per
       page + frame id, because Blazor replaces the prerendered DOM when an
       interactive circuit starts — without this, a tab picked before then resets. */
    var previewTabs = {};

    function previewKey(frame) {
        return frame.id ? location.pathname + '#' + frame.id : null;
    }

    function selectPreviewTab(frame, value, focus) {
        frame.setAttribute('data-preview-tab', value);
        frame.querySelectorAll('[data-preview-tab-target]').forEach(function (tab) {
            var selected = tab.getAttribute('data-preview-tab-target') === value;
            tab.setAttribute('aria-selected', selected ? 'true' : 'false');
            tab.setAttribute('tabindex', selected ? '0' : '-1');
            if (selected && focus) tab.focus();
        });
        var key = previewKey(frame);
        if (key) previewTabs[key] = value;
    }

    function onPreviewClick(e) {
        var tab = e.target.closest('[data-preview-tab-target]');
        if (tab) {
            var tabFrame = tab.closest('.preview-frame');
            if (tabFrame) selectPreviewTab(tabFrame, tab.getAttribute('data-preview-tab-target'), false);
            return;
        }

        var copy = e.target.closest('[data-preview-copy]');
        if (copy) {
            var frame = copy.closest('.preview-frame');
            var panel = frame && frame.querySelector('[data-preview-panel="code"]');
            // With code tabs, the visible one (none while the "not available" note shows).
            var source = panel && panel.querySelector('[data-tabs-missing]')
                ? panel.querySelector('[data-tab-panel]:not([hidden])')
                : panel;
            var code = source && source.querySelector('code');
            if (!code) return;
            // textContent: the code panel may be display:none, where innerText loses layout.
            var text = code.textContent;
            var writeText = navigator.clipboard && navigator.clipboard.writeText
                ? navigator.clipboard.writeText(text)
                : Promise.reject(new Error('clipboard unavailable'));
            writeText.then(function () {
                copy.classList.add('copied');
                setTimeout(function () { copy.classList.remove('copied'); }, 1400);
            }).catch(function () { /* silent */ });
        }
    }

    // WAI-ARIA tabs: arrows / Home / End move between Preview and Code.
    function onPreviewKeydown(e) {
        var tab = e.target.closest && e.target.closest('.preview-tabs [role="tab"]');
        if (!tab) return;
        var tabs = Array.prototype.slice.call(tab.parentElement.querySelectorAll('[role="tab"]'));
        var i = tabs.indexOf(tab);
        var next = e.key === 'ArrowRight' ? tabs[(i + 1) % tabs.length]
            : e.key === 'ArrowLeft' ? tabs[(i - 1 + tabs.length) % tabs.length]
            : e.key === 'Home' ? tabs[0]
            : e.key === 'End' ? tabs[tabs.length - 1]
            : null;
        if (!next) return;
        e.preventDefault();
        selectPreviewTab(tab.closest('.preview-frame'), next.getAttribute('data-preview-tab-target'), true);
    }

    /* <Tabs> and <CodeGroup>: [data-tabs] roots, [data-tab-target] buttons,
       [data-tab-panel] panels. Groups sharing [data-tabs-sync] switch together and
       remember the choice in localStorage; others remember it per page + id so it
       survives the circuit's DOM swap. Nested groups are kept apart via closest(). */
    var TAB_STORE = 'shelldocs-tabs:';
    var tabMemory = {};

    function ownTabParts(root, selector) {
        return Array.prototype.filter.call(root.querySelectorAll(selector), function (el) {
            return el.closest('[data-tabs]') === root;
        });
    }

    /* A group without the value is left alone, except preview code tabs
       ([data-tabs-missing]): they show "Not available on {value} yet" instead. */
    function selectTabsValue(root, value, focus) {
        var buttons = ownTabParts(root, '[data-tab-target]');
        var has = buttons.some(function (b) { return b.getAttribute('data-tab-target') === value; });
        if (!has && !root.hasAttribute('data-tabs-missing')) return false;
        root.setAttribute('data-tabs-value', value);
        buttons.forEach(function (b, i) {
            var selected = b.getAttribute('data-tab-target') === value;
            b.setAttribute('aria-selected', selected ? 'true' : 'false');
            b.setAttribute('tabindex', selected || (!has && i === 0) ? '0' : '-1');
            if (selected && focus) b.focus();
        });
        ownTabParts(root, '[data-tab-panel]').forEach(function (panel) {
            panel.hidden = panel.getAttribute('data-tab-panel') !== value;
        });
        ownTabParts(root, '[data-tab-missing]').forEach(function (note) {
            note.hidden = has;
            var label = note.querySelector('[data-tab-missing-label]');
            if (label && !has) label.textContent = value;
        });
        return true;
    }

    // The page switch (<SyncSwitch>) for a key, if the page has one.
    function switchFor(sync) {
        return Array.prototype.find.call(document.querySelectorAll('[data-tabs-switch]'), function (sw) {
            return sw.getAttribute('data-tabs-sync') === sync;
        }) || null;
    }

    function chooseTab(root, value, focus) {
        if (!selectTabsValue(root, value, focus)) return;
        var sync = root.getAttribute('data-tabs-sync');
        if (sync) {
            try { localStorage.setItem(TAB_STORE + sync, value); } catch (e) {}
            document.querySelectorAll('[data-tabs]').forEach(function (other) {
                if (other !== root && other.getAttribute('data-tabs-sync') === sync) selectTabsValue(other, value, false);
            });
        } else if (root.id) {
            tabMemory[location.pathname + '#' + root.id] = value;
        }
    }

    function restoreTabs(scope) {
        if (!scope.querySelectorAll) return;
        var roots = Array.prototype.slice.call(scope.querySelectorAll('[data-tabs]'));
        if (scope.matches && scope.matches('[data-tabs]')) roots.unshift(scope);
        roots.forEach(function (root) {
            var sync = root.getAttribute('data-tabs-sync');
            var saved = null;
            if (sync) {
                try { saved = localStorage.getItem(TAB_STORE + sync); } catch (e) {}
                // Nothing chosen yet: follow the page switch's default, so blocks agree with it.
                var sw = saved ? null : switchFor(sync);
                if (sw && sw !== root) saved = sw.getAttribute('data-tabs-value');
            }
            else if (root.id) saved = tabMemory[location.pathname + '#' + root.id];
            if (saved && root.getAttribute('data-tabs-value') !== saved) selectTabsValue(root, saved, false);
        });
    }

    function onTabsClick(e) {
        var btn = e.target.closest('[data-tab-target]');
        if (!btn) return;
        var root = btn.closest('[data-tabs]');
        if (root) chooseTab(root, btn.getAttribute('data-tab-target'), false);
    }

    function onTabsKeydown(e) {
        var btn = e.target.closest && e.target.closest('[data-tab-target]');
        if (!btn) return;
        var root = btn.closest('[data-tabs]');
        var buttons = ownTabParts(root, '[data-tab-target]');
        var i = buttons.indexOf(btn);
        var next = e.key === 'ArrowRight' ? buttons[(i + 1) % buttons.length]
            : e.key === 'ArrowLeft' ? buttons[(i - 1 + buttons.length) % buttons.length]
            : e.key === 'Home' ? buttons[0]
            : e.key === 'End' ? buttons[buttons.length - 1]
            : null;
        if (!next) return;
        e.preventDefault();
        chooseTab(root, next.getAttribute('data-tab-target'), true);
    }

    function restorePreviewTabs(root) {
        var frames = root.matches && root.matches('.preview-frame[id]')
            ? [root]
            : (root.querySelectorAll ? root.querySelectorAll('.preview-frame[id]') : []);
        Array.prototype.forEach.call(frames, function (frame) {
            var saved = previewTabs[previewKey(frame)];
            if (saved && frame.getAttribute('data-preview-tab') !== saved) selectPreviewTab(frame, saved, false);
        });
    }

    function watchForReplacedFrames() {
        if (!window.MutationObserver) return;
        new MutationObserver(function (records) {
            for (var r = 0; r < records.length; r++) {
                var added = records[r].addedNodes;
                for (var n = 0; n < added.length; n++) {
                    if (added[n].nodeType !== 1) continue;
                    restorePreviewTabs(added[n]);
                    restoreTabs(added[n]);
                    window.shelldocsIframes.init(added[n]);
                    restoreMobileNav(added[n]);
                    restoreCollapse(added[n]);
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }

    /* Mobile nav: hamburgers toggle [data-mobile-open] on the page shell. CSS slides
       in the docs sidebar or shows the home menu; the backdrop, Escape, picking a
       link, or an enhanced navigation closes it. */
    /* Desktop sidebar collapse: [data-sidebar-collapse-toggle] buttons flip
       [data-sidebar-collapsed] on the page shell, and the choice is remembered. */
    var COLLAPSE_KEY = 'shelldocs-sidebar';

    function onCollapseClick(e) {
        var btn = e.target.closest && e.target.closest('[data-sidebar-collapse-toggle]');
        var shell = btn && btn.closest('.docs-shell');
        if (!shell) return;
        var collapsed = shell.getAttribute('data-sidebar-collapsed') !== 'true';
        shell.setAttribute('data-sidebar-collapsed', collapsed ? 'true' : 'false');
        try { localStorage.setItem(COLLAPSE_KEY, collapsed ? 'collapsed' : 'expanded'); } catch (e) {}
    }

    function restoreCollapse(root) {
        var saved = null;
        try { saved = localStorage.getItem(COLLAPSE_KEY); } catch (e) {}
        if (saved !== 'collapsed' || !root.querySelectorAll) return;
        var shells = Array.prototype.slice.call(root.querySelectorAll('.docs-shell'));
        if (root.matches && root.matches('.docs-shell')) shells.unshift(root);
        shells.forEach(function (shell) { shell.setAttribute('data-sidebar-collapsed', 'true'); });
    }

    function mobileShell(el) {
        return el.closest('.docs-shell, .home-shell') || document.documentElement;
    }

    // Survives the circuit's DOM swap the same way preview tabs do.
    var mobileNavOpen = false;

    function setMobileNav(shell, open) {
        mobileNavOpen = open;
        shell.setAttribute('data-mobile-open', open ? 'true' : 'false');
        shell.querySelectorAll('[data-mobile-nav-toggle]').forEach(function (b) {
            b.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
    }

    function closeMobileNav() {
        mobileNavOpen = false;
        document.querySelectorAll('[data-mobile-open="true"]').forEach(function (shell) { setMobileNav(shell, false); });
    }

    function restoreMobileNav(root) {
        if (!mobileNavOpen || !root.matches) return;
        var shell = root.matches('.docs-shell, .home-shell') ? root : root.querySelector('.docs-shell, .home-shell');
        if (shell && shell.getAttribute('data-mobile-open') !== 'true') setMobileNav(shell, true);
    }

    function onMobileNavClick(e) {
        var toggle = e.target.closest('[data-mobile-nav-toggle]');
        if (toggle) {
            var shell = mobileShell(toggle);
            setMobileNav(shell, shell.getAttribute('data-mobile-open') !== 'true');
            return;
        }
        if (e.target.closest('[data-mobile-nav-close]') ||
            e.target.closest('.docs-sidebar-slot a[href], .docs-header-mobile-menu a[href]')) {
            closeMobileNav();
        }
    }

    function onMobileNavKeydown(e) {
        if (e.key === 'Escape') closeMobileNav();
    }

    // Re-run on enhancedload: heading IDs change per page, so the old observer is stale.
    function initToc() {
        document.querySelectorAll('[data-toc-list]').forEach(function (list) {
            if (list.__tocHandle) {
                try { list.__tocHandle.dispose(); } catch (e) {}
                list.__tocHandle = null;
            }
            var raw = list.getAttribute('data-toc-ids') || '';
            var ids = raw.split(',').map(function (s) { return s.trim(); }).filter(Boolean);
            if (ids.length === 0) return;
            list.__tocHandle = window.shelldocsToc.attach(list, ids);
        });
    }

    /* Document-level listeners attach immediately: waiting for DOMContentLoaded
       (which also waits on module scripts such as the Shiki import) left early
       clicks on prerendered chrome with no handler. */
    document.addEventListener('click', onSidebarClick);
    document.addEventListener('click', onDropdownClick);
    document.addEventListener('keydown', onDropdownKeydown);
    document.addEventListener('click', onPreviewClick);
    document.addEventListener('keydown', onPreviewKeydown);
    document.addEventListener('click', onTabsClick);
    document.addEventListener('keydown', onTabsKeydown);
    document.addEventListener('click', onMobileNavClick);
    document.addEventListener('click', onCollapseClick);
    document.addEventListener('keydown', onMobileNavKeydown);
    window.shelldocsOnEnhancedLoad(closeMobileNav);
    watchForReplacedFrames();
    // The shell is already parsed here; restoring now avoids a flash of the open sidebar.
    restoreCollapse(document);

    function initPage() {
        initToc();
        restoreCollapse(document);
        restoreTabs(document);
        window.shelldocsIframes.init(document);
    }

    if (document.readyState !== 'loading') {
        initPage();
    } else {
        document.addEventListener('DOMContentLoaded', initPage);
    }
    window.shelldocsOnEnhancedLoad(initPage);

    return { initToc: initToc };
})();
