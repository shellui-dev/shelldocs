# ShellDocs — Design Doc

Living design doc. Updated as decisions land.

---

## TL;DR

**ShellDocs is a docs framework for .NET, like [fumadocs](https://fumadocs.dev) for Node/React.**

Ship it as NuGet packages + a CLI so any .NET user can:

```bash
dotnet new blazorwasm -n MyDocs
cd MyDocs
dotnet tool install -g ShellDocs.CLI
shelldocs init
```

…and end up with a sleek, animated, Cmd+K-searchable docs site with markdown authoring and live Razor component previews. First class targets: ShellUI, MudBlazor, Radzen, MAUI Community Toolkit — any Blazor UI library that needs docs. Long-term consumer-agnostic: docs sites for regular libraries, guides, blog-style content.

The framework is **opinionated by default, composable underneath.** Reasonable defaults get you a beautiful site in 5 minutes; primitives are exposed if you want to build something custom.

---

## Positioning — how ShellDocs relates to existing options

| Tool | Ecosystem | What it is | Why not just use this |
|---|---|---|---|
| **fumadocs** | Node / Next.js | Docs framework — MDX + primitives + CLI | Different runtime; .NET users need to context-switch |
| **Docfx** | .NET / Microsoft | XML-doc + conceptual docs generator, ASP.NET template | Powerful but dated aesthetic, opinionated in a Microsoft-y way, not Blazor-native |
| **Statiq** | .NET | Static site generator toolkit | Lower-level; you build the site yourself |
| **shadcn/ui docs** | Node / Next.js | Custom Next.js app, not extracted as a framework | Not reusable — only shadcn ships from it |
| **MudBlazor docs** | .NET / Blazor | Custom Blazor Server app | Not extracted; every Blazor library rebuilds this from scratch |

**The gap:** every .NET UI library ends up hand-rolling their own docs site (see MudBlazor.com, radzen.com, mudblazor.com). None are extractable as reusable frameworks. **ShellDocs = the "just use this" answer for .NET docs sites.**

Design north star: **fumadocs' polish + shadcn's composability + .NET's runtime.**

---

## Package family

`shellui-dev/shelldocs` monorepo ships six NuGet packages:

| Package | Role | Analog in fumadocs |
|---|---|---|
| **`ShellDocs.Core`** | Navigation graph, search index model, routing helpers | fumadocs-core |
| **`ShellDocs.Markdown`** | Markdig pipeline + frontmatter + Razor component embedding | fumadocs-mdx |
| **`ShellDocs.Components`** | RCL — UI primitives (DocsSidebar, CodeBlock, SearchDialog, etc.) | fumadocs-ui |
| **`ShellDocs.CLI`** | `shelldocs init`, `shelldocs new page`, `shelldocs dev`, `shelldocs build` | create-fuma-app + tooling |
| **`ShellDocs.Templates`** | Content used by CLI (starter markdown, meta.json, .csproj patches) | starter templates |
| **`ShellDocs.Xml`** *(v2)* | Extract API reference from XML doc comments, generate `<TypeTable>` markup | fumadocs-typescript |

Optional / future:

| Package | Role |
|---|---|
| `ShellDocs.OpenApi` | Turn OpenAPI spec into API reference pages |
| `ShellDocs.Themes.Fuma` | Fumadocs-inspired theme preset |
| `ShellDocs.Themes.Nextra` | Nextra-inspired theme preset |

Package families keep each concern small and versioned independently. Users install only what they need. `ShellDocs.CLI` is the entry point that pulls the rest.

---

## CLI UX

Modeled on `shellui` — same feel, same commands:

```bash
# Bootstrap a docs project into any Blazor WASM project
shelldocs init
  → Adds ShellDocs.Components + ShellDocs.Markdown packages
  → Creates content/docs/, content/components/ folders with sample .md files
  → Creates Layout/DocsLayout.razor + Pages/DocsPage.razor
  → Patches Program.cs to wire the Markdown pipeline + navigation graph
  → Writes meta.json for sidebar structure
  → Copies default theme CSS

# Add a doc page from a template
shelldocs new page installation
shelldocs new component-page button

# Local dev with hot reload for markdown files
shelldocs dev
  → Watches content/**/*.md, rebuilds nav graph on change, hot-reloads Blazor

# Build with static site generation for deployment
shelldocs build
  → Generates search-index.json, prerenders every route to /wwwroot,
    outputs static site ready for GH Pages / Vercel / Netlify / whatever

# Preview any component in isolation
shelldocs preview Button --variant Destructive
```

Same install path as `shellui`: `dotnet tool install -g ShellDocs.CLI`.

---

## Architecture — how ShellDocs wraps a Blazor app

ShellDocs assumes the host is a **Blazor WebAssembly** app. It layers on top via three mechanisms:

**1. Markdown pipeline** (`ShellDocs.Markdown`)

`Program.cs` registers a Markdig-based renderer configured with:
- YAML frontmatter parsing (YamlDotNet)
- Custom fenced-code handling — ` ```razor:preview ` becomes a `<CodeBlock>` + `<DynamicComponent>` pair
- Custom link resolution — `[Button](@component:button)` maps to `/components/button`
- Inline Razor tags — `<Button />` in markdown gets rendered via `<DynamicComponent>` at render time

**2. Navigation graph** (`ShellDocs.Core`)

At startup, walks `content/**/*.md`, reads frontmatter, builds a tree keyed by URL. Each node has: title, description, category, order, section headings. Used by:
- `<DocsSidebar>` to render grouped nav
- `<PrevNextNav>` to compute previous/next links
- `<SearchDialog>` to feed the client-side index
- SPA routing to map `/docs/introduction` → `content/docs/introduction.md`

**3. Component primitives** (`ShellDocs.Components`)

Standard RCL. Consumer references it, uses `<DocsLayout>` in their MainLayout, and the framework handles the rest.

Minimal `Program.cs` for a ShellDocs consumer:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddShellDocs(options =>
{
    options.ContentRoot = "content";
    options.SiteName = "ShellUI";
    options.GitHubRepo = "shellui-dev/shellui";
    options.Theme = ShellDocsTheme.Fuma;
});

await builder.Build().RunAsync();
```

Three lines of config, everything else discoverable from convention.

---

## Primitives shipped in `ShellDocs.Components`

| Component | Purpose |
|---|---|
| **`DocsLayout`** | Full-page shell: header + sidebar + main + TOC + footer — the primary chrome |
| **`DocsHeader`** | Top nav: logo, primary nav, search trigger, theme toggle, GitHub link |
| **`DocsSidebar`** | Left-rail grouped nav, auto-derived from nav graph, collapsible sections, active highlighting |
| **`DocsBreadcrumb`** | Section > subsection > current-page trail |
| **`TableOfContents`** | Right-rail auto-generated from `<h2>`/`<h3>`, scroll-spy via IntersectionObserver |
| **`PrevNextNav`** | Bottom-of-page previous/next links, footer-anchored |
| **`SearchDialog`** | Cmd+K modal — reads generated `search-index.json`, fuzzy matches title + heading + excerpt. Composes existing `<CommandPalette>` from ShellUI |
| **`CodeBlock`** | Syntax-highlighted code — copy button, filename tab, line highlighting, language badge. **Shiki** via WASM (VSCode-parity highlighting) |
| **`DocsTabs`** | Multi-tab code examples (`npm` / `yarn` / `pnpm` / `standalone`) — remembers selection across tab groups on the same page via shared context |
| **`FileTree`** | Static filesystem visualization for project structure explainers |
| **`Steps`** | Vertical numbered steps for onboarding flows |
| **`TypeTable`** | Props table (name / type / default / description) — hand-authored initially, auto-generated from XML docs in `ShellDocs.Xml` |
| **`LinkCard`** | Card-shaped link with title / description / icon — for "Next steps" grids |
| **`ComponentPreview`** | Live preview of any component by name + props, with source-view toggle. Renders via `<DynamicComponent>` |
| **`Callout`** | Info / warning / tip / danger box. May be a `<Alert>` variant from ShellUI + docs styling |

All animated with mount transitions, hover states, smooth theme switching. Reduced-motion respected via `prefers-reduced-motion`.

---

## Content model

Every doc page is a `.md` file with YAML frontmatter under `content/`:

```markdown
---
title: Button
description: Interactive button component with type-safe variants
category: Form
order: 10
---

# Button

<Callout Type="Info">
Button uses ShellUI's `Shell.Cn` for class composition.
</Callout>

## Installation

Install with the CLI:

\`\`\`bash
dotnet shellui add button
\`\`\`

## Usage

\`\`\`razor:preview
<Button Variant="ButtonVariant.Destructive">Delete</Button>
\`\`\`

## API Reference

<TypeTable>
    <TypeRow Name="Variant" Type="ButtonVariant" Default="Default" Description="Visual style" />
    <TypeRow Name="Size" Type="ButtonSize" Default="Default" Description="Size preset" />
    <TypeRow Name="Disabled" Type="bool" Default="false" Description="Disables the button" />
</TypeTable>
```

Two extensions to standard markdown:

1. **`razor:preview` fenced blocks** — render as `<DocsTabs><Tab Label="Preview"><LiveComponent /></Tab><Tab Label="Code"><CodeBlock /></Tab></DocsTabs>`. Preview and code stay in sync automatically.

2. **Inline component tags** — `<Callout>`, `<TypeTable>`, `<LinkCard>` and any registered component render as real Razor via `<DynamicComponent>`. Consumer registers additional component types in options:

   ```csharp
   options.RegisterComponent<Callout>();
   options.RegisterComponent<TypeTable>();
   options.RegisterComponent<Button>();  // for library authors documenting their own components
   ```

---

## Navigation & meta.json

Sidebar order is derived from a `meta.json` file per folder:

```json
{
    "title": "Components",
    "pages": [
        "button",
        "input",
        "---",
        {
            "title": "Data Display",
            "pages": ["table", "card", "badge"]
        }
    ]
}
```

Falls back to alphabetical if no `meta.json` present. Nested folders get their own `meta.json`. `---` renders a section divider.

---

## Search

Build-time indexer walks `content/**/*.md`, produces `wwwroot/search-index.json`:

```json
[
    {
        "url": "/components/button",
        "title": "Button",
        "description": "Interactive button component…",
        "category": "Components",
        "headings": [
            { "level": 2, "text": "Installation", "id": "installation" },
            { "level": 2, "text": "Usage", "id": "usage" }
        ],
        "excerpt": "Button uses ShellUI's Shell.Cn for class composition. Install with the CLI…"
    }
]
```

`<SearchDialog>` fetches once on Cmd+K, does client-side fuzzy match (Fuse.js-equivalent). No server, no infrastructure, works offline. Docs sites this size don't need a real search backend.

For larger docs sites (100+ pages), swap in Orama or Meilisearch via a config option — the primitive is search-provider-agnostic.

---

## Theming

Two-layer approach:

**Layer 1: theme presets** — `ShellDocs.Themes.*` packages, each preset is a set of CSS custom properties + component style overrides. Consumer picks one in `Program.cs`:

```csharp
options.Theme = ShellDocsTheme.Fuma;      // fumadocs-inspired
options.Theme = ShellDocsTheme.Nextra;    // nextra-inspired
options.Theme = ShellDocsTheme.Shadcn;    // shadcn-inspired (default)
```

**Layer 2: full customization** — every CSS variable and every component parameter is overridable. Ejecting from a theme means writing your own CSS + variant. Same escape hatch shadcn provides.

Dark mode: automatic via `prefers-color-scheme`, persistent via localStorage, toggleable via `<ThemeToggle>` in header. Theme selection also respected on iframe embeds via `?theme=` query param.

---

## Animation

Sleek defaults, `prefers-reduced-motion` respected everywhere:

- **Page transitions** — fade + slight translate on route change (150ms)
- **Sidebar collapse** — height animation, chevron rotation (200ms)
- **TOC scroll-spy** — active heading indicator slides between items (spring-based)
- **Search dialog** — scale + fade in from center (100ms)
- **CodeBlock copy button** — checkmark morph on success (150ms + 1s hold)
- **Sidebar item hover** — background fade + subtle scale
- **Mobile drawer** — slide from left with backdrop blur

Framework choice: **CSS transitions + view-transitions API** where available, JS-based (Motion One) fallback for animations that need spring physics. No React-style animation library — keep the bundle lean.

---

## Deployment stories

Any static host works — ShellDocs generates a static site via `shelldocs build`:

| Target | Notes |
|---|---|
| **GitHub Pages** | `shelldocs build` outputs `wwwroot/`, workflow pushes to `gh-pages` branch. Base-href rewritten to `/<repo>/` at build time. |
| **Vercel** | Detects Blazor WASM. `shelldocs build` output goes to `.output/public/`. |
| **Netlify** | Same as Vercel. |
| **Cloudflare Pages** | Same. |
| **Static file host** | Copy `wwwroot/` to any CDN. |
| **Custom domain** | CNAME to your host. Base href becomes `/`. |

Server-side rendering is not supported in v1 — Blazor WASM only. May revisit for Blazor Server + prerendering later (Phase 5).

---

## Ecosystem story

```
                         shelldocs.dev
                     (ShellDocs' own docs
                       — dogfooded)
                              │
                              │  uses
                              ▼
                        ShellDocs
                              ▲
                              │  consumes
                              │
               ┌──────────────┼──────────────┐
               │              │              │
        shellui.dev       mudblazor's     avalonia's
       (ShellUI docs)    (hypothetical)  (hypothetical)
```

- **`shellui-dev/shelldocs`** — the framework
- **`shellui-dev/shellui.dev`** — ShellUI's docs site, built with ShellDocs (proves out the framework, sets the aesthetic bar)
- **`shellui-dev/shelldocs.dev`** — ShellDocs' own docs site, also built with ShellDocs (dogfood)
- **Other adopters** — Anyone building a .NET UI library gets a docs site by adopting ShellDocs

Long-term: if ShellDocs is good, it becomes the default "how do you build a docs site for a .NET library" answer, the same way fumadocs became that for the Next.js world.

---

## The Avalonia / cross-framework question

**v1 is Blazor-only.** ShellDocs renders in the browser via Blazor WASM. Live component previews are real Blazor renders.

**For Avalonia (or MAUI, etc.) library docs** three routes:

1. **Screenshot previews** — pre-generate images at build time, doc pages show image + code. Loses "live", keeps parity for the framework-agnostic content sections.
2. **Iframe to a running Avalonia app** — back to the iframe workaround, but now for cross-framework only.
3. **Wait for Avalonia's browser target to stabilize** — Avalonia already ships web builds. When mature, `IComponentPreviewer` interface in ShellDocs could have an `AvaloniaPreviewer` implementation.

Design principle: **don't design for Avalonia now, but keep an escape hatch.** `IComponentPreviewer` abstraction (Blazor is one implementation) leaves room without adding complexity to v1.

---

## Phased delivery

**Phase 0 — Setup** *(this)*
- Design doc (this file). Alignment on framework framing.
- Domain: register `shelldocs.dev`

**Phase 1 — Core framework** *(4–6 weeks)*
- Create `shellui-dev/shelldocs` repo
- Ship `ShellDocs.Core` + `ShellDocs.Markdown` + minimal `ShellDocs.Components` (DocsLayout, DocsSidebar, DocsHeader, CodeBlock, MarkdownRenderer)
- Ship `ShellDocs.CLI` with `init`, `dev`, `build`
- One theme preset (Shadcn — matches ShellUI aesthetic)
- Release as `0.1.0-alpha` on NuGet

**Phase 2 — Primitive completeness** *(2–4 weeks)*
- Add remaining primitives: `SearchDialog`, `TableOfContents`, `PrevNextNav`, `DocsTabs`, `TypeTable`, `LinkCard`, `Callout`, `Steps`, `FileTree`, `ComponentPreview`
- Search index build tool
- Animation polish pass — mount transitions, page transitions, scroll-spy
- Release as `0.2.0-alpha`

**Phase 3 — Dogfood via shellui.dev** *(2–4 weeks)*
- Bootstrap `shellui-dev/shellui.dev` repo using ShellDocs `0.2.0-alpha`
- Author real content — introduction, installation, theming, all 68 components
- Deploy to GH Pages, CNAME to `shellui.dev`
- Feedback loop drives ShellDocs `0.3.0` — every rough edge shellui.dev hits becomes a ShellDocs improvement

**Phase 4 — ShellDocs' own site** *(2 weeks)*
- Bootstrap `shellui-dev/shelldocs.dev` using ShellDocs
- Full docs for ShellDocs itself
- Deploy to `shelldocs.dev`
- ShellDocs `1.0.0-rc` — API stable

**Phase 5 — Ecosystem push**
- Additional theme presets (Fuma, Nextra)
- `ShellDocs.Xml` — auto-`TypeTable` from XML doc comments
- Migration guides for existing docs sites (mudblazor → shelldocs, etc.)
- Blog post + Twitter push
- Reach out to MudBlazor / Radzen / AvaloniaUI teams

---

## Open questions

- **Shiki vs. Prism for CodeBlock.** Shiki gives VSCode-parity syntax highlighting but ships a ~2MB WASM regex engine. Prism is lighter (~50KB) but syntax fidelity is patchy for Razor/C#. **Lean:** Shiki, ship a smaller Prism-based fallback for low-bandwidth via config.
- **Blazor Server support.** v1 is Blazor WASM. Blazor Server support = adding a mode flag, mostly working, but page navigation animations and code-splitting behave differently. Revisit Phase 5.
- **CLI vs. `dotnet new` template.** Should `shelldocs init` also be exposed as `dotnet new shelldocs`? Probably yes — both entry points converge on same scaffolding.
- **Theme distribution.** Should themes ship as separate NuGet packages (small, opt-in) or bundled in `ShellDocs.Components` (one install, more bytes)? **Lean:** separate packages — matches shadcn's per-component philosophy.
- **XML doc comment extraction.** Automating `<TypeTable>` from XML docs is the killer feature vs. hand-authoring. But it requires the consumer's build to emit XML docs and expose the referenced assemblies. Non-trivial. Phase 5+.
- **Interactive playgrounds** (like MDN's live sandbox). Nice-to-have. Requires a compiler-in-browser (Roslyn WASM) or server round-trip. Deprioritized until it's clearly needed.

---

## Naming, domains, mental model

- **ShellDocs** — the framework (the code, the repo, the NuGet packages)
- **`shelldocs.dev`** — the framework's own marketing/docs site
- **`shellui.dev`** — ShellUI's docs site, built with ShellDocs
- **`shellui-dev/shelldocs`** — the framework's GitHub repo
- **`shellui-dev/shellui.dev`** — ShellUI docs site's repo
- **`shellui-dev/shelldocs.dev`** — ShellDocs marketing site's repo (may just be inside the shelldocs repo as `examples/shelldocs.dev`)

All three repos in the same org. Domains bought separately once names are locked.

---

## Cross-references

- [ROADMAP.md](ROADMAP.md) — branch-by-branch implementation plan
- [ARCHITECTURE.md](ARCHITECTURE.md) — technical architecture
- [fumadocs](https://fumadocs.dev) — closest analog in another ecosystem
- [Docfx](https://dotnet.github.io/docfx/) — .NET's incumbent, what we're competing against on aesthetic + composability
- [shadcn/ui docs source](https://github.com/shadcn-ui/ui/tree/main/apps/www) — reference for content structure
