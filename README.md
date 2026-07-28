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

## Package family

| Package | Purpose |
|---|---|
| [`ShellDocs.CLI`](src/ShellDocs.CLI) | Global tool. `shelldocs init`, `add`, `dev`, `build` |
| [`ShellDocs.Components`](src/ShellDocs.Components) | RCL. Chrome (layout, sidebar, header, search) + content primitives (Callout, Card, Steps, CodeGroup, FileTree, TypeTable, ComponentPreview) |
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
