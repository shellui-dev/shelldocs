# ShellDocs Roadmap

Branch-by-branch implementation plan. Living doc — updated as branches ship.

See [DESIGN.md](DESIGN.md) for the high-level design and [ARCHITECTURE.md](ARCHITECTURE.md) for technical details.

---

## Release timeline

| Version | Target | Theme |
|---|---|---|
| `0.1.0-alpha` | Phase 1 end | Core framework: markdown pipeline + navigation graph + minimal chrome + CLI + one theme |
| `0.2.0-alpha` | Phase 2 end | Primitive completeness: search, TOC, nav, code tabs, TypeTable, animations |
| `0.3.0-alpha` | Phase 3 end | Hardened through building shellui.dev; consumer-driven fixes |
| `0.4.0-beta` | Phase 4 end | ShellDocs' own docs (shelldocs.dev) shipped; API stabilization |
| `1.0.0-rc` | Phase 4 end | Public release candidate |
| `1.0.0` | Phase 5 | Stable |

---

## Package family — what each branch ships to

| Package | Purpose |
|---|---|
| `ShellDocs.Core` | Navigation graph, search index model, routing helpers |
| `ShellDocs.Markdown` | Markdig pipeline + frontmatter + Razor component embedding |
| `ShellDocs.Components` | RCL — UI primitives (DocsLayout, CodeBlock, SearchDialog, etc.) |
| `ShellDocs.Tokens` | Shared CSS variable definitions — the palette + spacing scale that both ShellDocs and ShellUI-in-docs consume. Single source of truth for `--background`, `--foreground`, `--primary`, `--radius`, etc. |
| `ShellDocs.CLI` | `shelldocs init`, `shelldocs new`, `shelldocs dev`, `shelldocs build` |
| `ShellDocs.Templates` | Content for CLI scaffolding — starter markdown, meta.json, .csproj patches |
| `ShellDocs.Xml` | v2 — extract `<TypeTable>` from XML doc comments |

Each branch below annotates which package it touches. Multi-package branches call out cross-package changes.

---

## Phase 1 — Core framework (~4–6 weeks)

Goal: a consumer can `shelldocs init` an empty Blazor WASM project and get a working docs site with sidebar, header, code blocks, and one theme.

### ✅ `chore/repo-scaffolding` — shipped
Bootstrap the monorepo.

- `.slnx` (.NET 10 XML solution format) with all package projects + tests
- `.csproj` files with correct `TargetFramework`, `IsPackable`, `PackageId`
- `.gitignore`, `Directory.Build.props`, `Directory.Packages.props` for central package management
- `.github/workflows/ci.yml` — build + test on every push
- `.github/workflows/release.yml` — pack + push to NuGet on tag
- Nothing shipped; groundwork only

### ✅ `feat/core-navigation-graph` — shipped
Ships to `ShellDocs.Core`.

- `NavigationGraph` — tree of `NavigationNode { Url, Title, Description, Category, Order, Headings, Path }`
- `NavigationGraphBuilder` — walks a content root, reads frontmatter, builds the tree
- `meta.json` reader — folder-level sidebar ordering + section dividers
- Route resolution — `NavigationGraph.ResolveByUrl("/docs/button")` → node
- Unit tests: markdown → tree, meta.json ordering, deep-nested folders

### ✅ `feat/markdown-pipeline` — shipped
Ships to `ShellDocs.Markdown`.

- Markdig extension for YAML frontmatter (YamlDotNet)
- Markdig extension for `razor:preview` fenced blocks — emits placeholder that renderer swaps for `<DocsTabs>` component
- Markdig extension for inline Razor tags (`<Button />` mid-markdown)
- `MarkdownRenderer` component — takes a `.md` file path, returns rendered `MarkupString` + list of embedded component slots
- Type registry — `RegisterComponent<T>()` API for consumer to expose their components to inline tags
- Unit tests: frontmatter parsing, fenced block replacement, tag resolution, unknown-tag graceful degradation

### ✅ `feat/components-shell` — shipped
Ships to `ShellDocs.Components`.

