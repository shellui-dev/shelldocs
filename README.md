<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/shellui-dev/shelldocs/main/assets/shelldocs-dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/shellui-dev/shelldocs/main/assets/shelldocs-light.svg">
    <img alt="ShellDocs logo" src="https://raw.githubusercontent.com/shellui-dev/shelldocs/main/assets/shelldocs-light.svg" width="120">
  </picture>
</p>

<h1 align="center">ShellDocs</h1>

<p align="center">
  <b>The docs framework for .NET.</b> Markdown-driven documentation sites with live Blazor component previews, versioned docs, <code>Cmd+K</code> search, and a static export.<br/>
  Styled with shadcn-shaped design tokens and composable with any Blazor component library. The fumadocs / shadcn pattern, ported to .NET.
</p>

<p align="center">
  <a href="https://github.com/shellui-dev/shelldocs"><img src="https://img.shields.io/github/stars/shellui-dev/shelldocs?style=flat-square" alt="GitHub stars"></a>
  <a href="https://www.nuget.org/packages/ShellDocs.Components"><img src="https://img.shields.io/nuget/vpre/ShellDocs.Components?style=flat-square&logo=nuget&color=004880" alt="ShellDocs.Components on NuGet"></a>
  <a href="https://www.nuget.org/packages/ShellDocs.CLI"><img src="https://img.shields.io/nuget/vpre/ShellDocs.CLI?style=flat-square&logo=nuget&label=CLI&color=004880" alt="ShellDocs.CLI on NuGet"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="MIT license"></a>
</p>

Alpha: APIs may change between minor versions. See the [CHANGELOG](CHANGELOG.md) and [ROADMAP](docs/ROADMAP.md).

## Quick start

```bash
# Install the CLI (once)
dotnet tool install -g ShellDocs.CLI --prerelease

# From your repo root: scaffold a Blazor Web App in docs/<RepoFolder>.Docs
shelldocs init
cd docs/<RepoFolder>.Docs

# Add pages (or drop .md files into content/docs/)
shelldocs add component Button
shelldocs add guide getting-started

# Run with hot reload on http://localhost:5000
shelldocs dev

# Prerender a static site into publish/
shelldocs build
```

Already have a Blazor project? Run `shelldocs init --attach` inside it: it adds the packages and content, and writes `SHELLDOCS_SETUP.md` with the `Program.cs` / `App.razor` snippets instead of editing your code.

## What you get

- **Markdown-first authoring.** YAML frontmatter, Shiki-highlighted code fences, live `razor:preview` examples, inline component tags mid-prose. Set `o.RenderPageTitle = true` to render the frontmatter `title` and `description` as the page header instead of writing `# Title`.
- **File-based navigation.** Drop a `.md` in `content/docs/` and it's a page. Sidebar, breadcrumb, prev/next and TOC come from the folder tree; `meta.json` controls order, dividers, subsections and hidden pages.
- **`Cmd+K` search** across titles, headings and page text, with body snippets. The index is built in memory at startup, so there's no external service; search needs a running Blazor app (it isn't available in the static export).
- **Blazor-native.** Your components render as real Razor components, not iframes.
- **Composable.** Bring your own component library (ShellUI, MudBlazor, Radzen, hand-rolled) and register it in one line:
  ```csharp
  o.RegisterComponentsFromAssembly<MyLib.Button>();
  ```
  Your components win name collisions with ShellDocs' built-ins, which stay available as `<DocsCard>`, `<DocsCallout>`, `<DocsTabs>`, and so on.
- **Content primitives.** `Callout`, `Card` / `CardGrid` / `LinkCard`, `Steps`, `FileTree`, `Tabs`, `CodeGroup`, `TypeTable` / `AutoTypeTable`, `ComponentPreview`, `DemoPreview`.
- **Static export.** `shelldocs build` prerenders every page to static HTML for GitHub Pages, Cloudflare Pages, Netlify or S3. Optional flags rewrite `<base href>` (`--base-href`), add a SPA `404.html` (`--spa-fallback`), and write sitemap / robots / `og:` meta (`--site-url`). Navigation, sidebar sections and collapse, the mobile menu, selectors, tabs, preview toolbars, the TOC, code copy and the theme toggle work there through `shelldocs.js`. Only search and stateful demos need a running Blazor app. Unknown pages answer 404 (`<DocsNotFound />` in the docs page) and `build` writes the same view as `404.html`.

