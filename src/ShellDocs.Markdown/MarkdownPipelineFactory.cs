using Markdig;

namespace ShellDocs.Markdown;

/// Configures a Markdig pipeline with ShellDocs' custom extensions:
/// YAML frontmatter, razor:preview fenced blocks, inline Razor component tags.
public static class MarkdownPipelineFactory
{
    // Real implementation lands in feat/markdown-pipeline. This stub gets us compiling.
    public static MarkdownPipeline Create() =>
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseYamlFrontMatter()
            .Build();
}
