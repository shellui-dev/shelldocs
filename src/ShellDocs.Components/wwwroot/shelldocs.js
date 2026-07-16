window.ShellDocs = window.ShellDocs || {};

// Re-highlight all code blocks. Called from MarkdownContent after render.
window.shelldocsHighlight = function () {
    if (window.Prism) {
        try { window.Prism.highlightAll(); } catch (e) {}
    }
};

// Highlight a specific <pre> element — used by PreviewFrame when the code tab
// mounts, so we don't re-scan the whole page on every tab flip.
window.shelldocsHighlightElement = function (preEl) {
    if (!preEl || !window.Prism) return;
    var code = preEl.querySelector('code');
    if (!code) return;
    try { window.Prism.highlightElement(code); } catch (e) {}
};

// Copy-to-clipboard for code blocks.
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

/* Simple TOC scroll spy. IntersectionObserver on the content headings; on
   change, move .toc-bar to the active TOC link and toggle .active. No masks,
   no SVGs, no polling. */
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