## Versioned docs

Put each version in its own content folder with the same sub-structure (`content/docs/v0.3/...`, `content/docs/v0.2.1/...`; dots in folder names are fine) and register them:

```csharp
o.AddVersion("v0.3",   "v0.3.0", "/docs/v0.3",   "Current stable", latest: true);
o.AddVersion("v0.2.1", "v0.2.1", "/docs/v0.2.1", "Previous release");

// "{version}" in a package root resolves to the current version's Id.
o.AddPackage("shellui.cli", "ShellUI.CLI", "Command line", "/docs/{version}/cli", icon);
```

The current version is the one whose `RootUrl` prefixes the path (segment-aware), otherwise the `latest` one. With 2+ versions a `<VersionSelector />` renders under the package selector (and in the mobile drawer; in the `TopNav` layout it sits in the header).

- **Switching versions** keeps the same page when it exists, else the current package's root, else the version's first page.
- **Switching packages** keeps the version.
- **Inside a version**, the sidebar shows only that version's tree, prev/next never crosses into another version, and search shows the current version plus pages outside every version.
- **Outside a version**, the sidebar leaves the version folders to the selector.
- **Breadcrumbs** leave out the version folder.

It's all server-rendered links plus `shelldocs.js`, so it works on static hosts.

### Redirects

URLs that aren't pages redirect instead of 404ing: content folders and version roots go to their first page (`/docs/v0.2.1` → `/docs/v0.2.1/introduction`; `/docs` prefers unversioned pages, then the latest version), and unversioned URLs that exist in the latest version go there (`/docs/cli/dev` → `/docs/v0.3/cli/dev`). Add rules for moved content:

```csharp
o.AddRedirect("/docs/v0.3.0", "/docs/v0.3");   // also /docs/v0.3.0/x → /docs/v0.3/x (301)
```

`AddShellDocs` registers middleware that answers these before routing, so a dotted version root works even though `/docs/{*Path:nonfile}` routes can't match it. `shelldocs build` writes the same redirects as static pages. Turn it off with `o.EnableRedirects = false`.

## Component previews

- **Preview | Code toolbar on every example**, with copy and a ⋯ menu: *Open in new tab*, *Report a bug*, *Suggest something*. The issue links go to `https://github.com/{GitHubRepo}/issues/new`, pre-filled with the example and page URL; point them elsewhere with `o.IssueTrackerUrl`.
- **`razor:preview` fences render everything.** Several sibling components, HTML wrappers (`<div class="flex gap-2">…</div>`) and text render in order inside one frame, and the Code tab shows the whole fence.
- **Child content is Razor.** Component bodies inside a fence are parsed like the fence itself, so `<Navbar><div>…<ThemeToggle /></div></Navbar>` keeps its structure and nothing is wrapped in `<p>`.
- **Page typography stays out.** Preview frames are `not-prose`, so prose margins, list padding and link underlines don't reach your components.
- **Generic components work.** Set the type argument as Razor does: `<BarChart TItem="SalesRow" />`, `<DataTable TItem="int" />`. Libraries' generic components are registered under their bare name.
- **Code variants.** Give `<DemoPreview>`, `<ComponentPreview>` or `<IframePreview>` `<CodeTab Label="MAUI">` children with a fence inside, and the Code panel shows those tabs instead of the Razor (for a library documented in MAUI and Avalonia XAML, say). `SyncKey="platform"` switches every block together and remembers the choice; `o.AddSyncSwitch("platform", "MAUI", "Avalonia")` adds a shadcn-style tab row to those pages; a block missing the chosen tab says "Not available on Avalonia yet". `Caption="Rendered with Blazor"` adds a note to the toolbar.
- **Live iframe previews.** `<IframePreview Src="/preview/?c=button">` shows another page, such as Avalonia on WebAssembly, loaded lazily with `?theme=` and theme-change messages.
- **Centred, stretched or scrolling.** Examples are centred; `razor:preview stretch` (or `Layout="stretch"` on `<DemoPreview>` / `<ComponentPreview>`) lets charts, inputs and tables fill the frame, and `scroll` lets an example wider than a phone screen (pagination, toolbars, OTP inputs) scroll inside the frame. A scrolling frame clips popovers, so leave it off examples with dropdowns.
- **Razor-shaped attribute values work:** `Variant="ButtonVariant.Destructive"`, `@ButtonVariant.Destructive`, `@true`, `@42`, `[Flags]` values as `Bold | Italic`.
- **Attributes a static preview can't evaluate are skipped, not fatal:** `OnClick="HandleClick"`, `@onclick`, `@bind-*`, `@ref`, non-primitive parameter types, unparseable values. Each logs a warning and the component still renders.
- **Inline code stays code.** `` `<Button>` `` in prose renders as literal code, not a component.
- **Stateful demos from real files.** For demos that need `@code` (dialogs, bound selects, toasts, charts with data), write a `.razor` component, register it, and drop `<DemoPreview Component="ButtonClickDemo" Title="Optional" />` into markdown. The Code tab shows `{DemoSourceRoot}/**/ButtonClickDemo.razor`:

  ```csharp
  o.RegisterComponentsFromAssembly<App>("MyDocs.Demos");
  o.DemoSourceRoot = Path.Combine(builder.Environment.ContentRootPath, "Demos");
  ```

  The Razor SDK drops `.razor` files from build/publish output, so ship them explicitly. Use a `None` item, because `Content Update` doesn't survive publish:

  ```xml
  <None Include="Demos/**/*.razor" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  ```

