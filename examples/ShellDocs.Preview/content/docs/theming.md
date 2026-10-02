---
title: Theming
description: How ShellDocs' theme layer works, and how to override it.
order: 1
---

# Theming

ShellDocs ships one neutral, shadcn-shaped palette, defined as CSS custom properties in `ShellDocs.Tokens` (`tokens.css`) that every component reads from. `ShellDocsOptions.Theme` (and `shelldocs init --theme`) is reserved for future presets and doesn't change the palette yet; restyle by overriding tokens.

## The token layer

Every component reads from a small set of tokens:

| Token | Purpose |
|---|---|
| `--background` | Page background |
| `--foreground` | Primary text |
| `--muted` / `--muted-foreground` | Backgrounds and text for secondary chrome |
| `--card` / `--card-foreground` | Card surfaces (previews, tooltips) |
| `--border` | All 1px lines |
| `--primary` / `--primary-foreground` | Emphasis surfaces (CTAs) |
| `--radius` | Corner radius scale |

Override any of these in a stylesheet loaded after `tokens.css` and every component follows. The full list is in `docs/TOKENS.md`.

## Dark mode

Dark mode is the `dark` class on `<html>`. The `ThemeToggle` in the header / sidebar footer saves the choice in `localStorage` and falls back to `prefers-color-scheme` on the first visit; an inline script in `App.razor` applies it before first paint. The toggle is handled by `shelldocs.js`, so it works on static builds too. Whatever flips the class, including a component library's own theme toggle, is saved the same way, so the choice sticks across pages.
