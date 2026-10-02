---
title: CodeBlock
description: Fenced code blocks with Shiki highlighting, a language badge and a copy button.
---

# CodeBlock

There's no component to call: every fenced code block in your markdown becomes a code block with a language badge, a copy button, and syntax highlighting.

```csharp
builder.Services.AddShellDocs(o =>
{
    o.SiteName = "My Docs";
    o.GitHubRepo = "acme/my-docs";
});
```

## Highlighting

Highlighting is done in the browser by [Shiki](https://shiki.style) with the `github-light` / `github-dark` themes, following the site's light/dark mode. Load Shiki in your `App.razor` (`shelldocs init` adds this) and list the languages you need:

```js
import { createHighlighter } from 'https://esm.sh/shiki@1.24.0';
window.__shiki = await createHighlighter({
    themes: ['github-light', 'github-dark'],
    langs: ['razor', 'csharp', 'html', 'json', 'yaml', 'bash', 'typescript', 'javascript', 'markdown']
});
if (window.shelldocsHighlight) window.shelldocsHighlight();
```

A fence whose language isn't loaded still renders, as plain monospaced text. Highlighting is applied beside the original code rather than replacing it, so it stays correct when Blazor re-renders a page.

## Copy button

The copy button copies the original source text, not the highlighted markup, and shows a check mark once it succeeds.

## Related

- [`<CodeGroup>`](/docs/components/code-group) — several code samples behind tabs.
- `razor:preview` fences — a live component render with its source; see [Markdown syntax](/docs/markdown-syntax).
