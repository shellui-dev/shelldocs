---
title: Tabs
description: Grouped, keyboard-navigable tabs for split content, code samples, and platform variants.
category: Components
order: 30
---

# Tabs

Tabs let you split content that belongs on the same page into discrete, keyboard-navigable panels. In ShellDocs they render as a client-rendered island — the initial pane hydrates instantly, and switching between tabs happens without a round trip to the server.

This page walks through every knob the `<Tabs>` component exposes: composition, sync groups, state persistence, code-tab shortcuts, keyboard behavior, accessibility, styling hooks, and the edge cases you'll probably hit within your first week of authoring docs.

## Overview

At the most basic level a tab group is a `<Tabs>` element wrapping two or more `<Tab>` elements. Each `<Tab>` takes a `Label` (the text shown in the strip) and its own body markup, which becomes the panel.

```razor
<Tabs>
    <Tab Label="Blazor">Server-side rendering plus a tiny hydration payload.</Tab>
    <Tab Label="MAUI">Native shell with the same components.</Tab>
    <Tab Label="Static">Pre-rendered HTML, no JS runtime at all.</Tab>
</Tabs>
```

The first `<Tab>` in source order is selected on first render. You can override that with `DefaultValue` on the parent, using either the label text or an explicit `Value` prop set on the tabs themselves.

### When to reach for tabs

Tabs are the right tool when you have parallel content that a reader will consume one option at a time — for example, install instructions for different package managers, or the same API called from different languages. They are the wrong tool when the content is complementary (all of it matters). If a reader needs to see every panel to understand the page, prefer sections or accordions.

A useful heuristic: if you'd feel comfortable hiding four of the five panels behind a click, tabs are fine. If you'd feel bad hiding any of them, don't use tabs.

### When to prefer a code group

For blocks that only differ in language or package manager (`npm` vs `pnpm` vs `yarn`), reach for the dedicated `<CodeGroup>` component instead. It shares the same underlying `<Tabs>` primitive but adds a tighter frame, a per-language icon, and — critically — a global sync group so switching one code sample switches every code sample on the page.

## Composition

The `<Tabs>` component is a slotted container. Its direct children must be `<Tab>` elements; anything else gets a build-time warning and is stripped at render. This keeps the DOM shape predictable and the keyboard model simple.

### Panels and slots

Each `<Tab>` renders two DOM nodes at runtime: a `<button role="tab">` inside the strip, and a `<div role="tabpanel">` inside the panel region. The panel is populated with whatever markup you put between the opening and closing `<Tab>` tags — that includes markdown, other components, and even nested tab groups (though nesting more than one level deep tends to confuse readers).

### Icons

Pass an `Icon` prop for a leading lucide-style icon in the tab button. The value is either the name of a built-in icon (`Icon="terminal"`) or a raw SVG path string (`IconPath="M4 4l16 16..."`). Icons render at 0.875rem to sit comfortably alongside 0.8125rem tab labels.

### Descriptions

For tab groups where the labels alone don't carry enough meaning, you can pass a `Description` prop to render a second line beneath the label. Use this sparingly — most tab groups don't need it, and adding it uniformly bloats the strip.

## Sync groups

The most useful feature of the tabs primitive is sync groups. Set the `SyncKey` prop on two or more `<Tabs>` blocks and they'll broadcast selection changes to each other. When a reader picks "pnpm" in one code group, every other code group on the page that shares the same key jumps to "pnpm" as well.

```razor
<Tabs SyncKey="package-manager">
    <Tab Label="npm">npm install shelldocs</Tab>
    <Tab Label="pnpm">pnpm add shelldocs</Tab>
    <Tab Label="yarn">yarn add shelldocs</Tab>
</Tabs>
```

The sync key is arbitrary; use whatever slug feels natural. `package-manager`, `runtime`, `os`, and `framework` are the common ones. Choose stable names — the key is also used as the localStorage bucket, so renaming it later will wipe reader preferences.

### Persistence

By default sync groups persist their selection across page loads via localStorage. Disable this with `PersistSelection="false"` on the parent if you want the group to reset on every visit. That's rarely what you want except for tabs that expose ephemeral state (e.g. "before / after" comparisons).

### Cross-page sync

Sync also travels across pages. A reader who picks "pnpm" on the install page will see "pnpm" pre-selected on every subsequent page that uses the same sync key. This is the single biggest ergonomic win of the pattern — it means readers only have to tell you their toolchain once.

### Sync + default value

If a `<Tabs>` block has both a `SyncKey` and a `DefaultValue`, the persisted selection wins on subsequent visits, and the default value wins on first visit. That's usually the right behavior, but if you're building a landing tour where you want to force a specific tab regardless of history, pass `ForceDefault="true"`.

## Keyboard behavior

Tabs follow the ARIA Authoring Practices tabs pattern. Focus a tab button and:

- **Arrow Left / Arrow Right** move focus between tabs in the strip and activate the newly-focused tab.
- **Home** jumps to the first tab; **End** jumps to the last.
- **Tab** moves focus out of the strip and into the currently-selected panel.
- **Enter** and **Space** are no-ops on a focused tab (arrow keys already activate).

### Focus scope

