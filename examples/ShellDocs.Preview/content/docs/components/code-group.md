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

Pass `SyncKey` and every `<CodeGroup>` on the page with the same key switches together. Pick "pnpm" here — every other snippet with `SyncKey="pkg"` on the page will also read pnpm.

```razor:preview
<CodeGroup SyncKey="pkg">
    <CodeTab Label="npm">npm run build</CodeTab>
    <CodeTab Label="pnpm">pnpm build</CodeTab>
    <CodeTab Label="yarn">yarn build</CodeTab>
</CodeGroup>
```

## Notes

- The first `<CodeTab>` in source order is the default selection on first render (unless overridden by an active sync group).
- Choose stable sync keys — the sync state uses the key as its dictionary bucket, so renaming a key resets everyone's pick.
- Sync is per-circuit today. Cross-session persistence via `localStorage` lands in a follow-up.
