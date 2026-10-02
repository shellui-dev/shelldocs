---
title: Tabs
description: Keyboard-navigable tabs for parallel content, with page-wide sync groups.
category: Components
order: 30
---

# Tabs

`<Tabs>` splits parallel content — the same instructions for different platforms, the same API from different languages — into panels the reader switches between. The strip and every panel are rendered on the server, and `shelldocs.js` does the switching, so tabs work on static hosts too.

## Basic

Wrap two or more `<Tab>` elements. `Label` is the text in the strip; the tag body is the panel, and can hold markdown or other components.

```razor:preview
<Tabs>
    <Tab Label="Blazor">Server-side rendering plus a small hydration payload.</Tab>
    <Tab Label="MAUI">A native shell hosting the same components.</Tab>
    <Tab Label="Static">Prerendered HTML served from any static host.</Tab>
</Tabs>
```

## Default tab

The first tab is selected unless `DefaultValue` names another one, by `Label` or by `Value`.

```razor:preview
<Tabs DefaultValue="linux">
    <Tab Label="Windows" Value="windows">winget install Microsoft.DotNet.SDK.10</Tab>
    <Tab Label="macOS" Value="macos">brew install --cask dotnet-sdk</Tab>
    <Tab Label="Linux" Value="linux">sudo apt-get install -y dotnet-sdk-10.0</Tab>
</Tabs>
```

## Sync groups

Give several `<Tabs>` the same `SyncKey` and picking a tab in one switches all of them. The choice is saved in `localStorage`, so it also applies on other pages and on the next visit. Pick a package manager in either group below:

```razor:preview
<Tabs SyncKey="package-manager">
    <Tab Label="npm">npm install shelldocs</Tab>
    <Tab Label="pnpm">pnpm add shelldocs</Tab>
    <Tab Label="yarn">yarn add shelldocs</Tab>
</Tabs>
<Tabs SyncKey="package-manager">
    <Tab Label="npm">npm run build</Tab>
    <Tab Label="pnpm">pnpm build</Tab>
    <Tab Label="yarn">yarn build</Tab>
</Tabs>
```

Use stable keys (`package-manager`, `os`, `language`): the key names the saved preference, so renaming it resets readers' choice. Sync matches tabs by `Value` (or `Label` when there's no `Value`), so give tabs that mean the same thing the same value across groups.

For code samples, [`<CodeGroup>`](/docs/components/code-group) is the same mechanism with a code-block frame.

## Keyboard and accessibility

The strip is a `role="tablist"`; each button is a `role="tab"` with `aria-selected` and `aria-controls`, and each panel is a `role="tabpanel"` labelled by its tab. Only the selected tab is in the tab order. With a tab focused:

- **Arrow Left / Arrow Right** move to the previous / next tab and select it (wrapping around).
- **Home / End** jump to the first / last tab.
- **Tab** moves on to the content after the strip.

## Parameters

### `<Tabs>`

- **DefaultValue** (`string?`) — `Label` or `Value` of the tab selected on first render. Defaults to the first tab.
- **SyncKey** (`string?`) — groups with the same key switch together and remember the choice in `localStorage`.
- **Class** (`string?`) — extra classes on the outer element.

### `<Tab>`

- **Label** (`string`, required) — text shown in the strip.
- **Value** (`string?`) — stable identifier used by `DefaultValue` and sync; falls back to `Label`.

## Styling

The parts carry stable classes you can target from your own CSS: `.sd-tabs` (outer element, with `[data-tabs-value]` set to the selected value), `.sd-tabs-list` (strip), `.sd-tabs-trigger` (each button, `[aria-selected="true"]` when selected) and `.sd-tabs-panel`.

If your component library also registers a `Tabs`, `<Tabs>` renders yours; this one stays available as `<DocsTabs>` / `<DocsTab>`.
