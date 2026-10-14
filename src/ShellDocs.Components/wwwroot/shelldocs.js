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

/* Search. <SearchDialog> renders a hidden template; opening clones it onto <body>,
   outside the DOM Blazor owns, and ranks /_shelldocs/search.json in the browser, so
   it works the same on a static host. Cmd/Ctrl+K and [data-search-open] open it. */
window.shelldocsSearch = (function () {
    var index = null;
    var loading = null;
    var failed = false;
    var live = null;
    var results = [];
    var highlighted = 0;
    var returnFocus = null;

    function load() {
        if (!loading) {
            failed = false;
            loading = fetch(new URL('_shelldocs/search.json', document.baseURI))
                .then(function (r) {
                    if (!r.ok) throw new Error('search index: ' + r.status);
                    return r.json();
                })
                .then(function (data) {
                    (data.entries || []).forEach(function (e) {
                        e.lt = (e.t || '').toLowerCase();
                        e.ld = (e.d || '').toLowerCase();
                        e.ls = (e.s || '').toLowerCase();
                        e.lb = (e.b || '').toLowerCase();
                    });
                    index = { versions: data.versions || [], entries: data.entries || [] };
                })
                .catch(function () { failed = true; loading = null; });
        }
        return loading;
    }

    // Same rules as UrlPath.Normalize / IsUnder.
    function normalize(path) {
        var s = (path || '').split(/[?#]/)[0];
        if (s.charAt(0) !== '/') s = '/' + s;
        while (s.length > 1 && s.charAt(s.length - 1) === '/') s = s.slice(0, -1);
        return s.toLowerCase();
    }

    function isUnder(path, root) {
        var r = normalize(root);
        if (r === '/') return true;
        return path.indexOf(r) === 0 && (path.length === r.length || path.charAt(r.length) === '/');
    }

    function currentVersion() {
        var base = new URL(document.baseURI).pathname;
        var path = location.pathname;
        path = normalize(path.indexOf(base) === 0 ? path.slice(base.length) : path);
        var best = null, latest = null;
        index.versions.forEach(function (v) {
            if (v.latest && !latest) latest = v;
            if (isUnder(path, v.root) && (!best || v.root.length > best.root.length)) best = v;
        });
        return best || latest || index.versions[0] || null;
    }

    // The current version's entries, plus those outside every version.
    function scoped() {
        var current = currentVersion();
        return index.entries.filter(function (e) { return !e.v || (current && e.v === current.id); });
    }

    function evaluate(e, q, tokens) {
        var score = 0, bodyHit = false;
        if (e.lt.indexOf(q) >= 0) score += 100;
        if (e.lt.indexOf(q) === 0) score += 50;
        if (e.ls.indexOf(q) >= 0) score += 20;
        if (e.ld.indexOf(q) >= 0) score += 10;
        if (e.lb.indexOf(q) >= 0) { score += 6; bodyHit = true; }

        for (var i = 0; i < tokens.length; i++) {
            var t = tokens[i];
            if (e.lt.indexOf(t) >= 0) score += 8;
            else if (e.ls.indexOf(t) >= 0) score += 4;
            else if (e.ld.indexOf(t) >= 0) score += 2;
            else if (e.lb.indexOf(t) >= 0) { score += 1; bodyHit = true; }
            else return null;
        }

        if (e.k === 'p') score += 2;
        return { entry: e, score: score, snippet: bodyHit ? snippet(e, tokens) : null };
    }

    // ~150 characters around the first token match in the body.
    function snippet(e, tokens) {
        var hit = -1;
        tokens.forEach(function (t) {
            var at = e.lb.indexOf(t);
            if (at >= 0 && (hit < 0 || at < hit)) hit = at;
        });
        if (hit < 0) return null;
        var start = Math.max(0, hit - 75);
        var end = Math.min(e.b.length, hit + 75);
        var text = e.b.slice(start, end).trim();
        if (start > 0) text = '… ' + text;
        if (end < e.b.length) text += ' …';
        return text;
    }

    function rank(query) {
        var entries = scoped();
        var q = query.trim().toLowerCase();
        if (!q) return entries.slice(0, 20).map(function (e) { return { entry: e, score: 0, snippet: null }; });
        var tokens = q.split(' ').filter(Boolean);
        var scored = [];
        entries.forEach(function (e) {
            var s = evaluate(e, q, tokens);
            if (s) scored.push(s);
        });
        scored.sort(function (a, b) { return b.score - a.score; });
        return scored.slice(0, 30);
    }

    function part(selector) { return live.querySelector(selector); }

    function row(kind) { return part('[data-search-rows] [data-search-row="' + kind + '"]').cloneNode(true); }

    function fill(el, selector, text) {
        var target = el.querySelector(selector);
        if (text) target.textContent = text; else target.remove();
    }

    function message(host, text) {
        var el = row('empty');
        el.textContent = text;
        host.appendChild(el);
    }

    function render() {
        var host = part('[data-search-results]');
        var query = part('[data-search-input]').value;
        host.textContent = '';
        if (!index) {
            message(host, failed ? 'Search is unavailable.' : 'Loading…');
            return;
        }
        if (results.length === 0) {
            if (query.trim()) message(host, 'No results for “' + query + '”.');
            return;
        }
        results.forEach(function (r, i) {
            var el = row(r.entry.k === 'h' ? 'h' : 'p');
            el.id = 'shelldocs-search-option-' + i;
            el.href = new URL(r.entry.u.replace(/^\//, ''), document.baseURI).href;
            fill(el, '.search-result-title', r.entry.t);
            fill(el, '.search-result-section', r.entry.s);
            fill(el, '.search-result-snippet', r.snippet);
            host.appendChild(el);
        });
        highlight(highlighted, false);
    }

    function highlight(i, scroll) {
        var rows = part('[data-search-results]').querySelectorAll('.search-result');
        if (rows.length === 0) return;
        highlighted = (i + rows.length) % rows.length;
        Array.prototype.forEach.call(rows, function (el, n) {
            el.classList.toggle('highlighted', n === highlighted);
            el.setAttribute('aria-selected', n === highlighted ? 'true' : 'false');
        });
        part('[data-search-input]').setAttribute('aria-activedescendant', rows[highlighted].id);
        if (scroll) rows[highlighted].scrollIntoView({ block: 'nearest' });
    }

    function recalc() {
        if (!live) return;
        results = index ? rank(part('[data-search-input]').value) : [];
        highlighted = 0;
        render();
    }

    function onDialogKeydown(e) {
        if (e.key === 'Escape') { e.preventDefault(); close(); }
        else if (e.key === 'ArrowDown') { e.preventDefault(); highlight(highlighted + 1, true); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(highlighted - 1, true); }
        else if (e.key === 'Tab') { e.preventDefault(); part('[data-search-input]').focus(); }
        else if (e.key === 'Enter') {
            e.preventDefault();
            var rows = part('[data-search-results]').querySelectorAll('.search-result');
            if (rows[highlighted]) rows[highlighted].click();
        }
    }

    function onDialogClick(e) {
        if (e.target === live) { close(); return; }
        // Closing in this tick would detach the link before the browser follows it.
        if (e.target.closest('.search-result')) setTimeout(close, 0);
    }

    function onDialogMouseOver(e) {
        var el = e.target.closest && e.target.closest('.search-result');
        if (!el) return;
        var rows = part('[data-search-results]').querySelectorAll('.search-result');
        var at = Array.prototype.indexOf.call(rows, el);
        if (at >= 0 && at !== highlighted) highlight(at, false);
    }

    function open() {
        if (live && !live.isConnected) live = null;
        if (live) { part('[data-search-input]').focus(); return; }
        var template = document.querySelector('[data-search-dialog]');
        if (!template) return;

        returnFocus = document.activeElement;
        live = template.cloneNode(true);
        live.removeAttribute('data-search-dialog');
        live.removeAttribute('hidden');
        document.body.appendChild(live);
        live.addEventListener('keydown', onDialogKeydown);
        live.addEventListener('click', onDialogClick);
        live.addEventListener('mouseover', onDialogMouseOver);
        part('[data-search-input]').addEventListener('input', recalc);

        var pending = index ? null : load();
        recalc();
        part('[data-search-input]').focus();
        if (pending) pending.then(recalc);
    }

    function close() {
        if (!live) return;
        live.remove();
        live = null;
        if (returnFocus && returnFocus.isConnected && returnFocus.focus) returnFocus.focus();
        returnFocus = null;
    }

    document.addEventListener('keydown', function (e) {
        if ((e.key === 'k' || e.key === 'K') && (e.metaKey || e.ctrlKey) && document.querySelector('[data-search-dialog]')) {
            e.preventDefault();
            open();
        }
    });
    document.addEventListener('click', function (e) {
        if (e.target.closest && e.target.closest('[data-search-open]')) open();
    });
    window.shelldocsOnEnhancedLoad(close);

    return { open: open, close: close };
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
            host.__shelldocsIframe = { src: host.getAttribute('data-src'), lazy: host.getAttribute('data-lazy') };
            var lazy = host.getAttribute('data-lazy');
            if (lazy === 'none') load(host);
            else if (lazy !== 'click') watch(host);
        });
    }

    /* Navigating within the site can reuse a host for the next page's preview and
       change only its data-src (or data-lazy), which would leave the previous page's
       frame loaded and its Run button hidden. Unload it and set the host up again. */
    function reset(host) {
        var state = host.__shelldocsIframe;
        if (!state) return;
        if (state.src === host.getAttribute('data-src') && state.lazy === host.getAttribute('data-lazy')) return;
        if (observer) observer.unobserve(host);
        host.removeAttribute('data-loaded');
        var frame = host.querySelector('iframe');
        if (frame && frame.hasAttribute('src')) frame.src = 'about:blank';
        host.__shelldocsIframe = null;
        init(host);
    }

    if (window.MutationObserver) {
        new MutationObserver(function (records) {
            records.forEach(function (r) {
                if (r.target.matches && r.target.matches('[data-iframe-preview]')) reset(r.target);
            });
        }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-src', 'data-lazy'], subtree: true });
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

    /* 404.html is one file for every unknown URL, so its path line ships blank and
       hidden; fill in the visitor's path, relative to the site's base. */
    function fillNotFoundPath() {
        document.querySelectorAll('[data-not-found-path][hidden]').forEach(function (line) {
            var code = line.querySelector('code');
            if (!code || code.textContent) return;
            var base = new URL(document.baseURI).pathname.replace(/\/$/, '');
            var path = location.pathname;
            if (base && path.indexOf(base + '/') === 0) path = path.slice(base.length);
            code.textContent = decodeURIComponent(path);
            line.hidden = false;
        });
    }

    function initPage() {
        initToc();
        fillNotFoundPath();
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
