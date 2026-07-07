namespace ShellDocs.Core;

/// One page in the ShellDocs navigation graph. Built from a .md file's frontmatter + path.
public class NavigationNode
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public int Order { get; init; }
    public string? Path { get; init; }
    public IReadOnlyList<Heading> Headings { get; init; } = Array.Empty<Heading>();
    public NavigationNode? Parent { get; internal set; }
    public IReadOnlyList<NavigationNode> Children { get; internal set; } = Array.Empty<NavigationNode>();
}

public record Heading(int Level, string Text, string Id);
