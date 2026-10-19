---
title: Theming
description: How ShellDocs' theme layer works, and how to override it.
order: 1
---

# Theming

ShellDocs' palette is a set of CSS custom properties in `ShellDocs.Tokens` (`tokens.css`) that every component reads from. The default is Graphite, a neutral, shadcn-shaped palette; colour presets swap it for a tinted one, and you can override any token yourself.

## Colour presets

```csharp
builder.Services.AddShellDocs(o =>
{
    o.Theme = ShellDocsTheme.Harbor;
});
```

| Preset | Colour |
|---|---|
| `Graphite` | Neutral greys (the default) |
| `Harbor` | Blue |
| `Lagoon` | Teal |
| `Forest` | Green |
| `Savanna` | Amber on sand |
| `Ember` | Red-orange |
| `Jacaranda` | Purple |

Each preset tints the backgrounds, borders and muted text with its hue and uses its colour for the active sidebar item, focus rings, links and other accents, in light and dark mode. Text stays at a contrast of at least 4.5:1. Component previews follow the preset too, as long as the components read the same tokens. `shelldocs init --theme harbor` writes the line for you.

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

Override any of these in a stylesheet loaded after `tokens.css` and every component follows, with or without a preset. The full list is in `docs/TOKENS.md`.

## Dark mode

Dark mode is the `dark` class on `<html>`. The `ThemeToggle` in the header / sidebar footer saves the choice in `localStorage` and falls back to `prefers-color-scheme` on the first visit; an inline script in `App.razor` applies it before first paint. The toggle is handled by `shelldocs.js`, so it works on static builds too. Whatever flips the class, including a component library's own theme toggle, is saved the same way, so the choice sticks across pages.
