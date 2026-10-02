# ShellDocs Architecture

How ShellDocs works today, for contributors. For the original product design see [DESIGN.md](DESIGN.md); for release history see [CHANGELOG.md](../CHANGELOG.md).

---

## Overview

A ShellDocs site is a Blazor Web App (the example and the `shelldocs init` scaffold use interactive Server rendering with prerendering) plus a `content/` folder of markdown.

```
startup ─ AddShellDocs(options)
          ├─ NavigationGraphBuilder.Build(content/)   → NavigationGraph   (singleton)
          ├─ SearchIndex.FromGraph(graph)             → SearchIndex       (singleton, in memory)
          ├─ options.BuildTypeRegistry()              → TypeRegistry      (built-ins, then consumer components)
          └─ MarkdownRenderer, DocsVersionResolver                         (singletons)

request ─ /docs/{*Path} page (consumer-owned)
          ├─ NavigationGraph.ResolveByUrl(url)        → NavigationNode (its .md path)
          ├─ MarkdownRenderer.RenderFile(path)        → RenderedDocument (HTML + slot list + headings)
          └─ <MarkdownContent Document="…" />         → HTML parts + live components, inside DocsLayout

browser ─ shelldocs.js  delegated chrome handlers (sidebar, selectors, tabs, previews, mobile nav, TOC)
          Shiki          syntax highlighting into JS-owned siblings of the code blocks

static  ─ shelldocs build: dotnet publish → run the app → prerender every graph URL to index.html files
```

