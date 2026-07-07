namespace ShellDocs.Core;

public class NavigationNode
{
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string? Category { get; init; }
    public int Order { get; init; }
    public string? Path { get; init; }
    public NodeKind Kind { get; init; } = NodeKind.Page;

    // Populated at render time by ShellDocs.Markdown; empty until then.
    public IReadOnlyList<Heading> Headings { get; init; } = Array.Empty<Heading>();

    public NavigationNode? Parent { get; internal set; }
    public IReadOnlyList<NavigationNode> Children { get; internal set; } = Array.Empty<NavigationNode>();
}

public enum NodeKind
{
    Page,
    Section,
    Divider
}
