---
title: CodeBlock
description: Syntax-highlighted code with copy button, filename tab, and line highlighting.
---

# CodeBlock

Syntax-highlighted code fences with a copy button, a filename tab, and configurable line highlighting.

## Highlighting

Shiki via WASM gives VSCode-parity syntax coloring across Razor, C#, JavaScript, TypeScript, JSON, YAML, Markdown, and shell.

## Copy button

Every code block ships with a copy button in the top-right corner. Click to copy the code text (not the highlighted markup) to the clipboard.

## Filename tab

Pass a `Filename` attribute to render a pill above the block with the filename. Great for `Program.cs`, `App.razor`, `wwwroot/index.html` context markers.

## Status

Shipping in `feat/codeblock-shiki` — the final Phase 1 branch before `0.1.0-alpha`.
