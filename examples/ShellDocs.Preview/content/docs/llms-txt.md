---
title: llms.txt for AI agents
description: Publish your docs for Claude Code, Cursor, Copilot and other agents as llms.txt, Markdown pages and agent instruction files.
---

# llms.txt for AI agents

`EnableLlmsTxt` publishes a site's docs in the [llms.txt](https://llmstxt.org) format, so a coding agent can read every page as Markdown instead of scraping HTML. It also serves instruction files an agent reads at startup, and adds a page menu with **Copy page**, **View as Markdown**, **Open in Claude** and **Open in ChatGPT** next to the title.

## Turn it on

The ShellUI docs at [shellui.dev](https://shellui.dev) turn it on like this:

```csharp
builder.Services.AddShellDocs(o =>
{
    o.SiteName = "ShellUI";
    o.SiteUrl = "https://shellui.dev";
    o.EnableLlmsTxt(l =>
    {
        l.Summary = "ShellUI is a shadcn/ui-style component library for Blazor.";
        l.RulesFile = "Llms/agent-rules.md";
        l.InstallCommandPattern = @"shellui add [\w-]+";
    });
});
```

| Option | Default | What it does |
|--------|---------|--------------|
| `Summary` | `SiteTagline` | The `>` line under the site name in `llms.txt`. |
| `RulesFile` | none | A Markdown file shown at the top of `llms.txt` and in the agent files, such as a "Rules for AI agents" list. A relative path is resolved against the app's content root; copy it to the output and publish folders. |
| `InstallCommandPattern` | none | A regex looked for in each page's code blocks; the first match is listed after the page in `llms.txt` as `Install: ...`. |
| `AgentFiles` | `true` | Serves `AGENTS.md`, `CLAUDE.md`, `copilot-instructions.md` and a Cursor rule. |
| `PageActions` | `true` | The page menu next to the title. |

Links in the files are absolute: `SiteUrl` when it's set, otherwise the address the request came in on.

## What agents can read

| URL | Contents |
|-----|----------|
| [`/llms.txt`](/llms.txt) | The site name, summary and rules, then every page with its description, grouped by sidebar section |
| [`/llms-full.txt`](/llms-full.txt) | The same header, then every page as Markdown in one file |
| Any page + `.md` | One page as Markdown, for example [`/docs/installation.md`](/docs/installation.md) |
| `{version root}/llms.txt`, `/llms-full.txt` | One version's pages, for example [`/docs/v1.9.1/llms.txt`](/docs/v1.9.1/llms.txt) |
| `/agents/{file}`, `{version root}/agents/{file}` | The agent instruction files |

The bare `/llms.txt` covers the pages outside every version plus the latest version. On a site without versions it covers every page.

In the Markdown, `razor:preview` fences and `<DemoPreview>` become the example's source, `<Callout>` a blockquote, `<Tabs>` a bold label per tab, cards a list, `<Steps>` numbered steps, `<TypeTable>` a table, and other components their text. Links to docs pages point at their `.md`.

## Set up an agent

Each instruction file is a `## {SiteName}` section that links `llms.txt` and includes the rules, so it can be appended to a file the project already has.

<DocsTabs>
<DocsTab Label="Claude Code">

Claude Code reads `CLAUDE.md`. [Download CLAUDE.md](/agents/CLAUDE.md), or from the project root:

```bash
curl -fsSL https://shelldocs.shellui.dev/agents/CLAUDE.md >> CLAUDE.md
```

</DocsTab>
<DocsTab Label="Cursor">

Cursor reads rules from `.cursor/rules`. [Download the rule](/agents/shelldocs.mdc), named after the site, into that folder:

```bash
curl -fsSL --create-dirs -o .cursor/rules/shelldocs.mdc https://shelldocs.shellui.dev/agents/shelldocs.mdc
```

</DocsTab>
<DocsTab Label="GitHub Copilot">

Copilot reads `.github/copilot-instructions.md`. [Download copilot-instructions.md](/agents/copilot-instructions.md):

```bash
mkdir -p .github && curl -fsSL https://shelldocs.shellui.dev/agents/copilot-instructions.md >> .github/copilot-instructions.md
```

</DocsTab>
<DocsTab Label="Other agents">

Codex and many other agents read `AGENTS.md`. [Download AGENTS.md](/agents/AGENTS.md):

```bash
curl -fsSL https://shelldocs.shellui.dev/agents/AGENTS.md >> AGENTS.md
```

</DocsTab>
</DocsTabs>

## Static sites

`shelldocs build` writes `llms.txt`, `llms-full.txt`, every page's `.md` and the agent files into the export. Their links use `--site-url`, then `SiteUrl`; with neither, they are root-relative (under `--base-href` when it's set).

<DocsCallout Variant="warning" Title="Static hosts pick the content type">

The running app serves the files as `text/plain` and the agent files as downloads. A static host uses its own types: many send `.md` as `text/markdown`, which some browsers download instead of showing. Agents read either.

</DocsCallout>

Links to `.txt` and `.md` files on the site load the file, in the running app and in the export, rather than going through the docs router.
