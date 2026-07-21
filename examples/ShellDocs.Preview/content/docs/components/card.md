---
title: Card
description: Bordered cards with title, description, optional icon, and optional link.
category: Components
order: 12
---

# Card

`<Card>` renders a bordered rounded panel with a title, description, and optional icon. Pass `Href` to make the whole card a link.

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

Wrap Cards in `<CardGrid Columns="2">` for a responsive 2-col (or 3-col) layout.

```razor:preview
<CardGrid Columns="2">
    <Card Title="Fast" Description="Instant page loads, client-rendered islands only where needed." />
    <Card Title="Themeable" Description="One CSS var for every colour; override in your own stylesheet." />
    <Card Title="Composable" Description="Every layout, header, and TOC is a Blazor component you can swap." />
    <Card Title="Static-ready" Description="Deploy to GH Pages, Cloudflare, or S3 as pre-rendered HTML." />
</CardGrid>
```

## LinkCard

For "further reading" panels, `<LinkCard>` is a compact one-line variant with a hover arrow.

```razor:preview
<LinkCard Title="Frontmatter reference" Description="Every property you can set at the top of a .md file." Href="/docs/markdown-syntax" />
```
