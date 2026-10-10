# Changelog

All notable changes to ShellDocs land here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning is [SemVer](https://semver.org/spec/v2.0.0.html) with prerelease suffixes (`-alpha`, `-beta`, `-rc`) — the alpha window explicitly reserves the right to break APIs on minor bumps.

## [Unreleased]

### Added

- **Code variants on previews.** `<DemoPreview>`, `<ComponentPreview>` and `<IframePreview>` take `<CodeTab Label="…">` children with a code fence inside; the Code panel shows them as tabs instead of the Razor source, for libraries documented in several languages (MAUI and Avalonia XAML, say). The copy button copies the tab that's showing. `SyncKey` works as on `<CodeGroup>`: blocks with the same key switch together across pages and the choice is saved. A block without a tab for the current choice shows "Not available on {label} yet" and keeps its height. From `.razor`, pass `CodeTabs` (`PreviewCodeTab` records). Without code tabs nothing changes. `razor:preview` fences can't take code tabs: their body is Razor, so a fence can't nest in one.
- **Page switch.** `o.AddSyncSwitch("platform", "MAUI", "Avalonia")` adds underlined tabs, like shadcn/ui's library switch, to every page whose blocks use that key: under the page header, or right above the first such block. `<SyncSwitch SyncKey="…" [Options="a, b"] />` places one by hand.
- **Preview caption.** `Caption="…"` puts a short muted note in the preview toolbar; `ShellDocsOptions.PreviewCaption` sets a site-wide default.
- **`<IframePreview Src Height Lazy SyncKey Caption Title Id>`** shows another page (a live Avalonia app compiled to WebAssembly, for instance) in the Preview panel with the same toolbar and code tabs. `shelldocs.js` loads it when scrolled into view (`Lazy="visible"`, the default), on a **Run live preview** button (`click`) or at once (`none`), appends `?theme=light|dark`, and posts `{ type: "shelldocs-theme", theme }` to the page's origin when the theme changes. The Code tab is hidden when there's no code.
- XAML highlighting: `xaml` and `axaml` fences are highlighted as XML (`cs` as C#). The `init` template loads Shiki's `xml` grammar; existing sites add `'xml'` to the `langs` list in `App.razor`.

### Changed

- `SlotRenderer` no longer warns about child content for components that read their raw body (`ChildContentSource`).

### Fixed

- **One-line component bodies in prose were wrapped in `<p>`.** `<Badge>v0.3.2 stable</Badge>` rendered a paragraph inside the badge, so the page's paragraph margin and colour overrode it. As in MDX, a body on the same line as its tags now renders inline; a body on its own lines still becomes paragraphs.

## [0.1.12-alpha] — 2026-10-04

Parser fixes found while rewriting the ShellDocs docs site on 0.1.11: fences and component tags are now read the way CommonMark and Razor read them, so a page that shows its own markdown source no longer breaks what follows it.

### Added

- **Tilde fences.** `~~~` fenced blocks are recognised like backtick ones: tags inside them stay literal, and `~~~razor:preview` renders a preview.

### Fixed

- **A fence longer than three backticks that showed a shorter fence broke the rest of the page.** The slot extractor only knew three-backtick fences, so a four-backtick block containing a three-backtick fence was paired with the wrong closing line: the next `razor:preview` rendered as plain code, and component tags inside the example turned into empty placeholders. Fences now follow CommonMark: three or more backticks or tildes, closed by a run of the same character at least as long. An empty fence no longer pairs with a later one, and a closing fence may be longer than the opening one or have trailing spaces.
- **A `>` inside a quoted attribute value ended an inline component tag early**, so the component (a `<TypeRow>`, for example) silently disappeared. Inline tags are now read by the same scanner as `razor:preview`, so quoted values can hold `>`, another tag, or text that looks like an attribute (`Description='sets href="/"'`).
- **The same `>` problem in named-slot tags.** A child tag routed into a `RenderFragment` parameter (`<Icon title="a > b">`) was cut at the first `>`, leaking the rest of the tag into the slot. Named-slot tags use the shared scanner too.
- **Code spans in an attribute value could split the value** when the span contained a quote. Spans are restored per value after the tag is parsed.
- **`<TypeTable>` dropped rows that shared a `Name`** (overloads, or two rows describing one thing). Rows are now tracked by component instead of by name.

### Changed

- An inline tag with an unterminated quote is passed through as raw markup instead of being read up to the first `>`. A tag can't span a blank line.

## [0.1.11-alpha] — 2026-10-03

Polish from moving the ShellUI docs onto 0.1.10: the sidebar no longer repeats a folder's index page, and previews and tables behave on phones.

### Added

- **`scroll` preview layout.** `razor:preview scroll`, or `Layout="scroll"` on `<DemoPreview>` / `<ComponentPreview>`, lets an example wider than the frame (pagination, toolbars, OTP inputs) scroll inside it on small screens instead of spilling past the frame. Opt-in, since a scroll box clips popovers.

### Changed

- On phones the preview frame's side padding drops from 1.5rem to 0.75rem, and centring uses `safe center`, so an example that's still too wide starts at the left edge instead of losing both sides.

### Fixed

- **Sidebar listed a folder's `index.md` twice**: once as the section label and again as a page with the same title. The label now links to the index page (highlighted when it's the current page) and the page isn't repeated; in collapsible sections the chevron is its own toggle button.
- **Collapsed sidebar sections had no `aria-expanded`**: Blazor drops a `false` boolean attribute, so the toggle now writes `"true"` / `"false"`.
- **Wide markdown tables widened the page on phones.** Tables render inside a `.shelldocs-table` box that scrolls horizontally.

## [0.1.10-alpha] — 2026-10-02

Previews that look like the real thing (no page typography leaking in, Razor child content, stretch layout, generic components), redirects for folders, version roots and moved URLs, an optional frontmatter page header, and a theme that stays in sync with whatever flips it.

### Added

- **Preview layout.** `razor:preview stretch` (fence info string), or `Layout="stretch"` on `<DemoPreview>` / `<ComponentPreview>` / `PreviewFrame`, lets block-level components (charts, inputs, tables) fill the frame instead of shrinking to their content. `center` stays the default.
- **`not-prose` class.** Every `.shelldocs-prose` rule skips `.not-prose` subtrees. Preview frames carry it; add it to any element that should keep page typography out.
- **Generic components in markdown.** `RegisterComponentsFromAssembly` now registers generic components under their bare name (`<BarChart>`), and `razor:preview`, inline tags and `<ComponentPreview>` close them from a type-parameter attribute, as Razor does: `<BarChart TItem="SalesRow" />`. Type arguments take C# spellings (`int?`, `List<string>`, full or simple type names). Without one, `object` is used when the constraints allow it (with a warning); otherwise the component renders an inline error instead of breaking the page. A non-generic component with the same name keeps the tag. `<AutoTypeTable>` lists the type parameters.
- **Redirects.** URLs that aren't pages now redirect: content folders and version roots to their first page (`/docs` prefers unversioned pages, then the latest version), and unversioned URLs that exist in the latest version to that page (`/docs/cli/dev` → `/docs/v2.0/cli/dev`). `o.AddRedirect(from, to, permanent)` adds segment-aware prefix rules for moved content; computed redirects answer 302, rules 301. Middleware registered by `AddShellDocs` serves them before routing (so dotted version roots like `/docs/v0.2.1` work), and `shelldocs build` writes them as redirect pages. Opt out with `o.EnableRedirects = false`.
- **`ShellDocsOptions.RenderPageTitle`.** Renders frontmatter `title` as the page heading and `description` as a lead paragraph when the body doesn't start with its own `# Heading`. Off by default.

### Changed

- **`razor:preview` child content is parsed as Razor, not markdown.** Component bodies inside a fence (and `<ComponentPreview>` bodies) go through the same node parser as the fence itself: elements wrap nested components exactly as written and nothing is wrapped in `<p>`. Inline component tags in prose still take markdown bodies.
- **`ThemeToggle` works without a Blazor runtime.** Clicks are handled by `shelldocs.js` and both icons render, with CSS picking one from `<html class="dark">`.
- **`ShellDocs.Components` references the ASP.NET Core shared framework** (for the redirect middleware) instead of the `Microsoft.AspNetCore.Components.Web` package. It already read content from disk, so it was server-side in practice.

### Fixed

- **Prose styles leaked into previews.** Paragraph margins, list padding, heading sizes and link underlines from `.shelldocs-prose` hit live components and beat Tailwind's layered utilities (gaps inside cards and menus, underlined nav links).
- **Nested markup inside a preview component was mangled.** `<Navbar><div><a/>…<ThemeToggle/></div></Navbar>` closed the `<div>` before the nested component and wrapped loose text in `<p>`.
- **Theme state only followed ShellDocs' own toggle.** A component library flipping `<html class="dark">` left the stored theme and `ThemeState` stale. `shelldocs.js` now watches the class, saves it under `shelldocs-theme` and pushes it into `ThemeState`.
- **`shelldocs build` prerendered "Page not found" pages** when the csproj copied `meta.json` but not the `.md` files (`<Content Update="content/**/*.md">`). Build only mirrored `content/` when it was missing entirely; it now fills in every file publish left out. The example project's csproj is fixed too.
- **`enhancedload` handlers never ran.** Blazor raises the event through `Blazor.addEventListener`, not as a DOM event, so re-applying the theme, re-attaching the TOC and closing the mobile nav after enhanced navigation (static SSR apps) silently did nothing.

## [0.1.9-alpha] — 2026-10-02

A Preview | Code toolbar for every example, fixes for chrome that reset or ignored early clicks, consumer components no longer losing to built-ins with the same name, and code spans that stay code.

### Added

- **Preview toolbar.** `PreviewFrame` (razor:preview fences, `<DemoPreview>`, `<ComponentPreview>`) has a header with **Preview | Code** tabs, a copy button, and a **⋯ menu**: *Open in new tab* (links to the example's anchor), *Report a bug* and *Suggest something* (new-issue links pre-filled with the example name and page URL). Tabs follow the WAI-ARIA pattern, including arrow keys, Home and End. It replaces the "View Code" fade overlay.
- `ShellDocsOptions.IssueTrackerUrl`: where the issue links point. Defaults to `https://github.com/{GitHubRepo}/issues/new`; the items are hidden when neither is set.
- Stable example anchors: `preview-1`, `preview-2`, … in page order, `demo-{component}`, and `example-{component}-{hash}`. `DemoPreview` takes an optional `Id`.
- `Docs{Name}` aliases for every built-in primitive (`<DocsCard>`, `<DocsCallout>`, …), always resolving to ShellDocs' own component.
- `TypeRegistry.Collisions` (`TypeCollision(TagName, Replaced, Winner)`); collisions are logged at startup.
- `DocsHeader.MobileMenu`: the primary nav as a mobile menu. `HomeLayout` turns it on.
- **`<Tabs>` / `<Tab>` content primitive.** The tab strip and every panel are server-rendered and switched by `shelldocs.js`, so they work on static hosts. Supports `DefaultValue` (by `Label` or `Value`), `SyncKey` groups that switch together and persist in `localStorage` across pages, and the WAI-ARIA keyboard pattern. Available as `<DocsTabs>` / `<DocsTab>` when a consumer library shadows the names.

### Changed

- **Your components win name collisions with built-ins.** Built-ins are now registered before consumer components. Previously ShellDocs' `Card`, `Callout`, `Steps`, … silently replaced a consumer library's component of the same name (e.g. ShellUI's `Card`). The built-in stays reachable as `<DocsCard>`, and an informational log says so. Two consumer components claiming the same tag log a warning.
- **Version folders stay out of the sidebar outside a version.** On unversioned pages the sidebar used to list every version folder (`V0.3`, `V0.2.1`, …) as collapsible sections. The version selector already covers that navigation, so they are hidden there, along with sections that hold nothing but version folders. `DocsVersionResolver.IsHiddenInSidebar(node)`.
- `ComponentPreview` renders through `PreviewFrame`, so it gets the same toolbar. Its own `.component-preview-*` markup and CSS are gone.
- The hamburger is driven by `shelldocs.js` (`[data-mobile-open]` on the page shell) instead of `MobileNavState` + `@onclick`, so the mobile drawer also works on static hosts. The backdrop, Escape, or picking a link closes it. `MobileNavState` is still registered but no longer used by the built-in chrome.
- Documentation corrected to match the code:
  - README rewritten;
  - ARCHITECTURE rewritten to describe what's built;
  - ROADMAP marks what shipped;
  - DESIGN gets a status note;
  - the example site's Tabs, CodeBlock, TypeTable, ComponentPreview, Installation and Theming pages are fixed, and each primitive page has live previews.
- `shelldocs init` scaffolds packages at the CLI's own version (read from its assembly), instead of a hard-coded constant that had been stuck at `0.1.2-alpha`.

### Fixed

- **"View Code" sometimes didn't respond.** `shelldocs.js` attached its click handlers on `DOMContentLoaded`, which waits for module scripts such as the Shiki import, so early clicks on prerendered chrome were dropped. Handlers now attach as soon as the script runs.
- **Chrome state reset when the Blazor circuit started.** Interactive render replaces the prerendered DOM, so a tab picked (or a mobile menu opened) before then snapped back. The selected tab and the mobile-nav state are restored on the replacement nodes.
- **`<CodeGroup>` tabs didn't work on static hosts.** They switched through `@onclick` state. `CodeGroup` now uses the same `shelldocs.js` contract as `<Tabs>`, and `SyncKey` choices persist in `localStorage` across pages instead of per circuit. `CodeGroupSyncState` is still registered but no longer used.
- **Highlighted code went stale or threw `replaceChild` errors.** Shiki highlighting replaced Blazor-owned `<pre>` elements, so Blazor kept updating the detached originals. After client-side navigation the Code tab showed the previous page's source, and a `<pre>` already gone made the swap throw. The source `<pre>` now stays in place (hidden once highlighted) and Shiki renders into a JS-owned sibling. That output is re-rendered when the source text changes and dropped when its source goes away.
- **Inline code spans are no longer treated as components.** `` `<Button>` `` in prose stays literal code. Fences and code spans inside a component's child content are restored before that content renders, instead of leaking `SHELLDOCS_MASK_…` tokens.
- **Page content showed above the open mobile drawer.** The scroll lock set `overflow: hidden` on the page shell, which made the shell the sticky mobile bar's scroll container, so after scrolling the bar slid away from above the drawer. The lock now sits on `<html>`, and the mobile bar is exactly `--header-height` tall so the drawer and backdrop meet it with no gap.
- **Hamburger did nothing on pages without a sidebar** (e.g. the home page). It now opens the primary-nav menu.
- Example home: feature icons used the `--accent` surface token as their colour and were nearly invisible.

## [0.1.8-alpha] — 2026-10-02

Versioned docs, plus `razor:preview` fixes that let real component-library docs (ShellUI) preview what they actually write. Every new chrome interaction follows the 0.1.7 static-host pattern: server-rendered initial state, data attributes, and delegated handlers in `shelldocs.js`, with no `@onclick` state.

### Added

- **Docs versions.** `ShellDocsOptions.AddVersion(string id, string label, string rootUrl, string? description = null, bool latest = false)` stores `DocsVersion(Id, Label, RootUrl, Description, IsLatest)` records in `ShellDocsOptions.Versions`. The current version is the one whose `RootUrl` is a segment-aware prefix of the path, otherwise the latest, otherwise the first.
- **`DocsVersionResolver`** (registered singleton): current version, scoped sidebar nodes, version/package hrefs, search filtering, prev/next scope, breadcrumb filtering.
- **`<VersionSelector />`** chrome: rendered under `<PackageSelector />` in `DocsSidebar` (so it's in the mobile drawer too), and in `DocsHeader` for the `TopNav` layout (desktop; the sidebar copy covers mobile there). Hidden with fewer than 2 versions. Current label in the trigger, a "latest" badge, and a check on the selected version. Options are plain `<a href>`: same page in the target version → current package root in the target version → the target version's first page.
- **`{version}` token in `AddPackage` root URLs**, resolved to the current version's `Id`. Selected-package matching (longest prefix, now segment-aware) uses resolved URLs. Switching packages keeps the version; versioned package roots without an index page link to their first page.
- **`<DemoPreview Component="X" Title="…" />`** content primitive plus `ShellDocsOptions.DemoSourceRoot`. It renders the registered component inside the preview frame, and the source tab shows `{DemoSourceRoot}/**/X.razor` (cached, highlighted as razor). A missing component or file renders the red error panel.
- `UrlPath` helpers (`Normalize`, `IsUnder`, `RelativeTo`, `Combine`) and `NavigationGraph.GetPrevNext(node, inScope)`, `FindFolder(url)`, `FirstPageUnder(url)` in `ShellDocs.Core`.
- `PreviewSlot.Nodes` + `PreviewNode` / `PreviewTextNode` / `PreviewElementNode` / `PreviewComponentNode` in `ShellDocs.Markdown`.
- `PreviewFrame` content mode (`Content` / `Code` / `Title` / `Error` / `ErrorTitle`), `DocsHeader.ShowVersionSelector`.

### Changed

- **Version-scoped chrome.** Inside a version, `DocsSidebar` renders only that version folder's children, prev/next never crosses a version boundary, search shows the current version plus anything outside every version root, and the breadcrumb omits the version folder. Without versions configured, behavior is unchanged.
- `PackageSelector` and `VersionSelector` re-render on navigation themselves. Parameterless components weren't re-rendered by their parent, so the package selection could go stale after enhanced navigation.
- `shelldocs.js` dropdown delegation now covers `.pkg` and `.ver`. Picking an option or pressing Escape closes the menu.
- Inline component tags and preview tags keep full attribute names. `@bind-Value="v"` is no longer misread as a `Value` parameter.
- Parameter-name matching is case-insensitive, like Blazor's.
- **Icons come from [ShellIcons.Blazor](https://www.nuget.org/packages/ShellIcons.Blazor) 0.1.0-alpha** (new dependency of `ShellDocs.Components`) instead of inline `<svg>` markup. `SidebarIcons` maps titles to icon components. Brand marks (GitHub, X) and consumer-supplied raw icons (`DocsPackage.IconPath`, `NavMenuItem.IconSvg`, `Card.IconSvg`) are unchanged. Scoped CSS that styled icon `<svg>`s now uses `::deep`, so if you restyle those classes in your own scoped CSS, do the same.

### Fixed

- **`razor:preview` with several top-level siblings rendered only the first component.** All top-level content now renders in order inside the one frame: components, HTML wrappers like `<div class="flex gap-2">…</div>`, and text. Wrappers are real render-tree elements, so their children stay inside them under interactive re-renders. The "unknown component" error panel still appears when no registered component is found. `@code { }`, `@* *@` and `@using`-style lines are skipped for rendering but kept in the source view.
- **Enum attributes in Razor form.** `ButtonVariant.Destructive`, `@ButtonVariant.Destructive`, `@true`, `@42`, `@(…)`, and `[Flags]` values as `A | B` all coerce now.
- **Attributes that can't be coerced no longer crash the page.** `EventCallback`/`Action`/`Func` params (`OnClick="HandleClick"`), `@bind-*`, `@ref`, `@onclick`, parameters of unsupported types, values `Coerce` can't parse, unknown attributes on components without a catch-all, and child content on components without a `RenderFragment ChildContent` are skipped with a logged warning. The component still renders. `ComponentPreview` gets the same protection.
- Search results lagged one keystroke behind the input. The query now recalculates via `@bind:after`.
- `PreviewFrame` / `ComponentPreview` copy buttons showed the copy and check icons side by side. The check now appears only after a successful copy.
- `aria-selected` on selector options renders `"true"`/`"false"` instead of a bare or missing attribute.
- `DocsSidebar` now actually implements `IDisposable`, so its `LocationChanged` handler is released.

### Notes

- Version folder names with dots (`v0.2.1`) keep their dots in every URL, slug and prerender output path. A route declared as `/docs/{*Path:nonfile}` won't match a URL whose last segment contains a dot (`/docs/v0.2.1`). Pages under it are fine, and the selectors only link to a bare version root when it has an `index.md`.
- To ship `<DemoPreview>` sources, copy the `.razor` files with a `None` item. The Razor SDK drops `.razor` from publish even with `Content Update` metadata:
  `<None Include="Demos/**/*.razor" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />`

## [0.1.7-alpha] — 2026-09-06

Fixes the interactivity gap the `0.1.5-alpha` static-prerender pipeline opened up. Four chrome interactions (sidebar section expand/collapse, package selector dropdown, TableOfContents scroll-spy, PreviewFrame source view) were written as ordinary interactive Razor components with `@onclick` handlers that mutate `[Parameter] bool` state and re-render via `StateHasChanged()`. Prerendered HTML captured only the initial state; on a static host with no Blazor runtime, every one of those interactions was dead on the deployed site.

The `0.1.5-alpha` CHANGELOG glossed this as "SignalR-backed component state doesn't survive the static build — but a docs site doesn't need it." That was wrong. A docs site's primary navigation surface is the sidebar; if you can't expand a section, you can't reach the pages under it.

### Fixed

- **Sidebar section expand/collapse works on static-hosted builds.** `DocsSidebarNode.razor` no longer routes clicks through Blazor's `@onclick="Toggle"` + `_isOpen` state. Instead, `shelldocs.js` attaches a delegated `click` listener on `.sidebar-section-toggle` and flips `[data-open]` on the ancestor `.sidebar-section` and its `.sidebar-section-shell` child. The initial `[data-open]` value (from `OnParametersSet`'s active-path check) still comes from server rendering — so the ancestor of the current page pre-expands correctly on first paint. CSS unchanged; it already selected on `[data-open]` for both the chevron rotate and the grid-rows animation.
- **Package selector dropdown opens/closes on static-hosted builds.** `PackageSelector.razor` always renders the `.pkg-menu` now (previously conditional on `_open`); CSS hides it under `display: none` unless `.pkg[data-open="true"]`. Delegated JS handler on `.pkg-trigger` flips `[data-open]`; outside-click closes any open menu. Options are plain `<a href={RootUrl}>` — native navigation, no Blazor round-trip.
- **TableOfContents scroll-spy attaches on static-hosted builds.** Removed the `OnAfterRenderAsync` → `shelldocsToc.attach` invocation (which only fires with a live Blazor runtime). TOC list now emits `[data-toc-list]` + `[data-toc-ids="id1,id2,..."]`; `shelldocs.js` scans for these on `DOMContentLoaded` and after Blazor `enhancedload` and calls `shelldocsToc.attach` itself. Anchor click uses native `href="#id"` navigation.
- **`PreviewFrame` and `ComponentPreview` source-view expand/collapse work on static-hosted builds.** Removed `@onclick="Expand"` / `Collapse` / `Show` / `Hide` and the `_expanded` / `_showSource` state fields. Buttons carry `data-preview-toggle="expand|collapse"`; `shelldocs.js` toggles the same `.expanded` / `.collapsed` classes the Blazor state used to toggle. The copy button follows the same pattern (`[data-preview-copy]`).

### Notes

Every one of these interactions still works under a live Blazor runtime — the DOM emits the same initial state Blazor's rendering produced; JS mutations happen on top. Delegated document-level click listeners survive Blazor's enhanced-nav DOM swap without re-attaching; only the TOC scroll-spy needs re-init on `enhancedload` because heading IDs change per page.

### Removed

- `DocsSidebarNode.Toggle()` method — click handling is JS-side now.
- `PackageSelector._open` state, `Toggle()`, `Choose()`, `OnBlur()` — same reason; navigation is plain anchor.
- `TableOfContents._handle` / `_sig` / `Scroll()` and the `IJSObjectReference` / `IAsyncDisposable` machinery — scroll-spy attaches from JS, scroll-to uses native anchor.
- `PreviewFrame._expanded` / `_copied` / `_highlighted` / `_codeEl` / `Expand()` / `Collapse()` / `Copy()` — all JS-side.
- `ComponentPreview._showSource` / `_copied` / `_highlighted` / `_sourceEl` / `Show()` / `Hide()` / `Copy()` — same.

## [0.1.6-alpha] — 2026-08-22

Three authoring / SEO features that stack together to make writing per-component docs and shipping a public site substantially less manual.

### Added

- **`<AutoTypeTable Component="Name" />` primitive.** Renders a full props table for any registered component by reflecting on its `[Parameter]` properties. Reads type (compact C#-ish rendering including nullability and generic args), default (from `[DefaultValue]` or from instantiating the type and reading the property), required flag (from `[EditorRequired]`), and description (from XML doc `<summary>` on the property, loaded from the sidecar `<Assembly>.xml` next to the DLL). Replaces the "hand-copy every prop into `<TypeRow>`" pattern that every consumer was doing today. Existing `<TypeTable>` / `<TypeRow>` stay for cases where hand curation is preferable.
- **Named `RenderFragment` slots in `razor:preview` fences and inline component tags.** Direct-child tags whose name matches a target component's `[Parameter] RenderFragment` prop now route into that named slot instead of being flattened into `ChildContent`. Authors can finally write compositional previews:
  ```razor
  <Alert Title="Heads up">
    <Icon><svg>…</svg></Icon>
    Body text.
    <Footer>Small print.</Footer>
  </Alert>
  ```
  and have `<Icon>` render into `Alert.Icon`, `<Footer>` into `Alert.Footer`, and the leftover text into `Alert.ChildContent`. Unblocks previewing every ShellUI-shaped component that uses named slots (Alert, Card, Sheet, Drawer, Tabs, Accordion, Dialog).
- **`shelldocs build --site-url <https://example.com>` emits sitemap.xml + robots.txt + `og:*` meta tags on every prerendered page.** Sitemap enumerates every URL discovered by the prerender walk (visible + hidden). Robots allows all and points at the sitemap. Per-page `<meta property="og:title">` / `og:description` / `og:url` / `og:type` injected before `</head>` from the nav-graph node's title + description. All three artifacts are silently skipped when `--site-url` is unset — no accidental broken sitemaps in local dev builds.
- **`ShellDocsOptions.SiteUrl`** — declared symmetrically with the CLI flag for consumers who prefer configuration-side declaration (currently informational; `shelldocs build --site-url` remains the mechanism the CLI consumes).

### Changed

- **`SlotRenderer.BuildParameters`** now walks the target's `[Parameter]` props once to extract `RenderFragment` names before falling back to the pre-fix `ChildContent`-only behavior. Backwards compatible — no named-slot tags in child content → identical output to before.

### Notes

`XmlDocIndex` caches per-assembly load once; concurrent consumer requests share the parsed dictionary. XML docs are optional — a component whose assembly ships without an `.xml` file just renders em-dash descriptions.

## [0.1.5-alpha] — 2026-08-22

The `shelldocs build` output is now a real static site. Previously it copied `publish/wwwroot/` 1:1 — fine on paper, useless in practice for a Blazor Server scaffold, which produces no `index.html`, no client-side runtime, nothing a static host can serve at the root URL. Deploying the output to GH Pages / Cloudflare Pages / Netlify silently returned a blank shell. Now every URL the site knows about is prerendered at build time and the framework's static assets are merged on top.

### Fixed

- **`shelldocs build` produces a fully static site deployable to GH Pages / Cloudflare Pages / Netlify without a .NET host.** Previously the CLI ran `dotnet publish` and copied `wwwroot/` verbatim — which, for a Blazor Server scaffold (what `shelldocs init` produces by default), contains only static assets and no HTML entrypoint. Visiting the deployed root returned an empty page; deep links 404'd. New flow: publish → launch the published app on a loopback ephemeral port → walk `NavigationGraph.AllUrls` to enumerate every route (visible + hidden) plus `/` → `HttpClient.GetAsync` each URL → save the rendered HTML to `<output>/<url>/index.html`. Then merge `publish/wwwroot/` (`_content/`, `_framework/blazor.web.js`, `app.css`, `favicon.png`, tokens CSS) on top. Result is a directory any dumb file host can serve; every URL is a real HTML file with content in the response body. Server scaffold unchanged — the prerender happens entirely inside `shelldocs build`.
- **`--base-href` now rewrites `<base href>` in every prerendered HTML file, not just the root `index.html`.** The pre-fix pass only touched one file, so subpath deploys (GH user-repo pages at `/repo/`) worked for the home page but broke every deep-linked doc route (assets 404'd because the deep-linked pages still had `<base href="/">`). New pass walks `<output>/**/*.html` and rewrites every occurrence.

### Added

- **`NavigationGraph.AllUrls`** — public enumeration of every indexed URL (visible + hidden). Needed by the new prerender walk so hidden pages still land in the static output (they route at runtime and would 404 on the static site otherwise). Trivial addition, no API break.
- **`PrerenderRunner`** internal helper in `ShellDocs.CLI` — encapsulates the subprocess launch + readiness poll + URL walk + file save. Kept out of `BuildCommand` so the orchestration reads linearly. Guarantees subprocess cleanup on every exit path (finally + `ProcessExit` + `CancelKeyPress`).

### Known limitations

- **URL discovery is content-folder-based, not runtime-based.** `PrerenderRunner` walks URLs via `NavigationGraphBuilder.Build(contentRoot)` — same logic the running app uses — so file-based routes are complete. Custom routes registered outside `content/` (bespoke `@page` directives) won't be prerendered and will need to be added manually. Not a regression from `0.1.4-alpha` behavior (which prerendered nothing).
- **Interactive server components at request time.** The prerendered HTML is a snapshot of the initial render; hydration on the client picks up from there via `<script src="_framework/blazor.web.js">`. Deep-linking to a page hits the static HTML; the theme toggle, search, tabs, code copy work via the shipped `shelldocs.js` on top. Full Blazor Server interactivity (SignalR-backed component state) doesn't survive the static build — but a docs site doesn't need it.

## [0.1.4-alpha] — 2026-08-22

`0.1.3-alpha` pinned the sidebar footer to a fixed slot but only under the desktop Sidebar variant — footer still floated mid-sidebar on TopNav, mobile drawer, and short-tree cases. This release covers that plus three primitive-DX improvements.

### Fixed

- **Sidebar footer now pins to the bottom in every layout context.** The `0.1.3-alpha` fix only worked in the desktop Sidebar variant — the `flex: 1; min-height: 0` sizing that lets the nav fill its slot was scoped to `.docs-shell-sidebar .docs-sidebar-slot > nav` inside a `@media (min-width: 1024px)` block. On the TopNav variant, the mobile drawer, or any Sidebar site whose tree is shorter than the slot, the nav still collapsed to intrinsic content size and the footer (GitHub link + theme toggle) sat wherever the tree ended, mid-sidebar. Moved `flex: 1; min-height: 0` onto `.docs-sidebar` itself and made `.docs-sidebar-slot` `display: flex; flex-direction: column` in every context. Footer pins hard to the bottom regardless of variant / viewport / item count.
- **`razor:preview` fences with an unknown outer tag now render a visible error in the frame instead of falling back to a plain code block silently.** Previously `SlotExtractor.TryBuildPreviewSlot` returned `null` when the fence's first tag didn't resolve to a registered component; the fence rendered as regular fenced code with only a build-log warning. Authors chasing "why isn't my icon rendering" would hunt for a nonexistent component bug. Now the same case emits a `PreviewSlot` with `ComponentType = null` and an `Error` message; `PreviewFrame` renders a red-tinted error panel in the render region naming the unknown tag and pointing at `o.RegisterComponent<T>()` / `o.RegisterComponentsFromAssembly<TMarker>()`. Build-log warning still emitted. `PreviewSlot.ComponentType` is now nullable — technically a source-breaking change for callers pattern-matching on it, though external consumers of that type are ~none in the alpha window.

### Added

- **`RegisterComponentsFromAssembly<TMarker>(string namespacePrefix)` overload.** Registering only components under a specific namespace from a big assembly no longer needs a `Func<Type, bool>` — the common "register everything under my Components namespace" case reads as:
  ```csharp
  o.RegisterComponentsFromAssembly<Marker>("ShellIcons.Icons");
  ```
  instead of the lambda form. The `Func` overload stays for anything more complex.
- **`shelldocs init` scaffolded `Program.cs` now surfaces the `LayoutVariant` knob.** Commented-out `// o.LayoutVariant = DocsLayoutVariant.Sidebar;` line right in the `AddShellDocs(...)` block, plus the `RegisterComponentsFromAssembly` hint. First-time consumers no longer have to grep `ShellDocs.Components/Layouts/DocsLayout.razor` to discover the sidebar-variant option exists.

### Known limitations

Two items deferred to future releases; both need spec-level design rather than a patch:

- **`shelldocs build` produces no `index.html`, and `init`/`build` render-mode mismatch.** The scaffolded project is Server-interactive but `build` prints "publish kind: static (Blazor WASM)" and copies the resulting `wwwroot/` — which for a Server-interactive project has no `index.html`, no `_framework/dotnet.js`, no runtime blob. Output is unusable as a static site (blocks GH Pages / Cloudflare / Netlify deploys). Two viable paths (server-side prerender walk of the nav graph, or scaffold WASM Standalone by default) — either is a substantial change to `BuildCommand` and/or `InitCommand`. Consumers workaround: `dotnet run` locally, skip `shelldocs build`.
- **Markdig mangles inline HTML wrappers between component slots in `razor:preview`.** Plain `<span style="color:…">` around a registered component tag inside a preview loses its parent-child relationship after Markdig's inline pass, because SlotExtractor lifts component tags before Markdig sees them. Workaround: use registered wrapper components with their own attribute props instead of raw inline HTML. Framework fix would need SlotExtractor to lift-and-preserve trivial wrappers (`<span>`, `<a>`, `<button>`) around component tags.

## [0.1.3-alpha] — 2026-08-12

One sidebar-chrome bug + a broader icon vocabulary for real-world consumer sites.

### Fixed

- **`.docs-sidebar` no longer pushes its footer off-screen when the tree scrolls.** In `DocsLayoutVariant.Sidebar`, once the sidebar tree grew tall enough to need internal scrolling, the footer (GitHub link + theme toggle) disappeared below the visible area of the floating sidebar card. Root cause: `DocsSidebar.razor.css` set `height: 100%` on the nav, which in a flex-column parent resolves against the parent's full content box (header + nav) instead of the remaining space, overriding the `flex: 1; min-height: 0` sizing from `DocsLayout.razor.css`. Removed the `height: 100%` and added `min-height: 0` in its place — footer now stays pinned to the bottom of the slot regardless of tree depth.

### Added

- **Broader `SidebarIcons` coverage.** Hand-curated icon map grew from ~20 entries to ~50. New titles covered: `Authoring`, `CLI` (+ `Cli` alias for auto-title-cased folder names), `Packages`, `Configuration`, `Project Structure`, `Quick Start`, `Frontmatter`, `Fenced Code`, `Razor Preview`, `Inline Component Tags`, `Navigation`, plus PascalCase and space-separated variants of every content primitive (`CardGrid`/`Card Grid`, `LinkCard`/`Link Card`, `Steps`, `FileTree`/`File Tree`, `TypeTable`/`Type Table`, `CodeGroup`, `PreviewFrame`/`Preview Frame`, `ComponentPreview`/`Component Preview`) and the four CLI command names (`shelldocs init`/`add`/`dev`/`build` + bare `Init`/`Add`/`Dev`/`Build`). Closes the visual gap where categories a mature consumer's site actually uses rendered without an icon while the framework's own vocabulary had one. Longer-term a first-class icon package will replace this hand map.

## [0.1.2-alpha] — 2026-07-28

Dogfood-driven addition. Surfaced while building shelldocs.dev: the framework had no way to route to a page without also showing it in the sidebar. Fine for typical docs, blocker for landing pages reached via the sidebar package selector (they'd render redundantly in the sidebar tree AND be the dropdown target).

### Added

- **`meta.json` `hidden` array.** New optional field alongside `title` / `pages`. Slugs listed there route (URLs resolve, direct links + package-selector navigation work) but never appear in the sidebar tree. Takes precedence over `pages` — a slug listed in both stays hidden.
  ```json
  {
    "title": "Documentation",
    "pages": ["introduction", "getting-started"],
    "hidden": ["components", "cli", "markdown"]
  }
  ```
- **`NavigationGraph` constructor gains an optional `hiddenPages` parameter.** Hidden pages get indexed into the URL lookup but are excluded from `_flatPages` (so `GetPrevNext` skips them) and never appear as `Root.Children` (so sidebar tree and `Flatten()` skip them). Not intended for direct consumer use — `NavigationGraphBuilder.Build()` produces the collection during folder walking.

### Test coverage

Four new `NavigationGraphBuilderTests`: hidden slug excluded from sidebar but URL resolves, hidden folder excluded from sidebar but child URLs resolve, `hidden` takes precedence over `pages`, hidden slug excluded from auto-append.

## [0.1.1-alpha] — 2026-07-25

First point-release after the dogfood smoke of `0.1.0-alpha`. Three consumer-blocking fixes plus release-workflow hardening.

### Fixed

- **`NavigationGraphBuilder` now auto-includes `.md` files not referenced in `meta.json`.** Previously, when `meta.json` existed, ONLY the entries in its `pages` array made it into the nav — every other file on disk was silently dropped. `shelldocs add component Button` created `content/docs/components/button.md` on disk but the URL 404'd and the page never appeared in the sidebar until the consumer hand-edited `meta.json`. Fix: `meta.json` now controls ORDERING of explicitly-listed items; presence is driven by the file tree. Unreferenced files/folders get appended alphabetically after the explicit ordering. Backward-compatible — consumers who list everything explicitly get their exact ordering preserved verbatim before the auto-appended tail.
- **`shelldocs init` scaffold no longer emits a broken `<Callout Text=...>` example.** The intro-page template referenced a `Text` prop that doesn't exist on `<Callout>`; the current API is `Variant` + `Title` + `ChildContent`. Every new consumer running `dotnet run` on their fresh scaffold saw an empty callout as the first thing on their site. Template updated to `<Callout Variant="info" Title="Live component">body content</Callout>`.
- **`shelldocs init` now inserts a Content Update itemgroup so `dotnet publish` copies the markdown corpus.** Previously worked on `dotnet run` (resolves ContentRoot to source) but silently broke first deploy — the published output had zero markdown, so every `/docs/*` route 404'd. New `AddContentCopyIfMissing` helper adds `<Content Update="content/**/*.md;content/**/meta.json" CopyToOutputDirectory="PreserveNewest" />` to the consumer's csproj. Idempotent, runs in both CREATE and ATTACH modes.

### Hardened (release infrastructure)

- **Release workflow pre-push existence check.** New step queries `nuget.org/v3-flatcontainer` for each of the 6 package IDs at the tag's version before invoking `dotnet nuget push`. If any version already exists on nuget.org, the workflow **fails loud** with a "bump `Directory.Build.props` and re-tag" message. `--skip-duplicate` stays in the push step (still useful for resuming a workflow re-run that partially completed), but the pre-check catches the "you forgot to bump the version number" case explicitly instead of silently no-op'ing.

## [0.1.0-alpha] — 2026-07-25

First public release. The whole Phase 1 target is shipped, plus most of Phase 2's primitives + consumer DX polish. See [ROADMAP.md](docs/ROADMAP.md).

### Packages

Published to NuGet:

- `ShellDocs.CLI` — global tool: `dotnet tool install -g ShellDocs.CLI --prerelease`. Commands: `init`, `add`, `dev`, `build`, `preview`
- `ShellDocs.Components` — RCL with `<DocsLayout>`, `<DocsHeader>`, `<DocsSidebar>`, `<TableOfContents>`, `<PrevNextNav>`, `<DocsBreadcrumb>`, `<SearchDialog>`, content primitives, API-reference primitives
- `ShellDocs.Core` — navigation graph, search index model, routing helpers, markdown plain-text extractor
- `ShellDocs.Markdown` — Markdig pipeline with frontmatter, `razor:preview` fenced blocks, inline Razor component tags
- `ShellDocs.Templates` — starter markdown + Program.cs snippets for `shelldocs init` scaffolding
- `ShellDocs.Tokens` — RCL with `tokens.css` — shadcn-compatible palette + spacing scale, single source of truth for `--background`, `--foreground`, `--primary`, `--radius`, dark mode

### Added

**Markdown pipeline (`ShellDocs.Markdown`)**
- YAML frontmatter parsing via YamlDotNet
- ` ```razor:preview ` fenced blocks — live-rendered previews with source-view toggle
- Inline Razor component tags mid-markdown (`<Callout />`, `<Card ... />`)
- Component type registry (`RegisterComponent<T>()`) with per-type tag aliases (`RegisterComponent<Button>("Btn")`)
- Bulk `RegisterComponentsFromAssembly<TMarker>()` scan + `[ShellDocsIgnore]` opt-out attribute
- Automatic string→typed coercion for `bool`, `int`, `enum` attribute values

**Content primitives (`ShellDocs.Components`)**
- `<Callout Variant="info|warning|danger|tip">` — coloured info box with icon + title + body
- `<Card>` / `<CardGrid Columns="1|2|3">` / `<LinkCard>` — responsive card family
- `<Steps>` / `<Step>` — CSS-counter numbered list with badge-on-rail spine
- `<FileTree>` / `<FileTreeItem>` — recursive project-layout diagram
- `<CodeGroup SyncKey>` / `<CodeTab>` — tabbed code samples with cross-page sync

**API-reference primitives (`ShellDocs.Components`)**
- `<TypeTable>` / `<TypeRow Name Type Default Description Required>` — props/API reference table
- `<ComponentPreview Component="..." ...props>` — declarative-prop single-component demos

**Chrome (`ShellDocs.Components`)**
- `<DocsLayout>` with two variants (`TopNav`, `Sidebar` floating card)
- `<DocsHeader>` with primary nav mega-menu, GitHub link, theme toggle
- `<DocsSidebar>` with grouped nav, collapsible sections (animated grid-rows), auto-open on active path
- `<TableOfContents>` — right-rail, h2/h3 auto-extraction, scroll-spy indicator with smooth slide
- `<PrevNextNav>` — auto-derived from nav-graph adjacency
- `<DocsBreadcrumb>` — auto-generated from nav path; sections render as text, current page as `aria-current`, only leaf pages become links
- `<PackageSelector>` — consumer-configurable multi-package selector; hides when 0 or 1 packages declared
- `<BrandLogo>` — consumer-configurable logo with three modes: `LogoSvg` (inline SVG, tints via `currentColor`), `LogoLight`/`LogoDark` (theme-paired image URLs), or dot placeholder fallback
- `<SearchDialog>` — Cmd+K modal, client-side substring scoring against title / description / section / body, snippet extraction for body-only matches
- `<DocsFooter>` / `<DocsMobileBar>` / `<ThemeToggle>`

**Auto-chrome via `DocsPageState`**
- Consumer's docs page collapses to just `<MarkdownContent Document="_document" />` — TOC, PrevNext, Breadcrumb all auto-render from a shared scoped service
- Recomputes on `NavigationManager.LocationChanged`

**Search (`ShellDocs.Core`)**
- `SearchIndex.FromGraph()` — page + heading entries with URL, title, description, section
- Page entries carry extracted plain-text `Body` (frontmatter / fences / HTML / Razor tags / images / links / inline code / emphasis / heading `#` all stripped)
- `MarkdownPlainText.Extract()` — reusable helper for body extraction, 8KB default cap

**Code highlighting (`ShellDocs.Components`)**
- Shiki via WASM (bundle configurable)
- Dual-theme via `--shiki-light` / `--shiki-dark` CSS custom properties

**Design tokens (`ShellDocs.Tokens`)**
- Standalone RCL with `tokens.css` (base + full variants)
- Shadcn-compatible variable names for interop with ShellUI and other consumers

**CLI (`ShellDocs.CLI`)**
- `shelldocs init` — two modes: create (default, scaffolds a fresh Blazor Web App) and attach (`--attach`, augments existing project via `SHELLDOCS_SETUP.md`)
- `shelldocs add <component|guide|page> <name>` — scaffolds starter `.md` from template into `content/`
- `shelldocs dev` — dotnet watch with .md hot-reload
- `shelldocs build` — publishes static site, handles base-href rewrite + SPA 404 fallback

**Animation polish (Phase 2)**
- Native view-transitions API for cross-fade on route change (Chromium — silent no-op elsewhere)
- Sidebar section collapse animates via `grid-template-rows: 0fr → 1fr`
- Copy-icon success bounce
- Global `@media (prefers-reduced-motion: reduce)` guard — all animations collapse to instant

**Consumer configuration (`ShellDocsOptions`)**
- `RegisterComponentsFromAssembly<TMarker>(filter?)` — bulk-register a whole component library in one line
- `AddPackage(id, title, description, rootUrl, iconPath?)` — declares consumer's package family for the sidebar selector
- `SetLogo(url)` / `SetLogo(light, dark, alt?)` / `LogoSvg` — brand logo
- `AddNavLink` / `AddNavMenu` — top-nav wiring
- `LayoutVariant` — TopNav or Sidebar

### Known limitations

- Body-text search uses substring scoring, not an inverted index — fine for docs-sized corpora (~100 pages), will need rebuilding at 1000+
- Search snippets don't yet highlight the matched substring
- `<TypeTable>` is hand-authored today; XML-doc auto-generation ships in `ShellDocs.Xml` (Phase 4)
- No `<DocsBreadcrumb>` opt-out — currently hides when the trail has ≤ 1 node, otherwise always renders

[Unreleased]: https://github.com/shellui-dev/shelldocs/compare/v0.1.12-alpha...HEAD
[0.1.12-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.12-alpha
[0.1.11-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.11-alpha
[0.1.10-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.10-alpha
[0.1.9-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.9-alpha
[0.1.8-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.8-alpha
[0.1.7-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.7-alpha
[0.1.6-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.6-alpha
[0.1.5-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.5-alpha
[0.1.3-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.3-alpha
[0.1.2-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.2-alpha
[0.1.1-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.1-alpha
[0.1.0-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.0-alpha
