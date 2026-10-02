window.ShellDocs = window.ShellDocs || {};

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

/* Enhanced navigation swaps the DOM and strips the .dark class the inline
   head script set, so re-apply the saved theme after every enhanced load. */
(function () {
    function applyTheme() {
        var saved = null;
        try { saved = localStorage.getItem('shelldocs-theme'); } catch (e) {}
        var systemDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        var isDark = (saved || (systemDark ? 'dark' : 'light')) === 'dark';
        document.documentElement.classList.toggle('dark', isDark);
    }
    applyTheme();
    document.addEventListener('enhancedload', applyTheme);
    window.shelldocsApplyTheme = applyTheme;
})();

// Swap <pre><code class="language-X"> for Shiki's output; data-shiki makes it idempotent.

function langOf(codeEl) {
    var cls = (codeEl.className || '').split(/\s+/);
    for (var i = 0; i < cls.length; i++) {
        if (cls[i].indexOf('language-') === 0) return cls[i].substring(9);
    }
    return null;
}

function highlightOne(preEl) {
    if (!preEl || !window.__shiki) return;
    if (preEl.dataset.shiki === 'done') return;
    var code = preEl.querySelector('code');
    if (!code) return;
    var lang = langOf(code);
    if (!lang) return;
    /* Languages Shiki hasn't loaded stay as plain text. */
    if (!window.__shiki.getLoadedLanguages().includes(lang)) return;

    var source = code.textContent;
    try {
        var html = window.__shiki.codeToHtml(source, {
            lang: lang,
            themes: { light: 'github-light', dark: 'github-dark' },
            defaultColor: false
        });
        var tpl = document.createElement('template');
        tpl.innerHTML = html.trim();
        var newPre = tpl.content.firstElementChild;
        if (!newPre) return;
        newPre.dataset.shiki = 'done';
        preEl.parentNode.replaceChild(newPre, preEl);
    } catch (e) { /* skip on grammar error */ }
}

window.shelldocsHighlight = function () {
    if (!window.__shiki) return;
    document.querySelectorAll('pre:not([data-shiki]) > code[class*="language-"]')
        .forEach(function (code) { highlightOne(code.parentElement); });
};

window.shelldocsHighlightElement = function (preEl) { highlightOne(preEl); };

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
            var code = frame && frame.querySelector('[data-preview-panel="code"] code');
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
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
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
    watchForReplacedFrames();

    if (document.readyState !== 'loading') {
        initToc();
    } else {
        document.addEventListener('DOMContentLoaded', initToc);
    }
    document.addEventListener('enhancedload', initToc);

    return { initToc: initToc };
})();
