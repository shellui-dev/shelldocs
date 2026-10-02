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

    function onPackageClick(e) {
        var trigger = e.target.closest('.pkg-trigger');
        var openPkg = document.querySelector('.pkg[data-open="true"]');

        if (trigger) {
            var pkg = trigger.closest('.pkg');
            if (!pkg) return;
            var isOpen = pkg.getAttribute('data-open') === 'true';
            if (openPkg && openPkg !== pkg) closePkg(openPkg);
            pkg.setAttribute('data-open', isOpen ? 'false' : 'true');
            trigger.setAttribute('aria-expanded', isOpen ? 'false' : 'true');
            var chevron = pkg.querySelector('.pkg-chevron');
            if (chevron) chevron.classList.toggle('open', !isOpen);
            return;
        }

        if (openPkg && !e.target.closest('.pkg-menu')) closePkg(openPkg);
    }

    function closePkg(pkg) {
        pkg.setAttribute('data-open', 'false');
        var trigger = pkg.querySelector('.pkg-trigger');
        if (trigger) trigger.setAttribute('aria-expanded', 'false');
        var chevron = pkg.querySelector('.pkg-chevron');
        if (chevron) chevron.classList.remove('open');
    }

    function onPreviewClick(e) {
        var toggle = e.target.closest('[data-preview-toggle]');
        if (toggle) {
            var mode = toggle.getAttribute('data-preview-toggle');
            var frame = toggle.closest('.preview-frame, .component-preview');
            if (!frame) return;
            var expand = mode === 'expand';
            frame.classList.toggle('expanded', expand);
            frame.classList.toggle('collapsed', !expand);
            return;
        }

        var copy = e.target.closest('[data-preview-copy]');
        if (copy) {
            var frame = copy.closest('.preview-frame, .component-preview');
            if (!frame) return;
            var code = frame.querySelector('pre code');
            if (!code) return;
            var text = code.innerText;
            var writeText = navigator.clipboard && navigator.clipboard.writeText
                ? navigator.clipboard.writeText(text)
                : Promise.reject(new Error('clipboard unavailable'));
            writeText.then(function () {
                copy.classList.add('copied');
                setTimeout(function () { copy.classList.remove('copied'); }, 1400);
            }).catch(function () { /* silent */ });
        }
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

    var delegatesAttached = false;
    function attachDelegates() {
        if (delegatesAttached) return;
        delegatesAttached = true;
        document.addEventListener('click', onSidebarClick);
        document.addEventListener('click', onPackageClick);
        document.addEventListener('click', onPreviewClick);
    }

    function boot() {
        attachDelegates();
        initToc();
    }

    if (document.readyState !== 'loading') {
        boot();
    } else {
        document.addEventListener('DOMContentLoaded', boot);
    }
    document.addEventListener('enhancedload', initToc);

    return { initToc: initToc };
})();
