namespace ShellDocs.Components;

// Set through ShellDocsOptions.EnableLlmsTxt; see Llms/LlmsTxtFiles.cs for what is served.
public sealed class LlmsTxtOptions
{
    // The "> " line under the site name in llms.txt and the Cursor rule's description.
    // Defaults to SiteTagline.
    public string? Summary { get; set; }

    /* A Markdown file shown at the top of llms.txt and llms-full.txt and in the agent
       instruction files, such as "## Rules for AI agents" and a list. A relative path is
       resolved against the app's content root. */
    public string? RulesFile { get; set; }

    /* Regex for a page's install command, looked for in its code blocks and listed
       after the page in llms.txt, for example @"shellui add [\w-]+". */
    public string? InstallCommandPattern { get; set; }

    // AGENTS.md, CLAUDE.md, copilot-instructions.md and a Cursor rule under agents/.
    public bool AgentFiles { get; set; } = true;

    // The page actions menu by the title: Copy page, View as Markdown, Open in Claude / ChatGPT.
    public bool PageActions { get; set; } = true;
}
