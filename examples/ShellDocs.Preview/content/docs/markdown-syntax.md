---
title: Markdown Syntax
description: How to author content that goes beyond plain markdown.
order: 2
---

# Markdown syntax

ShellDocs takes standard CommonMark markdown and adds two extensions.

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

Reference Blazor components mid-content with self-closing PascalCase tags. Registered components render live; unknown ones warn at build time and pass through untouched.

## `razor:preview` fenced blocks

A code fence with the info string `razor:preview` renders as a live component preview alongside the source, in a tabbed container.
