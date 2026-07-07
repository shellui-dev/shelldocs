namespace ShellDocs.Templates;

/// Starter markdown emitted by `shelldocs new page`.
/// Real templates land alongside the CLI init/new commands.
public static class StarterPageTemplate
{
    public static string Content(string title, string description) => $$"""
        ---
        title: {{title}}
        description: {{description}}
        ---

        # {{title}}

        Start writing.
        """;
}
