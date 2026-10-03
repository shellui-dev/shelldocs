using System.Text.RegularExpressions;
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
        var html = WrapTables(CodeBlockEnhancer.Enhance(doc.ToHtml(_pipeline)));

        return new RenderedDocument(html, slots, parsed, headings);
    }

    public RenderedDocument RenderFile(string path) => Render(File.ReadAllText(path));

    // Tables scroll inside their own box, so a wide one doesn't widen the page on phones.
    private static readonly Regex TableOpen = new(@"<table(\s[^>]*)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TableClose = new(@"</table>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static string WrapTables(string html)
        => TableClose.Replace(TableOpen.Replace(html, m => "<div class=\"shelldocs-table\">" + m.Value), "</table></div>");

    // Razor markup (component child content inside a razor:preview) parsed into
    // nodes the way the fence itself is: no markdown pass, so nothing gets wrapped
    // in <p> and elements around nested components stay intact.
    public (IReadOnlyList<PreviewNode> Nodes, IReadOnlyList<string> Warnings) ParseRazor(string markup)
    {
        var warnings = new List<string>();
        var nodes = new PreviewParser(_registry, warnings).Parse(markup ?? "");
        return (nodes, warnings);
    }
}
