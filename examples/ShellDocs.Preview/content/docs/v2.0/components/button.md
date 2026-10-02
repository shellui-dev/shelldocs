---
title: Button
description: Multi-sibling previews, enum forms and a demo-file preview.
---

# Button

## Variants

Every top-level sibling in a `razor:preview` fence renders, in order. Enum
values accept the Razor forms `Destructive`, `ButtonVariant.Destructive` and
`@ButtonVariant.Destructive`.

```razor:preview
<Button>Default</Button>
<Button Variant="ButtonVariant.Destructive">Destructive</Button>
<Button Variant="@ButtonVariant.Outline">Outline</Button>
<Button Variant="Secondary">Secondary</Button>
```

## Inside a wrapper

Plain HTML wrappers and text render too:

```razor:preview
<div style="display: flex; gap: 0.5rem; align-items: center;">
    <Button Size="ButtonSize.Small">Small</Button>
    <Button>Medium</Button>
    <Button Size="ButtonSize.Large" Disabled="@true">Large (disabled)</Button>
</div>
<span>Text after the row renders as well.</span>
```

## Attributes a static preview can't evaluate

`OnClick="HandleClick"`, `@onclick`, `@bind-*` and `@ref` are skipped with a
logged warning; the component still renders:

```razor:preview
<Button OnClick="HandleClick" @ref="btn" Variant="ButtonVariant.Secondary">Still renders</Button>
```

## Stateful demo

Demos that need `@code` live in `Demos/*.razor`; the source tab shows the file.

<DemoPreview Component="ButtonClickDemo" Title="Click counter" />
