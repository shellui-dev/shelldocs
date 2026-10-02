# ShellDocs

**The docs framework for .NET.** Beautiful, animated, `Cmd+K`-searchable documentation sites. Powered by Blazor, styled with Tailwind-shaped design tokens, composable with any Blazor component library. The fumadocs / shadcn pattern, ported to .NET.

> `0.1.2-alpha` on nuget.org. See [CHANGELOG](CHANGELOG.md) and [ROADMAP](docs/ROADMAP.md). Docs at [shelldocs.dev](https://shelldocs.dev).

## Quick start

```bash
# Install the CLI (once)
dotnet tool install -g ShellDocs.CLI --prerelease

# Scaffold a site (creates docs/MyDocs.Docs/)
shelldocs init MyDocs

# Add pages
shelldocs add component Button
shelldocs add guide getting-started

# Run with hot reload
cd docs/MyDocs.Docs
shelldocs dev

# Ship
shelldocs build --output publish
```

That's a working docs site. See [shelldocs.dev/docs/getting-started/quick-start](https://shelldocs.dev/docs/getting-started/quick-start) for the walkthrough.

## What you get

- **Markdown-first authoring.** YAML frontmatter, fenced code blocks with Shiki, live-rendered `razor:preview` examples, inline Razor component tags mid-prose.
- **Auto-wired navigation.** File-based routing. Drop a `.md` in `content/docs/` and it becomes a page. Sidebar, breadcrumb, prev/next, TOC — all derived from the tree.
- **`Cmd+K` search.** Client-side substring scoring against title, description, section, and body text. Snippet extraction for body-only hits. Zero backend, zero external service.
- **Blazor-native.** Components render as real Razor. Full JS interop, hot reload, all the tooling you already have.
- **Composable.** Bring your own component library (ShellUI, MudBlazor, Radzen, hand-rolled). One-line assembly-scan registration:
  ```csharp
  o.RegisterComponentsFromAssembly<MyLib.Button>();
  ```
- **Static site output.** `shelldocs build` produces static HTML ready for GitHub Pages, Vercel, Netlify, Cloudflare, anywhere. Base-href rewrite + SPA 404 fallback included.

## Versioned docs

Put each version in its own content folder with the same sub-structure (`content/docs/v0.3/...`, `content/docs/v0.2.1/...`; dots in folder names are fine) and register them:

```csharp
o.AddVersion("v0.3",   "v0.3.0", "/docs/v0.3",   "Current stable", latest: true);
o.AddVersion("v0.2.1", "v0.2.1", "/docs/v0.2.1", "Previous release");

// "{version}" in a package root resolves to the current version's Id.
o.AddPackage("shellui.cli", "ShellUI.CLI", "Command line", "/docs/{version}/cli", icon);
```

The current version is the one whose `RootUrl` prefixes the path (segment-aware), otherwise the `latest` one. With 2+ versions a `<VersionSelector />` renders under the package selector (and in the mobile drawer; in the `TopNav` layout it sits in the header). Switching versions keeps the same page when it exists, else the current package's root, else the version's first page. Switching packages keeps the version. Inside a version the sidebar shows only that version's tree, prev/next never crosses into another version, search shows the current version plus pages outside every version, and the version folder is left out of the breadcrumb. Everything is server-rendered `<a href>`s plus `shelldocs.js` delegation, so it works on static hosts.

> Routes declared as `/docs/{*Path:nonfile}` don't match a URL whose **last** segment has a dot (`/docs/v0.2.1`). Pages below it (`/docs/v0.2.1/introduction`) are fine, and the selectors never link to a bare version root unless it has an `index.md`. If you add one, drop `:nonfile` from the route.

## Component previews

- **`razor:preview` fences render everything** — several sibling components, HTML wrappers (`<div class="flex gap-2">…</div>`) and text, in order, inside one frame. The source tab shows the whole fence.
- **Razor-shaped attribute values work:** `Variant="ButtonVariant.Destructive"`, `@ButtonVariant.Destructive`, `@true`, `@42`, `[Flags]` values as `Bold | Italic`.
- **Attributes a static preview can't evaluate are skipped, not fatal:** `OnClick="HandleClick"`, `@onclick`, `@bind-*`, `@ref`, non-primitive parameter types, unparseable values. Each logs a warning and the component still renders.
- **Stateful demos from real files.** For demos that need `@code` (dialogs, bound selects, toasts, charts with data), write a `.razor` component, register it, and drop `<DemoPreview Component="ButtonClickDemo" Title="Optional" />` into markdown. The source tab shows `{DemoSourceRoot}/**/ButtonClickDemo.razor`:

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
| [`ShellDocs.CLI`](src/ShellDocs.CLI) | Global tool. `shelldocs init`, `add`, `dev`, `build` |
| [`ShellDocs.Components`](src/ShellDocs.Components) | RCL. Chrome (layout, sidebar, header, search, version/package selectors) + content primitives (Callout, Card, Steps, CodeGroup, FileTree, TypeTable, ComponentPreview, DemoPreview). Icons via [ShellIcons.Blazor](https://www.nuget.org/packages/ShellIcons.Blazor) |
| [`ShellDocs.Markdown`](src/ShellDocs.Markdown) | Markdig pipeline. Frontmatter parser, `razor:preview` fence extractor, inline Razor tag extractor, per-property type coercion |
| [`ShellDocs.Core`](src/ShellDocs.Core) | Navigation graph, search index, plain-text extraction. No UI |
| [`ShellDocs.Tokens`](src/ShellDocs.Tokens) | Design-system CSS variables. shadcn-compatible names for interop with ShellUI and Tailwind-shaped design systems |
| [`ShellDocs.Templates`](src/ShellDocs.Templates) | Starter markdown + Program.cs snippets emitted by `shelldocs init` |

## Docs

- [shelldocs.dev](https://shelldocs.dev) : full documentation site (built with ShellDocs itself)
- [docs/DESIGN.md](docs/DESIGN.md) : product positioning, primitive inventory, ecosystem story
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) : package boundaries, service registration, markdown pipeline, navigation graph, search
- [docs/ROADMAP.md](docs/ROADMAP.md) : branch-by-branch delivery plan
- [docs/RELEASING.md](docs/RELEASING.md) : how a maintainer cuts a NuGet release (Trusted Publishing)

## Related

- [shellui-dev/shellui](https://github.com/shellui-dev/shellui) : the Blazor component library ShellDocs' authors are building alongside
- [shellui-dev/shelldocs-docs](https://github.com/shellui-dev/shelldocs-docs) : source for [shelldocs.dev](https://shelldocs.dev), consuming ShellDocs from NuGet like any other user

## Contributing

The alpha is API-fluid : we're taking freedom to break minor versions until `1.0`. Bug reports and dogfood-driven fixes welcome via issues. A proper `CONTRIBUTING.md` lands with the `0.2.0-alpha` cut.

## License

[MIT](LICENSE). Do whatever you want, no warranty.
