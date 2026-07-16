# ShellDocs Design Tokens

`ShellDocs.Tokens` is a tiny NuGet package that ships **one file**: `tokens.css`. It defines the CSS custom properties every ShellDocs component reads, and is the same surface any third-party Blazor UI library (ShellUI, custom) can consume to share the visual system.

Split out from `ShellDocs.Components` in `feat/design-tokens` so consumers who want *just the tokens* (custom-styled docs sites, or ShellUI users who don't need the docs chrome) can depend on it without pulling the whole components RCL.

## What's in it

A single stylesheet — `_content/ShellDocs.Tokens/tokens.css` — that defines two `:root` blocks: one for light mode and one scoped to `.dark`. That's the whole package.

```html
<link rel="stylesheet" href="_content/ShellDocs.Tokens/tokens.css" />
```

Add the `.dark` class to `<html>` and every dark-mode variable takes over — no additional file, no attribute selector wiring.

## Token contract

Names below are **stable** — renaming any of them is a breaking change (major version bump). Values are free to shift for palette refinement (patch or minor).

### Surface

| Token | Purpose |
|---|---|
| `--background` | Page background |
| `--foreground` | Page text |
| `--card` | Slightly-elevated surface (feature cards, code blocks) |
| `--card-foreground` | Text on `--card` |
| `--popover` | Floating surfaces (dropdowns, mega-menu, package selector menu) |
| `--popover-foreground` | Text on `--popover` |

### Actions

| Token | Purpose |
|---|---|
| `--primary` | Primary action colour (the coloured TOC thumb, primary buttons) |
| `--primary-foreground` | Text on `--primary` |
| `--secondary` | Secondary action bg |
| `--secondary-foreground` | Text on `--secondary` |

### Neutrals

| Token | Purpose |
|---|---|
| `--muted` | Backgrounds for muted UI (nav-link hover, code inline bg, kbd) |
| `--muted-foreground` | Muted text (page descriptions, hints, breadcrumbs) |
| `--accent` | Subtle translucent grey — sidebar active-item bg, hover accents |
| `--accent-foreground` | Text on `--accent` |

### Borders + form

| Token | Purpose |
|---|---|
| `--border` | Default border (all rounded chrome — cards, buttons, inputs) |
| `--border-strong` | Elevated border for interactive-hover state |
| `--input` | Form input background |
| `--ring` | Focus ring colour (2-pixel outline) |

### Semantic

The **only** vibrant tokens — reserved for meaning, never decoration.

| Token | Purpose |
|---|---|
| `--info` | Info callouts, informational badges |
| `--warning` | Warning callouts, deprecated markers |
| `--error` | Error callouts, destructive actions |
| `--success` | Success callouts, checkmark indicators |

### Scale

| Token | Default | Purpose |
|---|---|---|
| `--radius` | `0.5rem` | Base corner radius. Larger surfaces use `calc(var(--radius) + 2px)`; small chips use `calc(var(--radius) - 3px)`. |
| `--sidebar-width` | `17.5rem` | Docs sidebar width |
| `--toc-width` | `14rem` | Right-rail TOC width |
| `--header-height` | `3.5rem` | Sticky header offset |

### Typography

| Token | Purpose |
|---|---|
| `--font-sans` | Body text (`Inter var` first, then system stack) |
| `--font-mono` | Code (`ui-monospace`, JetBrains Mono, then system) |

`tokens.css` also `@import`s the Inter font from `rsms.me/inter/inter.css`. If you don't want Inter, override `--font-sans` in your own stylesheet loaded after `tokens.css` — the browser will just skip the unused font-face rules.

## Overriding

Load `tokens.css` first, then your override stylesheet. Any variable you re-declare on `:root` wins via the cascade.

```html
<link rel="stylesheet" href="_content/ShellDocs.Tokens/tokens.css" />
<link rel="stylesheet" href="my-overrides.css" />
```

```css
/* my-overrides.css */
:root {
    --primary: hsl(220, 90%, 55%);          /* switch primary to blue */
    --radius: 0.75rem;                      /* softer corners */
    --sidebar-width: 20rem;                 /* wider sidebar */
}

:root.dark {
    --primary: hsl(220, 80%, 65%);          /* dark-mode primary */
}
```

Per-page or per-scope overrides work too — anywhere the cascade applies.

## Integration paths

### With ShellDocs.Components (default)

`ShellDocs.Components` transitively depends on `ShellDocs.Tokens`, so a project that installs `ShellDocs.Components` gets tokens available at `_content/ShellDocs.Tokens/tokens.css`. Add the `<link>` tag once in your `App.razor` head.

### With ShellUI (Tailwind install)

ShellUI's Tailwind config reads the same variable names (`--primary`, `--muted`, `--border`, `--radius`, etc.). Load `tokens.css` once and ShellUI components inherit the palette automatically. This is the recommended path for Phase 1/2.

### With ShellUI (NuGet, future)

Deferred to `feat/shellui-nuget-interop` (Phase 3). ShellUI's RCL will offer an `AddShellUI(o => o.UseSharedTokens())` opt-in that suppresses its own token emission when `ShellDocs.Tokens` is on the classpath, avoiding duplicate `:root` blocks.

### Standalone (no components RCL)

You can depend on `ShellDocs.Tokens` alone if you want *just the palette* for a custom-styled Blazor site — no ShellDocs sidebar, no ShellDocs header. The tokens are all you get.

## Stability

- **Names** — stable across major versions. Renames are breaking.
- **Values** — may shift between minor versions as the palette is refined. If your site depends on a specific hue, override the token in your own stylesheet.
- **Add-only** — new tokens can appear in minor versions without breaking existing consumers.
- **Deprecations** — flagged one minor version ahead of removal, with a fallback alias for the transition.

## What's NOT in tokens.css

- Base HTML resets (`html`, `body`, `*` box-sizing) — those live in `ShellDocs.Components/wwwroot/shelldocs-theme.css` alongside the prose typography and code-block chrome.
- Component-specific styles (`.shelldocs-prose`, `.shelldocs-codeblock`, scrollbar overrides, Prism overrides) — same location.
- Font files — the Inter font is `@import`ed from `rsms.me`; hosted assets aren't shipped in the package.

If you use `ShellDocs.Components`, you get both files. If you use *just* `ShellDocs.Tokens`, you get variables only — bring your own component styles.
