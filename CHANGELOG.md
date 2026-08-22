# Changelog

All notable changes to ShellDocs land here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning is [SemVer](https://semver.org/spec/v2.0.0.html) with prerelease suffixes (`-alpha`, `-beta`, `-rc`) — the alpha window explicitly reserves the right to break APIs on minor bumps.

## [Unreleased]

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

[Unreleased]: https://github.com/shellui-dev/shelldocs/compare/v0.1.2-alpha...HEAD
[0.1.2-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.2-alpha
[0.1.1-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.1-alpha
[0.1.0-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.0-alpha
