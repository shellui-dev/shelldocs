---
title: Installation
description: Get a ShellDocs site running in five minutes.
order: 2
---

# Installation

Get from empty to a live docs site in a few minutes.

## Requirements

- .NET 10 SDK or later

## Create a site

Install the CLI (it's a prerelease, hence `--prerelease`), then scaffold a site from your repo root:

```bash
dotnet tool install -g ShellDocs.CLI --prerelease
shelldocs init
```

`shelldocs init` creates a Blazor Web App in `docs/<RepoFolder>.Docs`, adds the ShellDocs packages, writes starter content under `content/`, and wires `Program.cs` and `App.razor`. Pass a path to put it elsewhere (`shelldocs init site/docs`).

Already have a Blazor project? Run `shelldocs init --attach` inside it instead. It adds the packages and content, and writes a `SHELLDOCS_SETUP.md` with the `Program.cs` / `App.razor` snippets to paste, rather than editing your files.

## Author pages

From the docs project folder, scaffold pages from a template, or just drop `.md` files into `content/docs/`:

```bash
shelldocs add page faq
shelldocs add guide getting-started
shelldocs add component Button
```

## Run

```bash
shelldocs dev
```

The site runs at `http://localhost:5000` (change it with `--port`), with hot reload for `.razor`, `.cs` and `.md` changes.

## Ship

```bash
shelldocs build
```

`shelldocs build` prerenders every page to static HTML in `publish/` (change it with `--output`). Deploy it to GitHub Pages, Cloudflare Pages, Netlify or S3. Useful flags:

- `--base-href /my-repo/` rewrites `<base href>` for sites served from a subpath.
- `--spa-fallback` copies `index.html` to `404.html`.
- `--site-url https://docs.example.com` emits `sitemap.xml`, `robots.txt` and `og:` meta tags.

In the static output, navigation, sidebar sections, the mobile menu, the version and package selectors, tabs, preview tabs and menus, the table of contents, the theme toggle and code copy all work through `shelldocs.js`. Search, the desktop sidebar-collapse button and stateful demo components still need a running Blazor app (Server or WebAssembly).
