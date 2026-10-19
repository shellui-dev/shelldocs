![ShellDocs](https://raw.githubusercontent.com/shellui-dev/shelldocs/main/assets/readme-logo.svg)

# ShellDocs.Tokens

The design tokens of [ShellDocs](https://shelldocs.shellui.dev), the docs framework for .NET: CSS variables on `:root` for palette, radius, spacing and typography, in light and dark, named the shadcn way (`--background`, `--foreground`, `--primary`, `--muted`, `--border`, …).

`ShellDocs.Components` uses them, and any Blazor UI library that reads the same variables (ShellUI, or your own) matches the docs chrome without extra work. Besides the neutral default (Graphite) there are six colour presets, Harbor, Lagoon, Forest, Savanna, Ember and Jacaranda, picked with `o.Theme` in `ShellDocs.Components`.

```bash
dotnet add package ShellDocs.Tokens --prerelease
```

```html
<link rel="stylesheet" href="_content/ShellDocs.Tokens/tokens.css" />
```

## Links

- Docs: [shelldocs.shellui.dev](https://shelldocs.shellui.dev)
- Source, issues and changelog: [github.com/shellui-dev/shelldocs](https://github.com/shellui-dev/shelldocs)

Alpha: APIs may change between minor versions. MIT licensed.