Everything a reader clicks in the chrome is server-rendered with its initial state in data attributes and switched by `shelldocs.js`, so the same HTML works under a live Blazor runtime and on a static host. See [Client-side chrome](#client-side-chrome-shelldocsjs).

---

## Package boundaries

### `ShellDocs.Core`

Framework-agnostic building blocks; no Blazor dependency. Depends on Markdig, YamlDotNet.

- `NavigationNode` — `Url`, `Title`, `Description`, `Category`, `Order`, `Path`, `Kind` (Page / Section / Divider), `Parent`, `Children`.
- `NavigationGraphBuilder.Build(contentRoot)` → `NavigationGraph` — `ResolveByUrl`, `GetPrevNext` (optionally scoped), `GetBreadcrumb`, `FindFolder`, `FirstPageUnder`, `Flatten`, `AllUrls`.
- `MetaJson` — `meta.json` model (`title`, `pages`, `hidden`).
- `FrontmatterParser.Parse(markdown)` → `ParsedDocument(Frontmatter, Body)`.
- `SearchIndex.FromGraph(graph)` → page and h2/h3 heading entries; `MarkdownPlainText` extracts page body text.
- `UrlPath` — segment-aware path helpers (`Normalize`, `IsUnder`, `RelativeTo`, `Combine`).

### `ShellDocs.Markdown`

Turns markdown into HTML plus typed component slots. Depends on `ShellDocs.Core`, Markdig, YamlDotNet.

- `MarkdownRenderer.Render(markdown)` / `RenderFile(path)` → `RenderedDocument(Html, Slots, Source, Headings)`; `LastWarnings` holds unknown/unclosed tag warnings.
- `Slot` records: `ComponentSlot` (inline tag) and `PreviewSlot` (razor:preview fence, with parsed `Nodes`).
- `TypeRegistry` — tag name → component type; records `Collisions`.
- `MarkdownPipelineFactory`, `CodeBlockEnhancer`, `HeadingExtractor`.

### `ShellDocs.Components`

The Razor class library everyone references. Depends on `ShellDocs.Core`, `ShellDocs.Markdown`, `ShellDocs.Tokens`, `ShellIcons.Blazor` and the ASP.NET Core shared framework (it reads content from disk and runs middleware, so it's server-side).

- `AddShellDocs(options)` and `ShellDocsOptions`.
- Layouts: `DocsLayout` (`TopNav` / `Sidebar` variants), `HomeLayout`.
- Chrome: `DocsHeader`, `DocsSidebar`, `DocsSidebarHeader`, `DocsMobileBar`, `PackageSelector`, `VersionSelector`, `DocsBreadcrumb`, `PrevNextNav`, `TableOfContents`, `SearchDialog`, `ThemeToggle`, `DocsFooter`, `BrandLogo`.
- Content primitives (auto-registered for markdown): `Callout`, `Card`, `CardGrid`, `LinkCard`, `Steps`/`Step`, `FileTree`/`FileTreeItem`, `Tabs`/`Tab`, `CodeGroup`/`CodeTab`, `TypeTable`/`TypeRow`, `AutoTypeTable`, `ComponentPreview`, `DemoPreview`.
- Render machinery (`[ShellDocsIgnore]`, not reachable from markdown): `MarkdownContent`, `PreviewFrame`.
- Services: `DocsPageState` (scoped), `DocsVersionResolver` and `DocsRedirects` (singletons), plus the redirect middleware.
- `wwwroot/shelldocs.js` and `wwwroot/shelldocs-theme.css`.

### `ShellDocs.Tokens`

One static asset, `tokens.css`: the CSS variables every component reads. See [TOKENS.md](TOKENS.md).

### `ShellDocs.CLI` / `ShellDocs.Templates`

The `shelldocs` global tool and the file content it writes. See [CLI](#cli).

---

## Service registration

`AddShellDocs` (in `ServiceCollectionExtensions.cs`):

1. Builds `ShellDocsOptions` and runs the consumer's `configure` callback.
2. Registers the scoped chrome state (`MobileNavState`, `ThemeState`, `SearchState`, `SidebarCollapseState`, `CodeGroupSyncState`, `DocsPageState`).
3. Adds every component in `ShellDocs.Components.Content` as a built-in.
4. Registers singletons: `TypeRegistry` (built lazily from the options, collisions logged), `MarkdownRenderer`, `NavigationGraph` (empty graph if `ContentRoot` doesn't exist), `SearchIndex`, `DocsVersionResolver`.

Registering components after `AddShellDocs` still works because the registry is built on first resolve.

### Type registry precedence

Built-ins are registered first, each also under a `Docs` alias (`Card` and `DocsCard`); consumer components are registered last. On a tag-name collision the consumer component wins and the built-in stays reachable through its alias. `TypeRegistry.Collisions` records every re-pointed tag; an overridden built-in is logged at information level, two consumer components claiming one tag at warning level. `RegisterComponent<T>("Alias")` registers a type only under the alias.

---

## Markdown pipeline

`MarkdownRenderer.Render`:

1. **Frontmatter** — `FrontmatterParser` splits the YAML block from the body.
2. **Slot extraction** (`SlotExtractor`):
   - Fenced code blocks and inline code spans are masked so tag scanning never sees them.
   - `razor:preview` fences are parsed by `PreviewParser` into a `PreviewSlot` and replaced with a placeholder `<div data-shelldocs-slot="preview" …>`.
   - Inline PascalCase tags that resolve in the `TypeRegistry` become `ComponentSlot`s with their attributes and raw child markup, replaced with a placeholder. Unknown tags pass through as raw markup with a warning.
   - Masks are restored, both in the document and inside each component's child markup.
3. **Markdig** renders the result (pipe/grid tables, auto heading ids, autolinks, task lists, emphasis extras, footnotes, diagrams, media links, soft-line-as-hard-line).
4. `HeadingExtractor` collects headings for the TOC; `CodeBlockEnhancer` wraps each `<pre><code>` with the language badge and copy button.

Placeholders plus a slot list keep everything at render time: no generated Razor source and no build step. `SlotSplitter` later cuts the HTML at the placeholders.

### Attribute parsing

`RazorTagScanner` is a linear, quote-aware tag reader: it keeps full attribute names (`@bind-Value`, `@onclick:preventDefault`) and skips balanced `@( … )` expressions, so values like `OnClick="@(() => x > y)"` don't break the tag.

### `razor:preview` fences

`PreviewParser` turns the fence into ordered nodes: component nodes (registered capitalised tags; their children kept raw and parsed the same way, as Razor, when the component renders), element nodes (HTML, including unregistered capitalised tags) and text. `@code { }` / `@functions { }` blocks, `@* *@` comments and directive lines (`@using`, `@inject`, …) are skipped for rendering but stay in the Code tab. If no registered component is found the slot carries an `Error`, rendered as a red panel instead of a silent code block. `razor:preview stretch` in the info string sets `PreviewSlot.Layout`.

---

## Component rendering

`MarkdownContent` splits a `RenderedDocument` into HTML parts and slots:

- HTML parts render as `MarkupString`.
- `ComponentSlot` → `<DynamicComponent>` with parameters from `SlotRenderer.BuildParameters`.
- `PreviewSlot` → `<PreviewFrame Id="preview-N">`, where `SlotRenderer.RenderNodes` emits elements and components as real render-tree nodes, so wrappers keep their children under interactive re-renders.

**Generic components** register under their bare name (`TypeRegistry.TagNameOf`) as open definitions. `SlotRenderer.Component` closes one per use with `GenericComponents.Close`, reading attributes named after the type parameters (`TItem="int"`, resolved from C# spellings across loaded assemblies) or falling back to `object`; one it can't close renders a `.shelldocs-render-error` element instead. A non-generic component with the same name keeps the tag.

Component child markup is rendered recursively. Inline tags in prose take markdown bodies (`SlotRenderer.FromMarkup`, dedented first, since Markdig treats 4-space indentation as a code block). Inside a `razor:preview` and in `<ComponentPreview>` the body is Razor: `SlotRenderer.FromRazor` parses it with `PreviewParser` and emits real elements, so wrappers around nested components stay intact. Child tags named after a `RenderFragment` parameter (`<Icon>` → `Alert.Icon`) are routed into that slot.

**Parameter coercion.** Attribute values are strings. `BuildParameters` matches them to `[Parameter]` properties case-insensitively and coerces string, bool, char, numeric primitives and enums, accepting Razor forms: a leading `@`, `@( … )`, `Type.Member` enum values, `A | B` flags, numeric suffixes, `@null`. It never throws. Anything static markup can't set is skipped with a logged warning: directive attributes, delegate/`EventCallback` parameters, unsupported types, unparseable values, unknown attributes on components without a `CaptureUnmatchedValues` catch-all, and child content on components without a plain `RenderFragment ChildContent`.

**`PreviewFrame`** is the frame for every example. It carries `not-prose` (every `.shelldocs-prose` rule skips `.not-prose` subtrees) and a `Layout` of `center`, `stretch` or `scroll` (the example scrolls horizontally inside the frame; opt-in because a scroll box clips popovers). Centring uses `safe center`, so an example wider than the frame starts at its left edge. It has a toolbar with Preview | Code tabs, copy and a ⋯ menu (*Open in new tab* → the example's anchor; *Report a bug* / *Suggest something* → new-issue links built by `PreviewLinks` from `IssueTrackerUrl` or `GitHubRepo`). `ComponentPreview` and `DemoPreview` feed it a ready-made `Content` fragment and `Code` string instead of a `PreviewSlot`. `DemoPreview` reads its source from the first `{DemoSourceRoot}/**/X.razor` (shallowest path wins, cached, re-read when the file changes).

**`DocsPageState`** (scoped) is fed by `MarkdownContent` and recomputes current node, prev/next and breadcrumbs on navigation, so the layout chrome needs no per-page wiring.

---

## Navigation graph

`NavigationGraphBuilder.Build(contentRoot)` walks the folder tree:

- Every `.md` file is a page; `index.md` takes the folder's URL. Frontmatter supplies `title` (else derived from the slug), `description`, `category`, `order`.
- Every subfolder is a section titled from its name. Folder names keep their dots (`v0.2.1`).
- Without `meta.json`: subfolders first, then pages by `order` and title.
- With `meta.json`: `pages` sets the order for what it lists: a slug, `"---"` for a divider, or `{ "title", "pages" }` for a subsection. Unlisted files and folders are appended alphabetically, and unknown slugs are ignored. `hidden` entries route but stay out of the sidebar and prev/next.

```json
{
  "title": "Components",
  "pages": ["button", "input", "---", { "title": "Data Display", "pages": ["table", "card"] }],
  "hidden": ["drafts"]
}
```

Runtime queries: `ResolveByUrl` is a case-insensitive dictionary lookup on normalized URLs; prev/next walk the flattened visible page list (optionally restricted to a scope); breadcrumbs walk up `Parent`. The graph is built once at startup; `shelldocs dev` restarts the app when content changes.

### Versions

`ShellDocsOptions.Versions` (`AddVersion(id, label, rootUrl, description, latest)`) maps each docs version to a content folder URL. `DocsVersionResolver` (singleton, stateless — the current path is always passed in) answers every version question the chrome asks:

| Question | Answer |
|---|---|
| Current version | Version whose `RootUrl` is a segment-aware prefix of the path, longest wins → else `latest` → else `Versions[0]` |
| Sidebar nodes | Inside a version: that version folder's children. Outside: the whole tree minus version folders (`IsHiddenInSidebar`) |
| Version switch href | Same relative page in the target version → current package's root there → the target version's first page |
| Package href | `{version}` token replaced with the current version's `Id`; a versioned root that isn't a page lands on its first page |
| Prev/next | Restricted to pages in the same version (or both unversioned) |
| Search | Entries in the current version, plus entries outside every version root |
| Breadcrumb | Version folder node removed |

All answers are computed at render time and emitted as plain `<a href>`, so prerendered pages behave identically.

### Redirects

`DocsRedirects` (singleton) maps URLs that aren't pages to a target, computed once from the graph and versions:

- content folders and version roots → their first page, within the folder's version scope (a folder holding version folders → its first unversioned page, else the latest version's);
- unversioned URLs that exist in the latest version → that page;
- `AddRedirect` rules on top: segment-aware prefixes that may chain into the computed redirects. They are the only thing that can redirect an existing page.

`DocsRedirectMiddleware` runs first in the pipeline (added by an `IStartupFilter`, so no `Program.cs` change) and answers 302 for computed redirects or 301 for permanent rules, keeping `PathBase` and the query string. Running before routing is what makes `/docs/v0.2.1` work. It also serves the full map at `/_shelldocs/redirects.json` for `shelldocs build`. `EnableRedirects = false` leaves it out.

---

## Search

`SearchIndex.FromGraph` runs once at startup and keeps everything in memory: one entry per visible page (title, description, section, plain-text body) and one per h2/h3 heading (headings are read from the source if the page hasn't rendered yet).

`SearchDialog` (Cmd/Ctrl+K, or the header / sidebar buttons) filters the entries to the current version and scores them in .NET: whole-query and per-token matches against title (strongest), section, description and body; every token must match somewhere. Body-only matches get a ~150-character snippet. It's an interactive Blazor component, so search needs a running Blazor app; a static `shelldocs build` site has no search.

---

## Client-side chrome (`shelldocs.js`)

Chrome that must work without a Blazor runtime follows one contract:

- The server renders the full markup with its initial state in data attributes (`data-open`, `data-tabs-value`, `data-preview-tab`, `data-mobile-open`, `aria-selected`, `hidden`). Nothing uses `@onclick` state.
- `shelldocs.js` attaches document-level delegated listeners as soon as it runs (not on `DOMContentLoaded`, which waits for module scripts), so early clicks and enhanced-navigation DOM swaps are both covered.
- When a Blazor circuit starts it replaces the prerendered DOM. A `MutationObserver` re-applies state chosen before then: preview tabs (per page + frame id), `<Tabs>`/`<CodeGroup>` selections, the mobile nav.

| Feature | Markup | Behaviour |
|---|---|---|
| Sidebar sections | `.sidebar-section[data-open]` | toggle on click |
| Package / version selectors, preview ⋯ menu | `.pkg` / `.ver` / `.preview-menu` `[data-open]` | open on trigger; close on outside click, Escape or option |
| Preview tabs | `.preview-frame[data-preview-tab]` | Preview / Code; arrow keys; copy reads the source |
| Tabs, CodeGroup | `[data-tabs]`, `[data-tab-target]`, `[data-tab-panel]` | switch on click / arrows; `[data-tabs-sync]` groups switch together and persist in `localStorage` |
| Mobile nav | page shell `[data-mobile-open]` | hamburger toggles; backdrop, Escape, link or navigation closes; page scroll locked on `<html>` |
| TOC | `[data-toc-list][data-toc-ids]` | IntersectionObserver scroll-spy, re-attached on `enhancedload` |
| Theme | `<html class="dark">`, `[data-theme-toggle]` | inline head script applies the saved / system theme; the toggle flips the class and CSS picks its icon; a `MutationObserver` saves any change (including another library's toggle) and pushes it to `ThemeState`; re-applied after enhanced navigation |

Still Blazor-only: search, the desktop sidebar-collapse button.

`enhancedload` is a Blazor event (`Blazor.addEventListener`), not a DOM event; `shelldocsOnEnhancedLoad(fn)` attaches once `blazor.web.js` has run.

**Syntax highlighting.** Shiki (loaded by the app as `window.__shiki`) never replaces Blazor-owned nodes: the source `<pre>` gets `data-shiki="source"` (hidden by CSS) and the highlighted output goes into a JS-owned `[data-shiki-output]` sibling, re-rendered when the source text changes and removed when its source is gone.

---

## Theming

One neutral, shadcn-shaped palette of CSS variables in `ShellDocs.Tokens/tokens.css` (`:root` and `:root.dark`). Component styles are scoped CSS reading those variables, plus `shelldocs-theme.css` for global prose, code-block and Shiki styling. Restyle by overriding variables in a stylesheet loaded after `tokens.css`. `ShellDocsOptions.Theme` exists but doesn't switch palettes yet.

---

## CLI

- `shelldocs init [path] [--attach] [--dir] [--yes] [--theme]` — create mode runs `dotnet new blazor` (default `docs/<CwdName>.Docs`), adds the packages at the CLI's own version, writes starter content and patches `Program.cs` / `App.razor`. Attach mode augments an existing project and writes `SHELLDOCS_SETUP.md` instead of editing code. Both are idempotent.
- `shelldocs add <component|guide|page> <name> [--force]` — scaffolds a page into `content/docs/`.
- `shelldocs dev [--port 5000]` — `dotnet watch run`, with `content/**/*.md` added to the watch set.
- `shelldocs build [--output publish] [--base-href] [--spa-fallback] [--site-url]` — see below.
- `shelldocs preview` — reserved, not implemented.

### Static site generation

`shelldocs build`:

1. `dotnet publish -c Release` into `obj/shelldocs-publish`, then copies in any `content/` file publish left out (a csproj that copies `meta.json` but not `.md`).
2. Builds the navigation graph and collects every URL (visible and hidden) plus `/`.
3. `PrerenderRunner` starts the published app on a free port, requests each URL and writes `<output>/<path>/index.html`.
4. Reads the app's redirect map and writes a redirect page for each source URL without a prerendered page (meta refresh plus `location.replace`, relative to `<base href>`).
5. Merges the published `wwwroot/` into the output without overwriting prerendered HTML.
6. Optionally rewrites `<base href>` in every HTML file, copies `index.html` to `404.html`, and with `--site-url` writes `sitemap.xml`, `robots.txt` and `og:*` meta.

---

## Testing

`tests/ShellDocs.Tests` (xUnit):

- Unit tests for the graph builder, `meta.json`, frontmatter, search index, URL helpers, versions, the markdown pipeline (slots, previews, code spans, attribute parsing), parameter coercion and the type registry.
- Component render tests: `ComponentRenderHarness` renders components with Blazor's `HtmlRenderer` — the same static HTML a prerendered page ships — and asserts on markup (selectors, sidebar scoping, preview toolbar, tabs, mobile nav).
- CLI tests for `init` (attach mode end-to-end, create-mode patchers against template fixtures), `add` and `build` helpers.

JavaScript behaviour is verified manually in the example app (`examples/ShellDocs.Preview`).

---

## Not building (deliberately)

- **A markdown editor or CMS.** Content is `.md` files in a git repo.
- **A hosted service.** ShellDocs is a NuGet package family; you host the app or its static output.
- **A design system.** ShellDocs ships only the primitives a docs site needs; bring your own component library and register it.
- **i18n**, for now. Content routing is plain folders, so locale-aware resolution can be added later without changing the graph's shape.
