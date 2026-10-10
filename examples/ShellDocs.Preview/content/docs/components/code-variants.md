---
title: Code variants
description: Show one live preview with code for several platforms, switched site-wide.
category: Components
order: 70
---

# Code variants

A preview can show code for several targets instead of the Razor that rendered it. Each target is a `<CodeTab>` with a code fence inside. That's how a cross-platform library documents one example in MAUI and Avalonia XAML, like shadcn/ui's code tabs for Radix and Base UI.

<DemoPreview Component="ButtonClickDemo" SyncKey="platform" Caption="Rendered with Blazor">
<CodeTab Label="MAUI">
```xml
<Button Text="Clicked 0 times"
        Clicked="OnClicked" />
```
</CodeTab>
<CodeTab Label="Avalonia">
```xml
<Button Content="Clicked 0 times"
        Click="OnClick" />
```
</CodeTab>
</DemoPreview>

Written as:

````markdown
<DemoPreview Component="ButtonClickDemo" SyncKey="platform" Caption="Rendered with Blazor">
<CodeTab Label="MAUI">
```xml
<Button Text="Clicked 0 times" Clicked="OnClicked" />
```
</CodeTab>
<CodeTab Label="Avalonia">
```xml
<Button Content="Clicked 0 times" Click="OnClick" />
```
</CodeTab>
</DemoPreview>
````

- Each tab's code is its first fence; the fence's language drives highlighting (`xml`, `csharp`, …). `xaml` and `axaml` are highlighted as XML. Without a fence, the tag's text is the code and `Language="…"` sets the language.
- `SyncKey` works like it does on [`<CodeGroup>`](/docs/components/code-group): every block with the same key switches together, on this page and later ones, and the choice is remembered.
- The **Copy** button copies the tab that's showing.
- Without `<CodeTab>` children, the Code panel shows the Razor source as before.
- `<ComponentPreview>` takes `<CodeTab>` children too; they're removed from the component's own child content. `razor:preview` fences can't: their body is Razor, so a code fence can't sit inside one. Use `<DemoPreview>` there.
- From a `.razor` file, pass the tabs directly: `CodeTabs="@(new PreviewCodeTab[] { new("MAUI", xaml, "xml") })"`.

## A missing variant

When a block has no tab for the current choice, it says so and keeps its height, instead of jumping to another tab. Choose **Avalonia** above, then look at this block:

<DemoPreview Component="ButtonClickDemo" Id="maui-only" SyncKey="platform">
<CodeTab Label="MAUI">
```xml
<Button Text="Only documented for MAUI so far" />
```
</CodeTab>
</DemoPreview>

## Page switch

`AddSyncSwitch` adds a row of tabs to every page whose blocks use that key, like shadcn/ui's library switch. It sits under the page header (frontmatter `title` with `RenderPageTitle`), or right above the first block that uses the key. The **MAUI | Avalonia** tabs above the first example on this page come from it:

```csharp
builder.Services.AddShellDocs(o =>
{
    o.AddSyncSwitch("platform", "MAUI", "Avalonia");
});
```

The first option is the default until a reader picks one, so every block starts on the same platform. To place it yourself, write `<SyncSwitch SyncKey="platform" />` (with `Options="MAUI, Avalonia"` if the key has no `AddSyncSwitch`); the automatic one is then left out.

## Caption

`Caption="…"` adds a short muted note to the preview's toolbar, such as which platform rendered it. `ShellDocsOptions.PreviewCaption` sets a default for every preview on the site.

## Live iframe preview

`<IframePreview>` shows another page in the Preview panel, with the same toolbar and code tabs. It's meant for live examples built with something other than Blazor, such as Avalonia compiled to WebAssembly.

<IframePreview Src="/preview/index.html?c=button" Height="240" SyncKey="platform" Caption="Live preview">
<CodeTab Label="MAUI">
```xml
<Button Text="Clicked 0 times" Clicked="OnClicked" />
```
</CodeTab>
<CodeTab Label="Avalonia">
```xml
<Button Content="Clicked 0 times" Click="OnClick" />
```
</CodeTab>
</IframePreview>

With `Lazy="click"`, nothing loads until the reader asks:

<IframePreview Src="/preview/index.html?c=card" Height="240" Lazy="click" Id="iframe-click" />

| Attribute | Default | Meaning |
|---|---|---|
| `Src` | (required) | Page to show. |
| `Height` | `320` | Pixels, or any CSS length (`24rem`). |
| `Lazy` | `visible` | `visible` loads when scrolled into view, `click` shows a **Run live preview** button, `none` loads at once. |
| `SyncKey`, `Caption`, `Title`, `Id` | | As on `<DemoPreview>`. |

The page in the iframe gets:

- `?theme=light` or `?theme=dark` on its URL, matching the site;
- a `{ type: "shelldocs-theme", theme: "light" | "dark" }` message whenever the reader switches theme, sent to the page's own origin.

```js
window.addEventListener('message', e => {
    if (e.data?.type === 'shelldocs-theme') applyTheme(e.data.theme);
});
```

Each iframe starts its own app, so lazy loading keeps a page to the previews the reader can see.
