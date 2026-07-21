---
title: TypeTable
description: Structured props / API reference table for a component or type.
category: Components
order: 60
---

# TypeTable

`<TypeTable>` is the props / API reference primitive. Nest `<TypeRow>` children — one per prop — and the parent table renders a clean four-column layout (Prop / Type / Default / Description) with type-code chips and a `required` badge.

## Basic

<TypeTable>
    <TypeRow Name="Variant" Type="string" Default="info" Description="One of info | warning | danger | tip." />
    <TypeRow Name="Title" Type="string" Description="Bold heading line above the body." />
    <TypeRow Name="ChildContent" Type="RenderFragment" Description="Body content — markdown or nested Razor." Required="true" />
</TypeTable>

## Props

- `Name` — the prop name shown in the first column (renders as `<code>`)
- `Type` — the type signature, e.g. `string`, `bool`, `int?`, `RenderFragment`
- `Default` — literal default value, omit for none (renders as `—`)
- `Description` — free-text explanation, right-aligned column
- `Required` — badge next to the name when the prop must be supplied

## Notes

- Rows render in source order, deduplicated by `Name` — repeated names silently drop.
- Type auto-generation from XML doc comments ships in v2 via `ShellDocs.Xml`.