## Package family

| Package | Purpose |
|---|---|
| [`ShellDocs.CLI`](src/ShellDocs.CLI) | Global tool: `shelldocs init`, `add`, `dev`, `build` |
| [`ShellDocs.Components`](src/ShellDocs.Components) | Razor class library: layouts and chrome (sidebar, header, search, TOC, version / package selectors) and the content primitives. Icons via [ShellIcons.Blazor](https://www.nuget.org/packages/ShellIcons.Blazor) |
| [`ShellDocs.Markdown`](src/ShellDocs.Markdown) | Markdig pipeline: frontmatter, `razor:preview` fences, inline component tags, the type registry |
| [`ShellDocs.Core`](src/ShellDocs.Core) | Navigation graph, `meta.json`, search index, URL helpers. No UI |
| [`ShellDocs.Tokens`](src/ShellDocs.Tokens) | Design-token CSS variables, shadcn-compatible names for interop with ShellUI and Tailwind-shaped design systems |
| [`ShellDocs.Templates`](src/ShellDocs.Templates) | Files and snippets emitted by `shelldocs init` and `shelldocs add` |

## Docs

- [shelldocs.dev](https://shelldocs.dev) : full documentation site (built with ShellDocs itself)
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) : how it works: packages, pipeline, rendering, navigation, search, client-side chrome, CLI
- [docs/TOKENS.md](docs/TOKENS.md) : the design-token contract and how to override it
- [docs/DESIGN.md](docs/DESIGN.md) : original product design and positioning
- [docs/ROADMAP.md](docs/ROADMAP.md) : branch-by-branch plan and what has shipped
- [docs/RELEASING.md](docs/RELEASING.md) : how a maintainer cuts a NuGet release (Trusted Publishing)
- [examples/ShellDocs.Preview](examples/ShellDocs.Preview) : the example site used for development, with versions, previews and demos

## Related

- [shellui-dev/shellui](https://github.com/shellui-dev/shellui) : the Blazor component library ShellDocs' authors are building alongside
- [shellui-dev/shelldocs-docs](https://github.com/shellui-dev/shelldocs-docs) : source for [shelldocs.dev](https://shelldocs.dev), consuming ShellDocs from NuGet like any other user

## Contributing

The alpha is API-fluid: we're taking freedom to break minor versions until `1.0`. Bug reports and dogfood-driven fixes are welcome via issues. A proper `CONTRIBUTING.md` lands with the `0.2.0-alpha` cut.

## License

[MIT](LICENSE). Do whatever you want, no warranty.
