---
title: ComponentPreview
description: Live-render a registered component by name with declarative props, inside the standard preview frame.
category: Components
order: 65
---

# ComponentPreview

`<ComponentPreview>` is the declarative-prop cousin of the `razor:preview` fence. Instead of writing a razor snippet in a fenced block, you pass the target component's **name** plus its props as attributes. ShellDocs renders it live in the same frame as every other example, with **Preview | Code** tabs, a copy button and the ⋯ menu. The Code tab shows source rebuilt from those same props.

## Basic

<ComponentPreview Component="Callout" Variant="info" Title="Heads up">
Body content that becomes the Callout's ChildContent.
</ComponentPreview>

## Self-closing

<ComponentPreview Component="LinkCard" Title="Getting started" Description="Install ShellDocs and scaffold your first docs site." Href="/docs/installation" />

## Props

- `Component` — required. The registered tag name (`"Callout"`, `"Card"`, `"LinkCard"`, …). Resolved through the same registry as `razor:preview`, so anything `AddShellDocs` or your `RegisterComponent*` calls register works.
- Any other attribute — forwarded to the target. Values are strings in markdown and are coerced to each parameter's type (`bool`, numbers, enums including `Type.Member` and `A | B` flags). Attributes that can't be set this way are skipped with a logged warning.
- `Layout` — `"center"` (default) or `"stretch"`, which lets block-level components fill the frame's width.
- The tag body is parsed as Razor (HTML and registered components, no markdown) and becomes the target's `ChildContent`. Child tags named after one of the target's `RenderFragment` parameters fill that slot.

## Notes

- The rebuilt source sorts attributes by name and shows the tag self-closing when there's no body.
- If `Component` doesn't resolve, the frame shows a "ComponentPreview error" panel instead of throwing.
- Prefer `razor:preview` fences for multi-component examples; `<ComponentPreview>` is aimed at single-component, prop-focused ones.
