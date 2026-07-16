using Markdig;
using ShellDocs.Core;

namespace ShellDocs.Markdown;

public class MarkdownRenderer
{
    private readonly TypeRegistry _registry;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownRenderer() : this(new TypeRegistry(), MarkdownPipelineFactory.Create()) { }
    public MarkdownRenderer(TypeRegistry registry) : this(registry, MarkdownPipelineFactory.Create()) { }
    public MarkdownRenderer(TypeRegistry registry, MarkdownPipeline pipeline)
    {
        _registry = registry;
        _pipeline = pipeline;
    }

    public IReadOnlyList<string> LastWarnings { get; private set; } = Array.Empty<string>();

    public RenderedDocument Render(string markdown)
    {
        var parsed = FrontmatterParser.Parse(markdown ?? "");

        var extractor = new SlotExtractor(_registry);
        var (processed, slots, warnings) = extractor.Process(parsed.Body);
        LastWarnings = warnings;

        var doc = Markdig.Markdown.Parse(processed, _pipeline);
        var headings = HeadingExtractor.Extract(doc);
        var html = CodeBlockEnhancer.Enhance(doc.ToHtml(_pipeline));

        return new RenderedDocument(html, slots, parsed, headings);
    }

    public RenderedDocument RenderFile(string path) => Render(File.ReadAllText(path));
}