- `DocsLayout` — full-page grid: header + sidebar + main + TOC + footer, fumadocs-shaped
- `DocsHeader` — logo, primary nav with hover mega-menu (icon cards), search-button placeholder, theme toggle, GitHub link, hamburger for mobile
- `DocsSidebar` — grouped nav from navigation graph, collapsible sections (fumadocs pattern — closed by default, active-path auto-open), lucide-style icons per section/page, package selector (ShellDocs · Markdown · Core · CLI · Components), footer bar with GitHub + theme toggle
- `MarkdownContent` — renders a doc page from a `.md` path via `MarkdownRenderer`
- `TableOfContents` — right-rail nav (h2/h3), scroll-tracked via multi-active headings so the thumb slides smoothly, SVG-mask + coloured thumb pattern lifted from fumadocs' `ClerkTOCItems`
- `PrevNextNav` — bordered cards, arrow icon square, translate-on-hover
- `MobileNavState` service + fixed drawer + backdrop + auto-close on route change
- Prism.js syntax highlighting (via CDN for now — Shiki lands in the next branch)
- Neutral fumadocs-shaped palette (`--background`, `--foreground`, `--primary`, `--muted`, `--accent`, `--border`) — will move to `ShellDocs.Tokens` in the next branch

### ✅ `feat/design-tokens` — shipped
Ships to new package `ShellDocs.Tokens`.

Extracts the palette + scale from `ShellDocs.Components/wwwroot/shelldocs-theme.css` into its own package so ShellUI (and any third-party consumer) can depend on the *tokens* without pulling in the whole components RCL.

- New `ShellDocs.Tokens` project — RCL that ships a single `wwwroot/tokens.css` with `:root` + `:root.dark` variable definitions
- `ShellDocs.Components` and `ShellDocs.Preview` update their `App.razor` link to `_content/ShellDocs.Tokens/tokens.css` and remove the inline theme file
- Add a `tokens-full.css` variant for consumers who want the extended set (semantic + chart colors), and a `tokens-base.css` for consumers who only want the core palette
- Document the token contract in `docs/TOKENS.md`: which names are stable, which are internal, and how to override
- **ShellUI integration path (Tailwind install):** ShellUI's Tailwind config reads the same `--primary`, `--background`, `--border` etc. — nothing changes on their side. Consumer just references `tokens.css` and both design systems light up together.
- **ShellUI integration path (NuGet install):** ShellUI's RCL detects `ShellDocs.Tokens` at runtime and skips emitting its own token file. Deferred to Phase 3 — needs a small opt-in flag on `AddShellUI()`.
- Unit tests: token file emits, dark-mode class toggling, no duplicate declarations across bundles

### ✅ `feat/codeblock-shiki` — shipped
Ships to `ShellDocs.Components`.

