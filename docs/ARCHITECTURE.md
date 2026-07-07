# ShellDocs Architecture

Technical architecture. Consumers won't read this; contributors and future-you will.

For high-level design see [DESIGN.md](DESIGN.md); for what ships when see [ROADMAP.md](ROADMAP.md).

---

## High-level dataflow

```
┌────────────────────────────────────────────────────────────────────────┐
│  Build time                                                             │
│                                                                         │
│  content/**/*.md  ──────►  ShellDocs.Markdown  ──────►  Rendered pages  │
│         │                       (Markdig)                   │           │
│         │                                                   │           │
│         ▼                                                   ▼           │
│    Frontmatter  ────►  NavigationGraph  ────►  SearchIndexBuilder       │
│                       (ShellDocs.Core)         (ShellDocs.Core)         │
│                                                             │           │
│                                                             ▼           │
│                                                    search-index.json    │
│                                                     (wwwroot/)          │
└────────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────┐
│  Runtime (browser, Blazor WASM)                                         │
│                                                                         │
│  URL /docs/button                                                       │
│         │                                                               │
│         ▼                                                               │
│  Router  ──►  DocsPage  ──►  NavigationGraph.ResolveByUrl              │
│                    │                    │                               │
│                    │                    └──►  NavigationNode            │
│                    │                                                    │
│                    ▼                                                    │
│  MarkdownRenderer  ──►  MarkupString + component-slot list              │
│         │                                                               │
│         │  slots contain: <Callout Type="Info">…</Callout>              │
│         │                                                               │
│         ▼                                                               │
│  DynamicComponent renders each slot via TypeRegistry                    │
└────────────────────────────────────────────────────────────────────────┘
```

---

## Package boundaries

### `ShellDocs.Core`

**Purpose:** framework-agnostic building blocks. Zero dependency on Blazor.

**Public API:**
- `NavigationNode` — POCO with `Url`, `Title`, `Description`, `Category`, `Order`, `Headings`, `Path`, `Children`
- `NavigationGraph` — tree wrapper with `ResolveByUrl(string)`, `GetPrevNext(NavigationNode)`, `GetBreadcrumb(NavigationNode)`, `Flatten()`
- `NavigationGraphBuilder` — takes `contentRoot` path + `FrontmatterParser`, returns `NavigationGraph`
- `FrontmatterParser` — YamlDotNet-backed, returns `Dictionary<string, object>`
- `SearchIndexEntry` — POCO for one page's search entry
- `SearchIndexBuilder` — walks `NavigationGraph` + rendered content, emits `IEnumerable<SearchIndexEntry>`

**Depends on:** Markdig (for AST types only — no rendering), YamlDotNet, `System.Text.Json`

**Why separate:** static site generators, external tooling, or non-Blazor consumers could reuse the graph + index logic without pulling in Blazor.

### `ShellDocs.Markdown`

**Purpose:** turn `.md` files into Razor-renderable output with typed component slots.

**Public API:**
- `MarkdownPipelineFactory` — configures a Markdig pipeline with all custom extensions
- `MarkdownRenderer` — takes markdown text or file path, returns `RenderedDocument`
- `RenderedDocument { MarkupString Html, List<ComponentSlot> Slots, DocumentFrontmatter Frontmatter, List<Heading> Headings }`
- `ComponentSlot { Guid Id, Type ComponentType, Dictionary<string, object> Parameters, RenderFragment? ChildContent }`
- `TypeRegistry` — maps `string tagName` → `Type componentType`; consumers register their types

**Depends on:** `ShellDocs.Core`, Markdig, YamlDotNet

**Key design decisions:**
- **Two-pass rendering.** First pass: Markdig turns markdown into HTML + placeholder `<span data-slot="{guid}">` markers for component tags. Second pass: `MarkdownContent` component walks its own DOM (or better: uses a `RenderTreeBuilder` on the AST directly) and swaps placeholders for `<DynamicComponent>` calls.
- **`razor:preview` blocks are two things.** Markdown fence with language `razor:preview` → a `<DocsTabs>` slot with two children: a live-rendered `<DynamicComponent>` for the "Preview" tab and a `<CodeBlock>` for the "Code" tab.
- **Inline tags are strict.** `<Button />` in markdown must match a registered type. Unknown tags render as escaped text with a build-time warning (not a runtime crash).

### `ShellDocs.Components`

**Purpose:** the UI. RCL — everyone's entry point.

**Public API:** the primitives listed in [DESIGN.md](DESIGN.md) — `DocsLayout`, `DocsHeader`, `DocsSidebar`, `CodeBlock`, `SearchDialog`, etc.

