![ShellDocs](https://raw.githubusercontent.com/shellui-dev/shelldocs/main/assets/readme-logo.svg)

# ShellDocs CLI

`shelldocs` scaffolds, runs and exports [ShellDocs](https://shelldocs.shellui.dev) sites: Markdown-driven documentation for .NET with live Blazor component previews, versioned docs, `Cmd+K` search and a static export.

## Install

The tool needs the .NET 10 runtime:

```bash
dotnet tool install --global ShellDocs.CLI --prerelease
```

## Quick start

```bash
# From your repo root: scaffold a Blazor Web App in docs/<RepoFolder>.Docs
shelldocs init
cd docs/<RepoFolder>.Docs

# Add pages (or drop .md files into content/docs/)
shelldocs add component Button
shelldocs add guide getting-started

# Run with hot reload on http://localhost:5000
shelldocs dev

# Prerender a static site into publish/, then serve it as a static host would
shelldocs build
shelldocs preview
```

Already have a Blazor project? Run `shelldocs init --attach` inside it: it adds the packages and content, and writes `SHELLDOCS_SETUP.md` with the `Program.cs` / `App.razor` snippets instead of editing your code.

## Commands

| Command | What it does |
|---|---|
| `shelldocs init` | Scaffolds a docs site, or adds ShellDocs to an existing Blazor project with `--attach` |
| `shelldocs add` | Adds a page from a template (`component`, `guide`, …) |
| `shelldocs dev` | Runs the site with hot reload |
| `shelldocs build` | Prerenders every page to static HTML for GitHub Pages, Cloudflare Pages, Netlify or S3. `--base-href` for a sub-path, `--site-url` for sitemap, robots and `og:` meta |
| `shelldocs preview` | Serves the export on `http://localhost:5050` as a static host would, to check it before deploying |

## Links

- Docs: [shelldocs.shellui.dev](https://shelldocs.shellui.dev)
- Source, issues and changelog: [github.com/shellui-dev/shelldocs](https://github.com/shellui-dev/shelldocs)

Alpha: APIs may change between minor versions. MIT licensed.