The panel is a focus scope. Once focus is inside a panel, arrow keys behave normally (they don't hijack navigation). To get back to the tab strip, press **Shift + Tab**.

### Skipping tabs

Tabs marked `Disabled="true"` are skipped by arrow navigation but remain in the tab order for screen readers. Their button gets `aria-disabled="true"` and a muted appearance.

## Accessibility

The strip is `role="tablist"`, each button is `role="tab"` with `aria-selected` and `aria-controls`, and each panel is `role="tabpanel"` with `aria-labelledby`. The IDs are generated at render time — you don't need to set them yourself, but you can override them via the `Id` prop if you need to target them from CSS or JS.

### Screen reader announcements

When a tab is activated, its panel becomes the ARIA "current" panel. Most screen readers announce the label + description on activation. Panels don't announce their content on activation — that would be too chatty — so the label and any tab description need to convey enough context on their own.

### Reduced motion

The panel-swap transition (a 120ms opacity fade) respects `prefers-reduced-motion`. Users who've asked for reduced motion see an instant swap.

## Styling

Every tab renders with a small set of class names you can override in your own CSS:

- `.shelldocs-tabs` on the outer container
- `.shelldocs-tab-strip` on the button row
- `.shelldocs-tab-button` on each button (with `[data-state=active]` when selected)
- `.shelldocs-tab-panel` on each panel

### Variants

Pass a `Variant` prop to switch between built-in appearances:

- `"pills"` (default) — rounded background under the active tab
- `"underline"` — bottom border under the active tab, no background
- `"card"` — the entire strip lives inside a bordered card

The default has been chosen to match the surrounding prose weight; the underline variant reads as more "documentation-nav-ish" and is a good pick for full-width code groups.

### Overrides

Every visual token uses CSS variables so you can restyle a single group without touching global CSS:

```css
.my-tabs {
    --tab-active-bg: var(--accent);
    --tab-active-color: var(--accent-foreground);
    --tab-transition: 200ms;
}
```

## Advanced

A few less-common flags exist for the edge cases that tend to surface once your docs site grows past ~30 pages.

### Lazy panels

By default all panels render eagerly — the DOM contains every panel, only the current one is visible. Pass `LazyPanels="true"` to mount panels on first activation instead. This is worth doing when panels are expensive (embed a chart, hit a live API, etc.) but not by default — eager rendering avoids layout jank when a reader switches quickly.

### Manual activation

If your tabs contain forms or other stateful widgets, you may want arrow-key navigation to *move focus* without activating the tab. Pass `ActivationMode="manual"` on the parent — the reader then presses **Enter** or **Space** to activate the focused tab. This matches the pattern used by GitHub for repo tabs.

### Controlled tabs

Pass `Value` + `OnValueChange` to run the tabs as a controlled component. This is useful when the current tab is part of a broader form state or is being driven by a URL fragment. The internal state machine still handles keyboard events; you just receive `OnValueChange` and are responsible for pushing the new value back down.

### URL fragment sync

Set `HashSync="true"` and the current tab writes itself to `location.hash`. Deep links pointing at `#pnpm` on a page will pre-select the pnpm tab. Combine with `SyncKey` to get link-shareable, cross-page-persistent tab state — the closest thing to routing without actually adding a route.

## Common pitfalls

A short list of things that trip up new authors.

### Too many tabs

More than five tabs in a strip almost always means the content should be reorganized. If you find yourself with seven package managers, ask whether the reader really needs that granularity or whether an "and others" catch-all is enough.

### Empty panels

If a panel is empty because the underlying feature doesn't exist in that variant, prefer to leave the tab out entirely rather than showing an empty state. An absent tab tells the reader "this doesn't apply here"; an empty tab tells them "we forgot to write this."

### Fragile labels

Sync keys are stable; labels are not. Never key off a tab label — always use `Value` when programmatically selecting a tab, and treat the label as user-facing copy that may change.

### Overusing sync

Sync is great for toolchain choices. It's bad for content variations that are unrelated between pages. If a reader picks "diagram" on one page and lands on a page where "diagram" doesn't mean anything, the sync silently no-ops — which is usually fine, but a reminder that sync keys should carry semantic weight.

## API reference

The complete parameter list for both `<Tabs>` and `<Tab>`, in the order they're most commonly used.

### `<Tabs>` parameters

- **DefaultValue** (`string?`) — the label or `Value` of the tab selected on first render.
- **Value** (`string?`) — controlled selection; pair with `OnValueChange`.
- **OnValueChange** (`EventCallback<string>`) — fired whenever the active tab changes.
- **SyncKey** (`string?`) — group key for cross-instance selection sync.
- **PersistSelection** (`bool`, default `true`) — write selection to localStorage.
- **ForceDefault** (`bool`, default `false`) — ignore persisted selection on first mount.
- **Variant** (`"pills" | "underline" | "card"`, default `"pills"`) — visual style.
- **ActivationMode** (`"automatic" | "manual"`, default `"automatic"`).
- **LazyPanels** (`bool`, default `false`) — mount panels on first activation.
- **HashSync** (`bool`, default `false`) — mirror selection to `location.hash`.
- **Class** (`string?`) — extra classes on the outer container.

### `<Tab>` parameters

- **Label** (`string`) — text shown in the strip.
- **Value** (`string?`) — stable identifier; falls back to `Label`.
- **Description** (`string?`) — optional second line in the strip.
- **Icon** (`string?`) — built-in icon name (e.g. `"terminal"`).
- **IconPath** (`string?`) — raw SVG `d` string, overrides `Icon`.
- **Disabled** (`bool`, default `false`).
- **Id** (`string?`) — override the auto-generated ID.

## What's next

If you've made it this far, you probably have a good enough mental model to start using tabs. A few natural follow-ups:

- **Code Group** — the tabs primitive dressed up for code samples with copy buttons and language icons.
- **Accordion** — when you want the same "one section at a time" affordance but need multiple sections to be openable independently.
- **Steps** — vertically-stacked, always-open sections numbered for tutorials.

Tabs pair well with sync groups and the code-block component — the three together cover most of the "show me how in my toolchain" flow that a docs site needs.
