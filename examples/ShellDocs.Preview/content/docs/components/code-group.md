---
title: CodeGroup
description: Tabbed code samples that sync across the page.
category: Components
order: 25
---

# CodeGroup

`<CodeGroup>` groups multiple code samples into a tabbed panel — the reader picks one, sees that source, ignores the others. The most common use is a per-package-manager install snippet.

## Basic

```razor:preview
<CodeGroup>
    <CodeTab Label="npm">npm install shelldocs</CodeTab>
    <CodeTab Label="pnpm">pnpm add shelldocs</CodeTab>
    <CodeTab Label="yarn">yarn add shelldocs</CodeTab>
</CodeGroup>
```

## Sync groups

Pass `SyncKey` and every `<CodeGroup>` (or [`<Tabs>`](/docs/components/tabs)) with the same key switches together. Pick "pnpm" here and every other group with `SyncKey="pkg"` follows, on this page and on later ones.

```razor:preview
<CodeGroup SyncKey="pkg">
    <CodeTab Label="npm">npm run build</CodeTab>
    <CodeTab Label="pnpm">pnpm build</CodeTab>
    <CodeTab Label="yarn">yarn build</CodeTab>
</CodeGroup>
```

## Notes

- The first `<CodeTab>` in source order is selected on first render; a saved sync choice replaces it once `shelldocs.js` runs.
- The choice is saved in `localStorage` under the sync key, so it carries across pages and visits. Pick stable keys: renaming one resets readers' choice.
- Tabs are switched by `shelldocs.js`, so they work on static hosts. Arrow keys and Home / End move between tabs.
