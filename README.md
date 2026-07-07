# ShellDocs

**The docs framework for .NET.** Beautiful, animated, Cmd+K-searchable documentation sites, powered by Blazor and Tailwind. Compose with ShellUI (or any Blazor component library) — like fumadocs composes with shadcn/ui.

> Status: **`0.1.0-alpha` in progress.** Not yet published to NuGet. See [ROADMAP](docs/ROADMAP.md).

## Why ShellDocs

Every .NET UI library ends up hand-rolling their own docs site. MudBlazor, Radzen, AvaloniaUI — each spent months rebuilding a sidebar, a search box, a code block, a theme toggle, from scratch. None of it is reusable.

ShellDocs is the "just use this" answer. It's the docs framework for the whole .NET ecosystem.

- **Markdown authoring** with YAML frontmatter, inline Razor tags, and live component previews (`` ```razor:preview ``)
- **File-based routing** — drop a `.md` in `content/` and it's a page
- **Cmd+K search** with a build-time client-side index — no backend needed
- **Blazor-native** — components render as real Razor, not iframes, not screenshots
- **Composable with any Blazor component library** — ShellUI, MudBlazor, Radzen, your own
- **Tailwind CSS v4** — same aesthetic as ShellUI + shadcn, same theme tokens for interop
- **Animated** — page transitions, sidebar collapses, scroll-spy, `prefers-reduced-motion` aware
- **Static site output** — deploy to GitHub Pages, Vercel, Netlify, Cloudflare, anywhere

## Quick start

```bash
# Create a new Blazor WASM app
dotnet new blazorwasm -n MyDocs
cd MyDocs

# Install the ShellDocs CLI
dotnet tool install -g ShellDocs.CLI

# Initialize the docs site
shelldocs init

# Author content in Markdown
shelldocs new page introduction

# Develop with hot-reload
shelldocs dev

# Ship it
shelldocs build
```

## Coexists with ShellUI (and any Blazor UI library)

ShellDocs uses the same Tailwind v4 setup and CSS variable contract as ShellUI. Both libraries share the same theme tokens (`--background`, `--foreground`, `--primary`, `--border`, `--radius`, etc.), so you can drop them into the same page and they compose seamlessly — the fumadocs + shadcn pattern, ported to .NET.

```razor
@* Your docs page — ShellUI components inline in Markdown *@
<Button Variant="ButtonVariant.Default">A ShellUI button</Button>
<Callout Type="Tip">A ShellDocs callout</Callout>
```

Under the hood ShellDocs takes a hard dependency on `ShellUI.Components` for base primitives (`Button`, `Dialog`, `Command`, `Sidebar`, etc.). Zero style clash.

## Package family

| Package | Purpose |
|---|---|
| [`ShellDocs.CLI`](src/ShellDocs.CLI) | Global tool — `shelldocs init`, `shelldocs new`, `shelldocs dev`, `shelldocs build` |
| [`ShellDocs.Components`](src/ShellDocs.Components) | RCL — `DocsLayout`, `DocsSidebar`, `CodeBlock`, `SearchDialog`, `TableOfContents`, etc. |
| [`ShellDocs.Markdown`](src/ShellDocs.Markdown) | Markdig pipeline — frontmatter, `razor:preview` fences, inline Razor tags |
| [`ShellDocs.Core`](src/ShellDocs.Core) | Navigation graph, search index model, routing helpers. Blazor-agnostic. |
| [`ShellDocs.Templates`](src/ShellDocs.Templates) | Content used by `ShellDocs.CLI` scaffolding |

Optional / v2:

- **`ShellDocs.Xml`** — extract `<TypeTable>` from XML doc comments
- **`ShellDocs.Themes.Fuma`**, **`ShellDocs.Themes.Nextra`** — theme presets
- **`ShellDocs.OpenApi`** — OpenAPI spec → API reference pages

## Documentation

- [Design](docs/DESIGN.md) — what ShellDocs is, positioning, primitives, ecosystem story
- [Roadmap](docs/ROADMAP.md) — branch-by-branch implementation plan
- [Architecture](docs/ARCHITECTURE.md) — technical architecture: package boundaries, service registration, markdown pipeline, navigation graph, search index

Once we ship `0.2.0-alpha`, official docs will live at **[shelldocs.dev](https://shelldocs.dev)** (dogfooded on ShellDocs itself).

## Related projects

- [ShellUI](https://github.com/shellui-dev/shellui) — the Blazor component library ShellDocs is built with
- [shellui.dev](https://github.com/shellui-dev/shellui.dev) *(coming soon)* — ShellUI's own docs site, built with ShellDocs

## Contributing

`0.1.0-alpha` is scaffolding-first — architecture and API surface are still moving. Once we hit `0.2.0-alpha`, we'll open up contributions with a proper `CONTRIBUTING.md`.

## License

[MIT](LICENSE) — do whatever you want, no warranty.
