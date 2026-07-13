---
title: Callout
description: Info, warning, tip, danger boxes for docs asides.
category: Components
order: 10
---

# Callout

Use `<Callout>` to break out of the reading flow with a short, high-signal note. Callouts render as a bordered strip with an icon, an optional bold title, and body copy. Four variants ship: `info`, `warning`, `tip`, and `danger`.

## Info

Neutral context — background, definitions, or clarifications. The default variant.

```razor:preview
<Callout Title="Note" Text="Callouts don't nest. If you find yourself wanting a callout inside a callout, split it into two." />
```

## Warning

A heads-up about something that could break — behaviour changes, deprecated APIs, non-obvious constraints.

```razor:preview
<Callout Variant="warning" Title="Heads up" Text="SyncKey stores the reader's selection in localStorage. Renaming a key later wipes previous preferences." />
```

## Tip

An optional-but-useful note. Best for "did you know" content that saves the reader time without being essential.

```razor:preview
<Callout Variant="tip" Title="Shortcut" Text="Press ⌘K anywhere on the site to jump straight into search." />
```

## Danger

Strong warning about destructive or irreversible actions. Use sparingly — if every callout on the page is a danger, none of them read as one.

```razor:preview
<Callout Variant="danger" Title="Data loss" Text="Running shelldocs init on an existing project overwrites content/meta.json without prompting. Back it up first." />
```

## Composition

Any of the four variants accept either a `Text` prop for one-liners, or a `ChildContent` slot when you need multiple paragraphs, links, or other components inside.

## Guidance

- **Fewer is more** — a page with four callouts reads as four things that matter. A page with fifteen reads as noise.
- **Lead with the takeaway** — the first sentence of a callout should be what a reader needs to know, not the setup.
- **Skip the title** for a single-sentence note. The title tag adds visual weight; use it when the callout body is more than one line.