**Depends on:** `ShellDocs.Core`, `ShellDocs.Markdown`, `ShellUI.Components`

**Key design decisions:**
- **`AddShellDocs()` service extension.** Consumer's `Program.cs` calls one method to register `NavigationGraph`, `MarkdownRenderer`, `TypeRegistry`, `IJSRuntime` interop wrappers, current theme.
- **Cascading values everywhere.** `NavigationGraph`, current theme, and current page context are cascaded from `DocsLayout` so child components can consume them without prop-drilling.
- **`<DynamicComponent>` for component rendering.** Standard Blazor primitive — takes a `Type` + `Dictionary<string, object>` params. Works with any registered component.
- **No JS beyond what's necessary.** Shiki (highlighting), Motion One (animation fallback), IntersectionObserver (scroll-spy). Everything else pure Blazor.

### `ShellDocs.CLI`

**Purpose:** developer ergonomics.

**Public API:** commands, not a library.
- `shelldocs init [--yes] [--theme <name>]`
- `shelldocs new page <path>`
- `shelldocs new component-page <name>`
- `shelldocs dev [--port <n>]`
- `shelldocs build [--output <path>]`
- `shelldocs preview <component-name> [--variant <v>]`

**Depends on:** `System.CommandLine`, `Spectre.Console`, `ShellDocs.Templates`

**Key design decisions:**
- **`init` is idempotent.** Detects existing setup and skips already-done steps. Fresh consumers get everything; existing consumers can rerun to pull in new defaults.
- **`dev` is `dotnet watch` + markdown watcher.** Two file watchers: `dotnet-watch` handles `.razor` / `.cs` reload; a second watcher watches `content/**/*.md` and pings the running app via a well-known endpoint to invalidate the navigation graph.
- **`build` is `dotnet publish` + post-processing.** Publish, then rewrite base-href, generate search index, copy `index.html` → `404.html`. Same pattern the ShellUI `fix/preview-app` branch uses.

### `ShellDocs.Templates`

**Purpose:** file content used by `ShellDocs.CLI`.

**Public API:** static classes with `Content` string properties, mirroring the pattern from `ShellUI.Templates`.

**Depends on:** nothing (or `ShellDocs.Core` for the `NavigationNode` POCO if templates need it).

**Why separate from CLI:** allows the templates to be updated (and versioned) independently. Also allows the CLI to be a small binary while templates carry the bulk of the bytes.

### `ShellDocs.Xml` (v2)

**Purpose:** extract API reference from XML doc comments.

**Public API:**
- MSBuild task `<ShellDocsXmlExtract Assembly="..." XmlDoc="..." Output="..." />`
- Emits JSON per public type: `{ Name, Namespace, Summary, Properties: [{ Name, Type, Summary, Default }], Methods: [...] }`
- `<TypeTable Source="ShellUI.Components.Button" />` reads the JSON at render time

**Depends on:** `Microsoft.CodeAnalysis` (Roslyn), `System.Xml.Linq`

**Deferred to Phase 4.** Hand-authored `<TypeRow>` unblocks Phase 2 shipping.

---

## Service registration

`AddShellDocs(options)` wires up everything.

```csharp
public static IServiceCollection AddShellDocs(
    this IServiceCollection services,
    Action<ShellDocsOptions> configure)
{
    var options = new ShellDocsOptions();
    configure(options);
    services.AddSingleton(options);

    // Core — nav graph is built once at startup
    services.AddSingleton<NavigationGraph>(sp =>
        NavigationGraphBuilder.Build(options.ContentRoot));

    // Markdown pipeline is singleton — construction is expensive
    services.AddSingleton<MarkdownPipelineFactory>();
    services.AddScoped<MarkdownRenderer>();

    // Type registry is populated during Register* calls
    services.AddSingleton<TypeRegistry>(sp =>
    {
        var registry = new TypeRegistry();
        foreach (var type in options.RegisteredComponents) registry.Register(type);
        return registry;
    });

    // Theme applied as cascading value in DocsLayout
    services.AddSingleton(options.Theme);

    return services;
}
```

`ShellDocsOptions` — plain POCO with fluent-friendly config:

```csharp
public class ShellDocsOptions
{
    public string ContentRoot { get; set; } = "content";
    public string SiteName { get; set; } = "";
    public string? GitHubRepo { get; set; }
    public ShellDocsTheme Theme { get; set; } = ShellDocsTheme.Shadcn;
    public List<Type> RegisteredComponents { get; } = new();
    public bool EnableSearch { get; set; } = true;
    public string SearchIndexPath { get; set; } = "search-index.json";
    // ... more knobs

    public ShellDocsOptions RegisterComponent<T>() where T : ComponentBase
    {
        RegisteredComponents.Add(typeof(T));
        return this;
    }
}
```

