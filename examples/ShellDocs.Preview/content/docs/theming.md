---
title: Theming
description: How ShellDocs' theme layer works, and how to override it.
order: 1
---

# Theming

ShellDocs ships with three theme presets — **Shadcn** (default), **Fuma**, and **Nextra**. Under the hood, each preset is a set of CSS custom properties that everything else reads from.

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

Override any of these in your app's CSS and every component follows.

## Dark mode

Toggle by adding a `dark` class to `<html>`. ShellDocs' header comes with a `ThemeToggle` that persists via `localStorage` and reads `prefers-color-scheme` on first visit.