- `CodeBlock` component — takes `Language`, `Code`, `Filename`, `HighlightLines`
- Shiki via WASM (bundle configurable — full theme set is ~2MB, subset ~200KB)
- Copy button (uses `Shell.Cn` + ShellUI's clipboard interop pattern)
- Filename tab (renders as pill above the block)
- Line-highlight styling via CSS
- Handles `razor:preview` blocks — code visible in Preview + Code tabs (`<DocsTabs>` primitive comes in Phase 2)

### ✅ `feat/cli-init` — shipped

### ✅ `feat/cli-init-create-mode` — shipped
Split `shelldocs init` into two modes:
- **create** (default) — runs `dotnet new blazor` in `docs/<CwdName>.Docs`, then directly patches Program.cs + App.razor + adds packages + drops content/DocsPage.razor. One-command scaffold.
- **attach** (`--attach`) — the old behaviour; augments an existing csproj and emits `SHELLDOCS_SETUP.md` for manual patching (safer for projects with custom auth/middleware).
Both idempotent. Tests: 12 (4 attach + 4 patcher + 4 fixture-verified).
Ships to `ShellDocs.CLI` + `ShellDocs.Templates`.

- `shelldocs init` — detects Blazor WASM project, adds package references, generates `content/`, `Layout/DocsLayout.razor`, patches `Program.cs` to register services, writes default `meta.json`
- Non-interactive mode (`--yes`) — defaults for CI
- Idempotent — running twice is a no-op
- Templates for `Program.cs` snippets, starter `.md` content, `meta.json` skeleton
- Similar structure to `ShellUI.CLI` from ShellUI project

### ✅ `feat/cli-dev-build` — shipped
Ships to `ShellDocs.CLI`.

- `shelldocs dev` — starts `dotnet watch run` with markdown file watcher, hot-reload triggers navigation graph rebuild on `.md` change
- `shelldocs build` — runs `dotnet publish`, then post-processes: base-href rewrite, SPA 404 fallback, search index generation (search stub for now)
- Configurable output directory
- Reused between hosts (GH Pages, Vercel, Netlify, static)

### `chore/release-0.1.0-alpha`
- Version bump across all packages
- Release notes
- Push to NuGet
- Announce internally; no external marketing yet

**Milestone:** A consumer can spin up a docs site with sidebar + header + markdown-driven content + one theme. Search + TOC + rich primitives still missing.

---

## Phase 2 — Primitive completeness (~2–4 weeks)

Goal: everything from DESIGN.md's primitives table shipped. Real docs sites become viable.

### `feat/search-primitives`
Ships to `ShellDocs.Core` + `ShellDocs.Components`.

- `SearchIndexBuilder` in `ShellDocs.Core` — walks nav graph, emits `search-index.json`
- Wired into `shelldocs build` — index emitted alongside published site
- `SearchDialog` component — Cmd+K modal, composes ShellUI's `<CommandPalette>`, fetches index, client-side fuzzy match (Fuse.js-equivalent — probably home-grown, ~200 LOC)
- Header search button opens the dialog
- Keyboard nav in dialog (up/down/enter/esc)

### `feat/toc-primitive`
Ships to `ShellDocs.Components`.

- `TableOfContents` — right-rail nav, generated from `<h2>` and `<h3>` in current page
- Scroll-spy via `IntersectionObserver` (JS interop)
- Smooth-scroll on click
- Auto-hides on mobile / narrow screens

### `feat/nav-primitives`
Ships to `ShellDocs.Components`.

- `PrevNextNav` — auto-derived from nav graph adjacency, rendered at page bottom
- `DocsBreadcrumb` — composes ShellUI's `<Breadcrumb>` with docs presets

### ✅ `feat/content-primitives` — shipped
Ships to `ShellDocs.Components`.

- `<CodeGroup>` / `<CodeTab>` — multi-tab code containers with cross-page `SyncKey` sync (`npm` / `pnpm` / `yarn`, etc.)
- `<Callout>` (Info / Warning / Danger / Tip) with per-variant icon
- `<Card>` / `<CardGrid>` / `<LinkCard>` — responsive card grid + anchor-shaped link card
- `<FileTree>` / `<FileTreeItem>` — recursive project-layout diagram with `IsFolder`, `Highlight`, `Comment`
- `<Steps>` / `<Step>` — CSS-counter numbered ordered list with a badge-on-rail spine
- Preview-frame overhaul: dropped tabs for a fumadocs-style stacked preview + collapsed code teaser with "View Code" expand — both panels stay mounted, killing the whole class of tab-switch state loss
- `SlotRenderer` gains recursive nested-markup rendering (`ChildContentRaw` threading, `Dedent` for Markdig 4-space-indent trap) and per-property type coercion for `bool` / `int` / enum attribute values

### ✅ `feat/api-reference-primitives` — shipped
Ships to `ShellDocs.Components`.

- `<TypeTable>` / `<TypeRow Name Type Default Description Required />` — hand-authored props reference table via `CascadingValue` registration
- `<ComponentPreview Component="Foo" ...props>` — declarative-prop cousin of `razor:preview`; resolves target by name through `TypeRegistry`, forwards attrs via `CaptureUnmatchedValues` with the same per-type coercion `SlotRenderer` uses, reconstructs source view from the resolved prop dict (self-closing form when no body)
- `SlotRenderer.Coerce` + `GetParameterProps` bumped to `internal` so `ComponentPreview` can drive the same conversion path

### ✅ `feat/consumer-registration-dx` — shipped
Ships to `ShellDocs.Components` + `ShellDocs.Templates` + `ShellDocs.CLI` + `ShellDocs.Markdown`.

**Registration (`ShellDocs.Components`)**
- `ShellDocsOptions.RegisterComponentsFromAssembly<TMarker>()` — assembly-scan overload that walks the marker's assembly for public, concrete, non-generic `ComponentBase` subclasses and registers each. Kills the "hand-type `RegisterComponent<T>()` for every ShellUI component" tax for consumers.
- `RegisterComponentsFromAssembly(Assembly, Func<Type, bool>?)` — explicit form with a filter predicate for finer control (namespace narrowing, opt-in subsets, etc.)
- `[ShellDocsIgnore]` attribute — opt-out marker for public components that shouldn't be reachable from markdown authoring (e.g. render-machinery components that live in the same assembly)
- `RegisterComponent(Type)` runtime overload alongside the existing generic form
- `RegisterComponent<T>(string tagName)` + `RegisterComponent(Type, string tagName)` — alias overloads that expose a component under a different markdown-facing tag (e.g. `<Btn>` for `ShellUI.Button`); backed by a per-type `ComponentAliases` dictionary that `BuildTypeRegistry` consults before falling back to `type.Name`
- **Dogfooded on ourselves:** `AddShellDocs` now scans `ShellDocs.Components.Content` via this API instead of the old explicit-list `RegisterComponent<Callout>(); .RegisterComponent<Card>(); …` block, so a new primitive dropped under `Content/` auto-appears without a maintainer edit to `ServiceCollectionExtensions.cs`. `MarkdownContent` and `PreviewFrame` opt out via `[ShellDocsIgnore]`.

**Content scaffolding (`ShellDocs.Templates` + `ShellDocs.CLI`)**
- `shelldocs add <template> <name> [--dir] [--force]` — CLI command that scaffolds a starter `.md` page from a template into `content/`. Templates:
  - `component <Name>` → `content/docs/components/<slug>.md` — frontmatter + intro + `razor:preview` block + empty `<TypeTable>` + Notes
  - `guide <slug>` → `content/docs/guides/<slug>.md` — frontmatter + intro + `<Steps>` skeleton
  - `page <slug>` → `content/docs/<slug>.md` — blank frontmatter + H1
- Slugifies PascalCase inputs (`MyBigCard` → `my-big-card.md`) and TitleCases kebab inputs (`getting-started` → "Getting Started"). Refuses to overwrite unless `--force`.
- `PageTemplates` static class in `ShellDocs.Templates` holds the three template bodies — same access pattern as the existing `StarterPageTemplate`.
- Replaces the placeholder `new` command stub in `Program.cs`.

**Authoring fix (`ShellDocs.Markdown`)**
- `SlotExtractor.ReplaceComponentTags` no longer `.Trim()`s the raw child content of inline component tags. The Trim was stripping the first line's indent and defeating `SlotRenderer.Dedent` — Markdig then interpreted the remaining 4-space-indented lines as an indented code block. Symptom was the same "literal `<pre>` around placeholder divs" bug that had already been fixed for `razor:preview` fences; the inline-tag code path was still hitting it.

### `feat/animation-polish`
Ships to `ShellDocs.Components`.

- Page transitions: fade + slight translate on route change (150ms)
- Sidebar section collapse: height animation, chevron rotation (200ms)
- TOC scroll-spy: active indicator slides between items (spring-based)
- Search dialog: scale + fade in from center (100ms)
- CodeBlock copy button: checkmark morph on success (150ms + 1s hold)
- Mobile drawer: slide from left with backdrop blur
- `prefers-reduced-motion` respected everywhere
- View-transitions API where supported, Motion One fallback

### `chore/release-0.2.0-alpha`
- Version bump, notes, NuGet push
- Ready for dogfood via shellui.dev

**Milestone:** ShellDocs is feature-complete for a full-featured docs site. Real content authoring can begin. Remaining Phase 2 work: `feat/animation-polish` (nice-to-have) and the `0.2.0-alpha` NuGet cut.

---

## Phase 3 — Dogfood via `shellui.dev` (~2–4 weeks)

This phase happens in the `shellui-dev/shellui.dev` repo (separate from `shellui-dev/shelldocs`). Branches in that repo consume ShellDocs `0.2.0-alpha` from NuGet.

Every rough edge that shellui.dev hits becomes a ShellDocs improvement, backported as patch releases (`0.2.1`, `0.2.2`, ...). Once shellui.dev is complete and smooth, ShellDocs tags `0.3.0-alpha` reflecting the hardening.

Branches expected in `shellui.dev`:
- `chore/scaffold` — `shelldocs init` a new Blazor WASM project
- `content/introduction` — landing + introduction + installation content
- `content/components` — ~68 component reference pages
- `content/theming` — theming guide, tweakcn walkthrough
- `content/cli` — CLI reference
- `content/blocks` — layout blocks (dashboard-01, dashboard-02, ...)
- `chore/gh-pages-deploy` — CI + CNAME to `shellui.dev`

Branches expected back in `shelldocs`:
- `fix/hardening-*` — a handful of small branches for edge cases discovered by shellui.dev
- `chore/release-0.3.0-alpha` — reflects hardening

**Milestone:** shellui.dev live at `shellui.dev`. Community-visible proof point.

---

## Phase 4 — ShellDocs' own site + API stability (~2 weeks)

### `chore/shelldocs-dev-bootstrap` *(in `shelldocs.dev` repo, or `examples/shelldocs.dev/` in shelldocs monorepo)*
- Bootstrap the ShellDocs docs site using ShellDocs itself
- Author full ShellDocs docs: getting started, CLI reference, primitives reference, theming guide, migration guides
- CNAME to `shelldocs.dev`

### `feat/xml-doc-extraction`
Ships to new package `ShellDocs.Xml`.

- MSBuild task: extract XML doc comments from a project, emit JSON per public type
- `<TypeTable Source="ShellUI.Components.Button" />` — auto-generates from the JSON
- Optional — hand-authored `<TypeRow>` children still supported

### `chore/release-1.0.0-rc`
- Version bump, notes, NuGet push
- API frozen — breaking changes require major version bump from here

### `chore/release-1.0.0`
- After `rc` bakes for 2+ weeks with no critical issues
- Announce publicly: blog post, Twitter, submit to /r/dotnet, /r/csharp, /r/blazor
- Reach out to MudBlazor / Radzen / AvaloniaUI teams about adoption

**Milestone:** ShellDocs 1.0. A stable framework anyone can adopt.

---

## Phase 5 — Ecosystem push (ongoing)

### `feat/theme-fuma`
Ships to new package `ShellDocs.Themes.Fuma`.

- Fumadocs-inspired theme preset — different color palette, different heading treatment, different card styling
- Preview at `shelldocs.dev/themes/fuma`

### `feat/theme-nextra`
Ships to new package `ShellDocs.Themes.Nextra`.

- Nextra-inspired preset

### `feat/blazor-server-support`
Ships to `ShellDocs.Core` + `ShellDocs.Components`.

- Mode flag on `AddShellDocs()` — `HostingMode.Server` vs. `HostingMode.WebAssembly`
- Server mode gives up code-splitting benefits but wins on load time
- Different JS interop patterns for animations under Server

### `feat/openapi-reference`
Ships to new package `ShellDocs.OpenApi`.

- OpenAPI spec → API reference page generator
- For consumers documenting REST APIs alongside their .NET client libraries

### `feat/interactive-playground`
- Roslyn WASM-based code sandbox for live component-code editing
- Big lift — only if community demand justifies it

---

## Component index — which branch ships which primitive

| Primitive | Branch |
|---|---|
| `DocsLayout` | `feat/components-shell` (Phase 1) |
| `DocsHeader` | `feat/components-shell` (Phase 1) |
| `DocsSidebar` | `feat/components-shell` (Phase 1) |
| `MarkdownContent` | `feat/components-shell` (Phase 1) |
| `CodeBlock` | `feat/codeblock-shiki` (Phase 1) |
| `SearchDialog` | `feat/search-primitives` (Phase 2) |
| `TableOfContents` | `feat/toc-primitive` (Phase 2) |
| `PrevNextNav` | `feat/nav-primitives` (Phase 2) |
| `DocsBreadcrumb` | `feat/nav-primitives` (Phase 2) |
| `DocsTabs` | `feat/content-primitives` (Phase 2) |
| `Callout` | `feat/content-primitives` (Phase 2) |
| `LinkCard` | `feat/content-primitives` (Phase 2) |
| `FileTree` | `feat/content-primitives` (Phase 2) |
| `Steps` | `feat/content-primitives` (Phase 2) |
| `TypeTable` | `feat/api-reference-primitives` (Phase 2) |
| `ComponentPreview` | `feat/api-reference-primitives` (Phase 2) |

---

## Dependencies on ShellUI

ShellDocs depends on `ShellUI.Components` for base primitives:

| ShellUI primitive | Used by |
|---|---|
| `<Button>` | `<CodeBlock>` copy action, `<SearchDialog>` triggers, `<PrevNextNav>` |
| `<Card>` | `<LinkCard>` |
| `<Dialog>` | `<SearchDialog>` (modal wrapper) |
| `<CommandPalette>` | `<SearchDialog>` (search interaction) |
| `<Tabs>` | `<DocsTabs>` |
| `<Breadcrumb>` | `<DocsBreadcrumb>` |
| `<Alert>` | `<Callout>` (or standalone) |
| `<Table>` | `<TypeTable>` |
| `<ThemeToggle>` | `<DocsHeader>` |
| `<Sidebar>` primitives | `<DocsSidebar>` (composes SidebarProvider/Trigger/Content) |
| `Shell.Cn` | Throughout |

**Locked ShellUI version:** ShellDocs targets `ShellUI.Components >= 0.5.0` (the version that ships `feat/data-selection-suite` — CommandPalette is required). Bumps require a ShellDocs major/minor.

### Install path — Tailwind-first, NuGet later

Mirrors how shadcn interops with fumadocs: shared CSS variables on `:root`, both design systems read them, one visual language.

- **Phase 1–2 (now):** ShellUI ships as a Tailwind consumer. Doc site owners install ShellUI the same way they would in any Blazor app — via `shellui add card` etc. — and the components read the same `--primary`, `--muted`, `--border` tokens that ShellDocs emits. Zero interop work; a `<Card>` written mid-markdown just picks up the ShellDocs palette. `ShellDocs.Tokens` (next branch) formalizes the contract so both packages point at the same source of truth.
- **Phase 3+ (`feat/shellui-nuget-interop`):** teach the ShellUI NuGet RCL to defer to `ShellDocs.Tokens` when it's on the classpath, so shipping both packages doesn't double-emit `:root` blocks. Small change — an `AddShellUI(o => o.UseSharedTokens())` opt-in. Deferred because it's not blocking for real docs sites — Tailwind consumers get 90% of the value today, and the NuGet story only matters for pure-server projects that don't run Tailwind.
- **Not doing:** shipping a "ShellUI-NuGet-only" install story for now. It'd double the QA surface for zero customer wins on day one. Revisit when a real consumer asks.

---

## Effort estimates (rough)

| Phase | Elapsed weeks | Notes |
|---|---|---|
| 1 — Core framework | 4–6 | Includes learning-curve on Markdig, Shiki-WASM, CLI patterns |
| 2 — Primitives | 2–4 | Mostly wiring — patterns from Phase 1 reused |
| 3 — Dogfood via shellui.dev | 2–4 | Content authoring is the bulk; ShellDocs fixes are incidental |
| 4 — ShellDocs' own site + 1.0 rc | 2 | Content authoring for ShellDocs docs |
| 5 — Ecosystem push | Ongoing | Not blocking |

**Total to 1.0-rc: ~10–16 weeks.** Solo-hackable if focused; halved with two people.

---

## Open questions to resolve early

- **Domain registration** — grab `shelldocs.dev` before Phase 1 ends
- **Package prefix** — `ShellDocs.*` locked in? Anyone else on NuGet using it? Verify before first publish
- **License** — MIT to match ShellUI (default assumption unless there's reason otherwise)
- **Contribution model** — CONTRIBUTING.md drafted before Phase 2 finishes so external contributors have a path
