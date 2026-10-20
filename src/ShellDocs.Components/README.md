![ShellDocs](https://raw.githubusercontent.com/shellui-dev/shelldocs/main/assets/readme-logo.svg)

# ShellDocs.Components

The Blazor side of [ShellDocs](https://shelldocs.shellui.dev), the docs framework for .NET: the docs layout and chrome (sidebar, header, search, table of contents, version and package selectors) and the content components you write in Markdown (`Callout`, `Card`, `Steps`, `Tabs`, `CodeGroup`, `FileTree`, `TypeTable`, live component previews).

## Install

The quickest start is the CLI, which scaffolds a site with everything wired up:

```bash
dotnet tool install --global ShellDocs.CLI --prerelease
shelldocs init
```

To add ShellDocs to an existing Blazor Web App:

```bash
dotnet add package ShellDocs.Components --prerelease
```

```csharp
builder.Services.AddShellDocs(o =>
{
    o.ContentRoot = Path.Combine(builder.Environment.ContentRootPath, "content");
    o.SiteName = "My Library";
    o.RegisterComponentsFromAssembly<MyLib.Button>();  // your components, usable in Markdown
});
```

`shelldocs init --attach` does this for you and writes the `App.razor` snippets into `SHELLDOCS_SETUP.md`.

## What you get

- **Markdown-first authoring.** Frontmatter, highlighted code fences, live `razor:preview` examples and component tags mid-prose.
- **File-based navigation.** Each `.md` under `content/docs/` is a page; `meta.json` sets order, dividers, subsections and hidden pages.
- **`Cmd+K` search** with body snippets, searched in the browser.
- **Colour presets** (`o.Theme = ShellDocsTheme.Harbor`), light and dark.
- **Versioned docs, redirects and llms.txt for AI agents.**
- **Works as a static site.** `shelldocs build` exports every page, and the chrome keeps working there through `shelldocs.js`.

## Links

- Docs: [shelldocs.shellui.dev](https://shelldocs.shellui.dev)
- Source, issues and changelog: [github.com/shellui-dev/shelldocs](https://github.com/shellui-dev/shelldocs)

Alpha: APIs may change between minor versions. MIT licensed.
