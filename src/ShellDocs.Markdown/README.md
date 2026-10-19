![ShellDocs](https://raw.githubusercontent.com/shellui-dev/shelldocs/main/assets/readme-logo.svg)

# ShellDocs.Markdown

The Markdig pipeline behind [ShellDocs](https://shelldocs.shellui.dev), the docs framework for .NET: frontmatter, `razor:preview` fences and Razor component tags written inline in Markdown, turned into slots that `ShellDocs.Components` renders as real Blazor components.

You don't usually reference this package directly: `ShellDocs.Components` brings it in. Start with the CLI:

```bash
dotnet tool install --global ShellDocs.CLI --prerelease
shelldocs init
```

## Links

- Docs: [shelldocs.shellui.dev](https://shelldocs.shellui.dev)
- Source, issues and changelog: [github.com/shellui-dev/shelldocs](https://github.com/shellui-dev/shelldocs)

Alpha: APIs may change between minor versions. MIT licensed.
