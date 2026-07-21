---
title: ComponentPreview
description: Live-render a registered component by name with declarative props and a reveal-on-click source view.
category: Components
order: 65
---

# ComponentPreview

`<ComponentPreview>` is the declarative-prop cousin of the `razor:preview` fence. Instead of authoring a full razor snippet inside a fenced code block, you pass the target component's **name** as a string plus its props as attributes, and ShellDocs renders it live — the source view is reconstructed from those same props on demand.

## Basic

<ComponentPreview Component="Callout" Variant="info" Title="Heads up">
Body content that becomes the Callout's ChildContent.
</ComponentPreview>

## Self-closing

<ComponentPreview Component="LinkCard" Title="Getting started" Description="Install ShellDocs and scaffold your first docs site." Href="/docs/quick-start" />

## Props

- `Component` — required. The registered tag name (e.g. `"Callout"`, `"Card"`, `"LinkCard"`) to render. Resolved through the same `TypeRegistry` that backs `razor:preview`, so any component `AddShellDocs` registers works here.
- Any other attribute — forwarded to the target component. Attribute values are strings in the markdown; ShellDocs coerces them to each target property's declared type (`bool`, `int`, enums, etc.) at render time.
- `ChildContent` — the tag body becomes the target's `ChildContent` render fragment.

## Notes

- The reconstructed source string is sorted by attribute name for stability and shows the tag as self-closing when there's no body.
- If `Component` doesn't resolve, the render slot shows an inline `Unknown component:` error instead of throwing.
- Prefer `razor:preview` fences for multi-component demos; `<ComponentPreview>` is optimised for single-component prop-focused examples.