Fluent API supports method chaining in `Program.cs`.

---

## Markdown pipeline internals

### Custom Markdig extensions

Three extensions register on the Markdig pipeline:

**1. Frontmatter extension**

Uses `Markdig.Extensions.Yaml.YamlFrontMatterExtension` (built-in). Wraps its output as `DocumentFrontmatter` in the `RenderedDocument`.

**2. `razor:preview` fence extension**

Extends `FencedCodeBlockRenderer` — inspects the info string. If it starts with `razor:preview`, replaces the standard code-block output with a placeholder `<span data-shelldocs-preview="{guid}"></span>` and adds a `PreviewSlot` to the rendered document.

At render time, `MarkdownContent` walks its markup and for each `data-shelldocs-preview` span, injects a `<DocsTabs>` with the preview slot.

**3. Inline component tag extension**

Custom `InlineParser` on Markdig — matches `<TagName params />` at the block or inline level. Rejects if `TagName` isn't in `TypeRegistry`. Emits a placeholder `<span data-shelldocs-component="{guid}"></span>` and adds a `ComponentSlot` to the rendered document.

Same render-time swap logic as `razor:preview`.

### Why placeholders + slot list, not direct Razor generation

Generating Razor source code from markdown at build time is possible but adds tooling complexity. Markdown-to-HTML + slot list at render time keeps everything in the runtime and lets us change the wrapper components without rebuilding the source.

Trade-off: DOM walking at render time has a per-page cost (~1ms for a large page). Acceptable.

---

## Navigation graph internals

### Build process

`NavigationGraphBuilder.Build(contentRoot)`:

1. Recursively walk `contentRoot`
2. For each `.md` file: parse frontmatter, create a `NavigationNode` with `Url = path minus root + filename`, populate from frontmatter
3. For each folder: check for `meta.json`, build children in specified order; fall back to alphabetical
4. Cross-link `Parent` and `Children`
5. Compute derived properties: `NextNode`, `PreviousNode` (in flatten order)
6. Return `NavigationGraph` root

Cache result — rebuild only when `dev` mode detects a change.

### Runtime queries

`NavigationGraph.ResolveByUrl("/docs/button")`:
- Walk the tree matching path segments
- O(depth) — usually 2–3 segments
- Return `null` if not found (page shows 404)

`NavigationGraph.GetPrevNext(node)`:
- Precomputed at build; O(1) lookup

`NavigationGraph.GetBreadcrumb(node)`:
- Walk up `Parent` chain; O(depth)

### `meta.json` schema

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

- Strings: page slug matches filename minus `.md`
- `"---"`: renders as a section divider in the sidebar
- Objects: nested subsection with its own title and pages
- Nested folders can have their own `meta.json` for further nesting

Unknown page slugs (typo in `meta.json`) emit a build-time warning but don't crash.

---

## Search index

### Build time

`SearchIndexBuilder.Build(graph, renderer)`:

For each node in the graph:
1. Render the page's markdown (via `MarkdownRenderer`)
2. Extract all `<h2>` and `<h3>` headings (already in `RenderedDocument.Headings`)
3. Extract first 200 chars of body per heading (for excerpt)
4. Build `SearchIndexEntry { Url, Title, Description, Category, Headings, Excerpt }`

Serialize the list to `search-index.json`, written to output `wwwroot/`.

### Runtime

`SearchDialog` component on first open (lazy):
1. `HttpClient.GetFromJsonAsync<SearchIndexEntry[]>("search-index.json")`
2. Store in memory for subsequent opens

On each keystroke:
1. Fuzzy match against `Title` + `Headings.Text` + `Excerpt`
2. Score = Levenshtein + prefix bonus + heading-match bonus
3. Top N results rendered inline in the modal

No debounce needed for docs sites this size. ~500 entries client-filtered in <1ms.

### Scaling out

For 1000+ pages, the client-side fuzzy match starts to lag on slow devices. Escape hatch: `options.SearchProvider = SearchProvider.Orama` swaps to an Orama-backed backend. Not v1; escape hatch design only.

---

## Component rendering (`<DynamicComponent>`)

Standard Blazor primitive — takes `Type` + `IReadOnlyDictionary<string, object>?`.

`MarkdownContent` component:

```razor
@foreach (var slot in Document.Slots)
{
    <span data-slot="@slot.Id">
        <DynamicComponent Type="@slot.ComponentType" Parameters="@slot.Parameters">
            @slot.ChildContent
        </DynamicComponent>
    </span>
}
```

