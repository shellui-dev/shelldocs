---
title: Installation
description: Get a ShellDocs site running in five minutes.
order: 2
---

# Installation

Get from empty to a live docs site in under five minutes.

## Requirements

- .NET 10 SDK or later
- A Blazor WebAssembly project (or Blazor Server for now)

## Bootstrap

Create a new Blazor project and install the CLI:

```bash
dotnet new blazorwasm -n MyDocs
cd MyDocs
dotnet tool install -g ShellDocs.CLI
```

Initialize ShellDocs — this adds package references, generates `content/`, and patches `Program.cs`:

```bash
shelldocs init
```

## Author your first page

Author content in Markdown under `content/`:

```bash
shelldocs new page introduction
```

## Run

```bash
shelldocs dev
```

Your docs site opens at `http://localhost:5000`.

## Ship

```bash
shelldocs build
```

The static site output goes to `publish/`. Deploy anywhere — GitHub Pages, Vercel, Netlify, Cloudflare Pages.
