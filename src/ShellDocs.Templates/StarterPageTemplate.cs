namespace ShellDocs.Templates;

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
