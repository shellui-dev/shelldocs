# Changelog

All notable changes to ShellDocs land here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning is [SemVer](https://semver.org/spec/v2.0.0.html) with prerelease suffixes (`-alpha`, `-beta`, `-rc`) — the alpha window explicitly reserves the right to break APIs on minor bumps.

## [Unreleased]

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

[Unreleased]: https://github.com/shellui-dev/shelldocs/compare/v0.1.1-alpha...HEAD
[0.1.1-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.1-alpha
[0.1.0-alpha]: https://github.com/shellui-dev/shelldocs/releases/tag/v0.1.0-alpha
