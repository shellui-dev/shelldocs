namespace ShellDocs.Templates;

/* Templates emitted by `shelldocs init`. Raw string constants so the CLI has
   zero I/O overhead — the templates ARE the payload. */
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
        @using ShellDocs.Components
        @using ShellDocs.Components.Chrome
        @using ShellDocs.Components.Content
        @using ShellDocs.Components.Layouts
        @using ShellDocs.Core
        @using ShellDocs.Markdown
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
                <p><a href="/docs/introduction"><svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:middle;margin-right:0.35rem"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>Back to introduction</a></p>
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

    /* Home.razor emitted by create mode after stripping the fresh Blazor
       template's Counter/Weather demo pages. Fumadocs-style welcome — one
       CTA to the docs, plus edit-this-file hints. */
    public static string WelcomeHomeRazor => """
        @page "/"
        @layout HomeLayout
        @using ShellDocs.Components
        @using ShellDocs.Components.Chrome
        @using ShellDocs.Components.Layouts
        @inject ShellDocsOptions Options

        <PageTitle>Home — @SiteName</PageTitle>

        <div class="welcome">
            <div class="welcome-hero">
                <h1>Welcome to your <span class="grad">ShellDocs</span> site.</h1>
                <p>Everything is wired up. Author markdown in <code>content/docs/</code>, and it renders live under <code>/docs</code>.</p>
                <a href="/docs/introduction" class="btn primary">
                    <span>View your docs</span>
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M12 5l7 7-7 7"/></svg>
                </a>
            </div>

            <div class="welcome-hints">
                <div class="welcome-card">
                    <h2>Customize this page</h2>
                    <p>This welcome screen lives at <code>Components/Pages/Home.razor</code>. Edit it to build your marketing page.</p>
                </div>
                <div class="welcome-card">
                    <h2>Add pages</h2>
                    <p>Drop new <code>.md</code> files in <code>content/docs/</code>. Order them via <code>content/docs/meta.json</code>.</p>
                </div>
                <div class="welcome-card">
                    <h2>Register components</h2>
                    <p>Add <code>o.RegisterComponent&lt;T&gt;()</code> calls in <code>Program.cs</code> so Blazor components render in <code>razor:preview</code> blocks.</p>
                </div>
            </div>
        </div>

        <DocsFooter Version="0.1.0-alpha" />

        <style>
            .welcome { max-width: 44rem; margin: 4rem auto; padding: 2rem 1.5rem; }
            .welcome-hero { text-align: center; margin-bottom: 3.5rem; }
            .welcome-hero h1 { font-size: 2.25rem; font-weight: 700; letter-spacing: -0.025em; margin: 0 0 1rem; line-height: 1.15; }
            .welcome-hero .grad {
                background: linear-gradient(135deg, var(--foreground), color-mix(in oklch, var(--foreground) 60%, transparent));
                -webkit-background-clip: text; background-clip: text; color: transparent;
            }
            .welcome-hero p { color: var(--muted-foreground); font-size: 1rem; line-height: 1.6; margin: 0 0 1.75rem; }
            .welcome-hints { display: grid; grid-template-columns: 1fr; gap: 0.85rem; }
            @@media (min-width: 640px) { .welcome-hints { grid-template-columns: repeat(3, 1fr); } }
            .welcome-card { padding: 1rem 1.15rem; border: 1px solid var(--border); border-radius: calc(var(--radius) + 2px); background: var(--card); }
            .welcome-card h2 { font-size: 0.9rem; font-weight: 600; margin: 0 0 0.4rem; letter-spacing: -0.005em; }
            .welcome-card p { margin: 0; color: var(--muted-foreground); font-size: 0.85rem; line-height: 1.5; }
            .welcome code { font-family: var(--font-mono); font-size: 0.85em; background: var(--muted); padding: 0.1rem 0.4rem; border-radius: 4px; border: 1px solid var(--border); }
            .btn.primary {
                display: inline-flex; align-items: center; gap: 0.5rem;
                padding: 0.65rem 1.25rem; border-radius: 9999px;
                background: var(--primary); color: var(--primary-foreground);
                text-decoration: none; font-weight: 500; font-size: 0.9rem;
                transition: background 150ms;
            }
            .btn.primary:hover { background: color-mix(in oklch, var(--primary) 90%, transparent); }
            .btn.primary svg { width: 0.85rem; height: 0.85rem; }
        </style>

        @code {
            private string SiteName => string.IsNullOrEmpty(Options.SiteName) ? "ShellDocs" : Options.SiteName;
        }
        """;

    /* Bare pass-through MainLayout that replaces the fresh template's
       sidebar+NavMenu layout. Every page uses @layout to pick its real
       layout (HomeLayout or DocsLayout), so this is a fallback only. */
    public static string BareMainLayoutRazor => """
        @inherits LayoutComponentBase
        @Body
        """;

    // --- Program.cs patch snippets ---

    public static string ProgramUsing => "using ShellDocs.Components;";

    public static string ProgramWebHost => "builder.WebHost.UseStaticWebAssets();";

    public static string ProgramAddShellDocs(string siteName, string githubRepo) => $$"""
        builder.Services.AddShellDocs(o =>
        {
            o.ContentRoot = System.IO.Path.Combine(builder.Environment.ContentRootPath, "content");
            o.SiteName = "{{siteName}}";
            o.GitHubRepo = "{{githubRepo}}";
            o.AddNavLink("Docs", "/docs/introduction");
            // o.RegisterComponent<MyComponent>();  // for razor:preview blocks
        });
        """;

    // --- App.razor patch snippets ---

    public static string AppTokenLinks => """
        <link rel="stylesheet" href="_content/ShellDocs.Tokens/tokens.css" />
        <link rel="stylesheet" href="_content/ShellDocs.Components/shelldocs-theme.css" />
        """;

    public static string AppThemeBootstrap => """
        <script>
            (function () {
                var saved = null;
                try { saved = localStorage.getItem('shelldocs-theme'); } catch (e) {}
                var systemDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
                if ((saved || (systemDark ? 'dark' : 'light')) === 'dark') {
                    document.documentElement.classList.add('dark');
                }
            })();
        </script>
        """;

    public static string AppScripts => """
        <script src="_content/ShellDocs.Components/shelldocs.js"></script>
        <script type="module">
            import { createHighlighter } from 'https://esm.sh/shiki@1.24.0';
            window.__shiki = await createHighlighter({
                themes: ['github-light', 'github-dark'],
                langs: ['razor', 'csharp', 'html', 'json', 'yaml', 'bash', 'typescript', 'javascript', 'markdown']
            });
            if (window.shelldocsHighlight) window.shelldocsHighlight();
        </script>
        """;

    // --- Fallback: SHELLDOCS_SETUP.md for --attach mode where we can't safely patch ---

    public static string SetupInstructionsMd(string siteName, string githubRepo) => $$"""
        # ShellDocs setup

        Two files in your Blazor project need small additions. Copy these snippets in, then delete this file.

        ## 1. `Program.cs`

        Add near the top with your other usings:

        ```csharp
        {{ProgramUsing}}
        ```

        Register the framework before `var app = builder.Build();`:

        ```csharp
        {{ProgramWebHost}}

        {{ProgramAddShellDocs(siteName, githubRepo)}}
        ```

        ## 2. `Components/App.razor`

        Add these two `<link>` tags inside `<head>`, before your app styles:

        ```html
        {{AppTokenLinks}}
        ```

        Add this small script inside `<head>` (before `<HeadOutlet>`) — bootstraps dark mode before Blazor hydrates:

        ```html
        {{AppThemeBootstrap}}
        ```

        Add these before `</body>` (below `blazor.web.js` is fine):

        ```html
        {{AppScripts}}
        ```

        ## 3. Run it

        ```bash
        dotnet run
        ```

        Visit `/docs/introduction`. Delete this file when done.
        """;
}
