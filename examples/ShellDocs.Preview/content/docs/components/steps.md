---
title: Steps
description: Vertically-numbered onboarding sequence with a connecting rail.
category: Components
order: 40
---

# Steps

Use `<Steps>` to lay out an onboarding sequence — install, configure, run. Each `<Step>` is auto-numbered by its position; you don't manage the counter.

## Basic

```razor:preview
<Steps>
    <Step Title="Install the CLI">
        Install the ShellDocs global tool from NuGet.
    </Step>
    <Step Title="Scaffold a project">
        Run <code>shelldocs init</code> in your repo root.
    </Step>
    <Step Title="Author your first page">
        Edit <code>content/docs/introduction.md</code>. It hot-reloads.
    </Step>
    <Step Title="Ship it">
        <code>shelldocs build</code> emits a static site. Deploy anywhere.
    </Step>
</Steps>
```

## Notes

- Each Step's `Title` is optional — omit for a rendered numbered paragraph.
- Nest richer content (paragraphs, code, callouts) via `ChildContent`.
- The rail is drawn from the outer `<Steps>` `border-left`; step number chips overlay it.
