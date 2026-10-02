---
title: Markdown Syntax
description: How to author content that goes beyond plain markdown.
order: 2
---

# Markdown syntax

ShellDocs takes standard CommonMark markdown (plus tables and other common extensions via Markdig) and adds frontmatter, inline component tags and `razor:preview` fences.

## Frontmatter

Every page starts with a YAML frontmatter block. It's what feeds the navigation graph:

```yaml
---
title: Button
description: Interactive button component
category: Form
order: 10
---
```

## Standard markdown works

Headings, lists, tables, code fences, images, links — all standard:

- Bullets like this
- Are perfectly normal

## Inline component tags

Reference registered Blazor components mid-content with PascalCase tags, self-closing or with a body. They render live:

<Callout Variant="tip" Title="This is a live component">
Written as a `<Callout>` tag in the middle of this page's markdown.
</Callout>

Unknown tags log a warning and pass through as raw markup. Tags inside inline code (`` `<Callout>` ``) and fenced code blocks stay literal.

## `razor:preview` fenced blocks

A code fence with the info string `razor:preview` renders live in a preview frame with **Preview | Code** tabs, a copy button and a ⋯ menu. Every top-level element in the fence renders, in order:

```razor:preview
<div style="display: flex; gap: 0.75rem; flex-wrap: wrap;">
    <Callout Variant="info" Text="First sibling." />
    <Callout Variant="warning" Text="Second sibling, same frame." />
</div>
```

Attribute values accept Razor forms such as `Variant="ButtonVariant.Destructive"`, `@true` and `[Flags]` values like `Bold | Italic`. Attributes a static preview can't evaluate (`OnClick="Handler"`, `@bind-*`, `@ref`) are skipped with a logged warning. For demos that need `@code`, see `<DemoPreview>` in the README.

### Child content is Razor

Inside a fence, component bodies are Razor markup, not markdown. Elements wrap nested components exactly as written, and nothing is wrapped in `<p>`:

```razor:preview
<Card Title="Nested markup">
    <div style="display: flex; gap: 0.75rem; align-items: center;"><span>Inside a div:</span><Callout Variant="tip" Text="a nested component" /></div>
</Card>
```

Inline component tags in prose (outside a fence) still take markdown bodies.

### Layout

Examples are centred. Add `stretch` to the info string (`razor:preview stretch`) so block-level components such as charts, inputs and tables fill the frame's width. `<DemoPreview>` and `<ComponentPreview>` take `Layout="stretch"`.

```razor:preview stretch
<Callout Variant="info" Text="This callout fills the frame instead of shrinking to its text." />
```

Preview frames are marked `not-prose`, so the page's typography (paragraph margins, list padding, link underlines) never reaches the components inside. Add the class to any other element that should opt out.
