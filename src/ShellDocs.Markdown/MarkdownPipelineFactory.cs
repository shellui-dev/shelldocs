using Markdig;

namespace ShellDocs.Markdown;

public static class MarkdownPipelineFactory
{
    public static MarkdownPipeline Create() =>
        new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseGridTables()
            .UseAutoIdentifiers()
            .UseAutoLinks()
            .UseTaskLists()
            .UseEmphasisExtras()
            .UseFootnotes()
            .UseDiagrams()
            .UseMediaLinks()
            .UseSoftlineBreakAsHardlineBreak()
            .Build();
}
