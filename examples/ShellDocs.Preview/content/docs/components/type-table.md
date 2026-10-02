---
title: TypeTable
description: Props / API reference tables, hand-written or generated from a component.
category: Components
order: 60
---

# TypeTable

Two ways to document a component's parameters: write the rows yourself with `<TypeTable>` / `<TypeRow>`, or let `<AutoTypeTable>` read them from the component.

## Hand-written

Nest one `<TypeRow>` per prop. The table renders a Prop / Type / Default / Description layout with a `required` badge.

```razor:preview
<TypeTable>
    <TypeRow Name="Variant" Type="string" Default="info" Description="One of info, warning, danger, tip." />
    <TypeRow Name="Title" Type="string" Description="Bold heading line above the body." />
    <TypeRow Name="ChildContent" Type="RenderFragment" Description="Body content: markdown or nested components." Required="true" />
</TypeTable>
```

### `<TypeRow>` props

- `Name` — the prop name in the first column (rendered as code)
- `Type` — the type signature, e.g. `string`, `bool`, `int?`, `RenderFragment`
- `Default` — literal default value; omit for none (renders as `—`)
- `Description` — free-text explanation
- `Required` — shows a `required` badge next to the name

Rows render in source order; a repeated `Name` is dropped.

## Generated

`<AutoTypeTable Component="Name" />` reflects on a registered component's `[Parameter]` properties: types (with nullability and generic arguments), defaults (from `[DefaultValue]` or the property initializer), `required` from `[EditorRequired]`, and descriptions from the assembly's XML doc comments when the `.xml` file ships next to the DLL.

```razor:preview
<AutoTypeTable Component="Callout" />
```
