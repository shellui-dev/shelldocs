namespace ShellDocs.Templates;

/* Templates emitted by `shelldocs init`. Kept as raw string constants so the
   CLI has zero I/O overhead — the templates ARE the payload. */
public static class ScaffoldTemplates
{
    public static string IntroductionMd => """
        ---
        title: Introduction
        description: Get started with your ShellDocs site.
        order: 1
        ---

        # Introduction

        Welcome to your new ShellDocs site. Author markdown in `content/docs/`, drop Blazor components mid-page, ship.

        ## What's next

        - Edit this file at `content/docs/introduction.md`
        - Add pages by creating more `.md` files in the same folder
        - Order them via `content/docs/meta.json`
        - Register components you want available inline via `RegisterComponent<T>()` in `Program.cs`

        ## Live components

        ```razor:preview
        <Callout Title="Nice" Text="This block is a live Blazor component rendered from markdown." />
        ```
        """;

    public static string MetaJson => """
        {
          "title": "Docs",
          "pages": ["introduction"]
        }
        """;

    public static string DocsPageRazor => """
        @page "/docs/{*Path:nonfile}"
        @layout DocsLayout
        @using Microsoft.AspNetCore.Components.Sections
        @inject NavigationGraph Graph
        @inject MarkdownRenderer Renderer

        <PageTitle>@_title</PageTitle>

        @if (_document is not null)
        {
            <MarkdownContent Document="_document" />
            <PrevNextNav Prev="_prev" Next="_next" />

            <SectionContent SectionName="docs-toc">
                <TableOfContents Headings="_document.Headings" />
            </SectionContent>
        }
        else
        {
            <div class="doc-not-found">
                <h1>Page not found</h1>
                <p>The page <code>@Path</code> doesn't exist yet.</p>
                <p><a href="/docs/introduction">← Back to introduction</a></p>
            </div>
        }

        @code {
            [Parameter] public string? Path { get; set; }

            private RenderedDocument? _document;
            private string _title = "";
            private NavigationNode? _prev;
            private NavigationNode? _next;

            protected override void OnParametersSet()
            {
                var url = "/docs" + (string.IsNullOrEmpty(Path) ? "" : "/" + Path);
                var node = Graph.ResolveByUrl(url);
                if (node?.Path is not null && System.IO.File.Exists(node.Path))
                {
                    _document = Renderer.RenderFile(node.Path);
                    _title = node.Title + " — Docs";
                    (_prev, _next) = Graph.GetPrevNext(node);
                }
                else
                {
                    _document = null;
                    _title = "Not found";
                    _prev = null;
                    _next = null;
                }
            }
        }
        """;

    /* Copy-paste snippets the user drops into their own Program.cs / App.razor.
       We don't patch those files directly — the user's project may have custom
       middleware, auth, etc. we can't safely rewrite around. */
    public static string SetupInstructionsMd(string siteName, string githubRepo) => $$"""
        # ShellDocs setup

        Two files in your Blazor project need small additions. Copy these snippets in, then delete this file.

        ## 1. `Program.cs`

        Add near the top with your other usings:

        ```csharp
        using ShellDocs.Components;
        ```

        Register the framework before `var app = builder.Build();`:

        ```csharp
        builder.WebHost.UseStaticWebAssets();

        builder.Services.AddShellDocs(o =>
        {
            o.ContentRoot = Path.Combine(builder.Environment.ContentRootPath, "content");
            o.SiteName = "{{siteName}}";
            o.GitHubRepo = "{{githubRepo}}";
            o.AddNavLink("Docs", "/docs/introduction");
            // o.RegisterComponent<MyComponent>();  // for razor:preview blocks
        });
        ```

        ## 2. `Components/App.razor`

        Add these two `<link>` tags inside `<head>`, before your app styles:

        ```html
        <link rel="stylesheet" href="_content/ShellDocs.Tokens/tokens.css" />
        <link rel="stylesheet" href="_content/ShellDocs.Components/shelldocs-theme.css" />
        ```

        Add these before `</body>` (below `blazor.web.js` is fine):

        ```html
        <script src="_content/ShellDocs.Components/shelldocs.js"></script>
        <script type="module">
            import { createHighlighter } from 'https://esm.sh/shiki@1.24.0';
            window.__shiki = await createHighlighter({
                themes: ['github-light', 'github-dark'],
                langs: ['razor', 'csharp', 'html', 'json', 'yaml', 'bash', 'typescript', 'javascript', 'markdown']
            });
            if (window.shelldocsHighlight) window.shelldocsHighlight();
        </script>
        ```

        And this tiny inline script inside `<head>` (before `<HeadOutlet>`) — bootstraps dark mode before Blazor hydrates so there's no flash:

        ```html
        <script>
            (function () {
                var saved = null;
                try { saved = localStorage.getItem('shelldocs-theme'); } catch (e) {}
                var systemDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
                if ((saved ?? (systemDark ? 'dark' : 'light')) === 'dark') {
                    document.documentElement.classList.add('dark');
                }
            })();
        </script>
        ```

        ## 3. Run it

        ```bash
        dotnet run
        ```

        Visit `/docs/introduction`. Delete this file when done.
        """;
}
