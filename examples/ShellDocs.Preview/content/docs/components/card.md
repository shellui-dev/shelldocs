---
title: Card
description: Bordered cards with title, description, optional icon, and optional link.
category: Components
order: 12
---

# Card

`<Card>` renders a bordered rounded panel with a title, description, optional icon and optional body. Pass `Href` to make the whole card a link.

## Basic

```razor:preview
<Card Title="Read the docs" Description="Get started with a walkthrough of what's inside." />
```

## As a link

Add `Href` and the card becomes a link with a hover accent.

```razor:preview
<Card Title="Configuration" Description="Every option on ShellDocsOptions, one table." Href="/docs/introduction" />
```

## In a grid

Wrap Cards in `<CardGrid>` for a responsive grid. `Columns` is `2` (default) or `3`; the grid drops to one column on narrow screens, and `3` shows two columns at tablet widths.

```razor:preview
<CardGrid Columns="2">
    <Card Title="Markdown-first" Description="Drop a .md file into content/docs/ and it's a page." />
    <Card Title="Themeable" Description="Every colour is a CSS variable; override them in your own stylesheet." />
    <Card Title="Composable" Description="Every layout, header, and TOC is a Blazor component you can swap." />
    <Card Title="Static-ready" Description="Deploy to GH Pages, Cloudflare, or S3 as pre-rendered HTML." />
</CardGrid>
```

## LinkCard

For "further reading" panels, `<LinkCard>` is a compact variant with a hover arrow. `Href` is required.

```razor:preview
<LinkCard Title="Frontmatter reference" Description="Every property you can set at the top of a .md file." Href="/docs/markdown-syntax" />
```

## Props

- `Title`, `Description`: the heading and muted text.
- `Href`: makes the whole card a link.
- `External`: opens `Href` in a new tab (`target="_blank" rel="noopener"`). `LinkCard` takes it too.
- `IconSvg`: raw SVG markup rendered in the icon chip above the title.
- Child content: rendered below the description.
