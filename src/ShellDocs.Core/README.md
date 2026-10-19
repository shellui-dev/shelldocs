![ShellDocs](https://raw.githubusercontent.com/shellui-dev/shelldocs/main/assets/readme-logo.svg)

# ShellDocs.Core

The content model behind [ShellDocs](https://shelldocs.shellui.dev), the docs framework for .NET: the navigation graph built from `content/docs` and its `meta.json` files, frontmatter parsing, the search index and URL helpers.

You don't usually reference this package directly: `ShellDocs.Components` brings it in. Start with the CLI:

```bash
dotnet tool install --global ShellDocs.CLI --prerelease
shelldocs init
```

## Links

- Docs: [shelldocs.shellui.dev](https://shelldocs.shellui.dev)
- Source, issues and changelog: [github.com/shellui-dev/shelldocs](https://github.com/shellui-dev/shelldocs)

Alpha: APIs may change between minor versions. MIT licensed.