Then the plain HTML with `data-slot` placeholders is emitted alongside; the DOM ends up interleaved. Details in the [markdown pipeline notes](#markdown-pipeline-internals).

**Parameter serialization:** frontmatter values are strings from YAML. `TypeRegistry` inspects each component's `[Parameter]` properties to know the target type and coerces (int, bool, enum, string). Complex types (records, DTOs) need JSON literals in the markdown.

---

## Theme layer

### Layer 1 — presets

Each theme is a package that ships:
- A CSS file with custom properties (`--color-bg`, `--color-accent`, `--font-heading`, `--radius`, etc.)
- Optional component style overrides (`.docs-sidebar-item.active { … }`)
- Registered via `options.Theme = ShellDocsTheme.Fuma`

`DocsLayout` applies the theme's CSS by injecting the stylesheet link into the `<HeadContent>` or by cascading a `ThemeContext` that child components consume.

### Layer 2 — full customization

Every custom property is overridable in the consumer's `wwwroot/*.css`. Cascade order: theme preset → consumer CSS. Standard shadcn escape hatch.

Every component parameter is public. Consumer can wrap `<DocsSidebar>` in a component of their own with different behaviour — same override pattern as shadcn's copy-and-edit philosophy.

---

## Static site generation

`shelldocs build`:

1. `dotnet publish -c Release -o <output>` — Blazor's WASM AOT publish
2. Run `SearchIndexBuilder` on the content, write `search-index.json` to `output/wwwroot/`
3. Rewrite `<base href="/" />` in `index.html` to `<base href="/<basePath>/" />` if `basePath` supplied
4. Copy `index.html` → `404.html` (GH Pages SPA fallback)
5. Copy any content-referenced static assets (images embedded in markdown)
6. Exit

Optionally, in a future version: prerender each route to a static `.html` file for SEO / first-paint. Requires Blazor's prerendering support — feasible in Server hosting mode, harder in WASM. Deferred.

---

## Dev server (`shelldocs dev`)

Two processes:

**1. `dotnet watch run`** on the consumer project. Hot-reloads on `.razor` / `.cs` changes.

**2. Markdown file watcher** (`FileSystemWatcher` on `content/**/*.md`) — spawns from the same CLI process. On change:
- Rebuild `NavigationGraph`
- POST to a well-known endpoint on the running app (`/_shelldocs/reload-graph`)
- The endpoint invokes a `NavigationGraph`-refresh service (registered by `AddShellDocs()` in dev mode)

Alternative: SignalR channel between CLI and app. Overkill for v1.

---

## Testing strategy

Same three-layer approach as ShellUI:

**1. Unit tests** (`ShellDocs.Tests`)
- xUnit
- Markdig extensions in isolation
- `NavigationGraphBuilder` on synthetic content trees
- `TypeRegistry` coercion
- `SearchIndexBuilder` output shape

**2. Template compile tests**
- Every CLI template's `Content` is Roslyn-parsed to catch escape-quote regressions

**3. Live↔template sync tests**
- The scaffold generated by `shelldocs init` matches what the CLI templates say it should

**4. E2E in CI**
- `shelldocs init` a Blazor WASM project in a temp dir
- `shelldocs build` it
- Verify output has expected files, valid HTML, search index present

---

## Cross-cutting concerns

### Bundle size

Priorities:
- Base ShellDocs.Components: target <100KB gzipped
- With Shiki full theme set: +~2MB (opt-out via config to a Prism-based highlighter, <50KB)
- With Motion One fallback: +~10KB gzipped

Every JS interop file gets budget scrutiny.

### Accessibility

- All primitives ship with correct ARIA roles + labels
- Keyboard nav complete (Tab / Shift+Tab / Enter / Esc across all interactives)
- Focus management on modal open/close
- Skip-to-content link in `DocsLayout`
- `prefers-reduced-motion` respected everywhere animations run

### Internationalization (i18n)

**Not v1.** Design left open by making content routing extension-friendly — a future `ShellDocs.I18n` package could add locale-aware content resolution without breaking `NavigationGraph`'s shape.

---

## What we're NOT building (deliberately)

- **A markdown editor.** Consumer authors in whatever they use (VSCode, Rider, whatever).
- **A CMS.** Content is `.md` files in a git repo. That's the interface.
- **A server backend.** ShellDocs is static-only; the search index is client-side.
- **A hosted service.** No shelldocs.dev SaaS; it's a NuGet package family.
- **A design system.** ShellUI is that. ShellDocs uses ShellUI; doesn't compete.

Scope discipline. These are all things fumadocs also didn't build — and it stayed lean and usable.
