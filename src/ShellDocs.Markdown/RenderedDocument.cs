using ShellDocs.Core;

namespace ShellDocs.Markdown;

public record RenderedDocument(
    string Html,
    IReadOnlyList<Slot> Slots,
    ParsedDocument Source,
    IReadOnlyList<Heading> Headings);

public abstract record Slot(string Id);

public record ComponentSlot(
    string Id,
    Type ComponentType,
    IReadOnlyDictionary<string, string> Parameters,
    string? ChildContentRaw) : Slot(Id);

// Nodes is the whole fence in order and is what PreviewFrame renders; the other
// fields describe the first registered component (or the first unknown tag on Error).
public record PreviewSlot(
    string Id,
    Type? ComponentType,
    IReadOnlyDictionary<string, string> Parameters,
    string Code,
    string Language,
    string? ChildContentRaw = null,
    string? Error = null,
    IReadOnlyList<PreviewNode>? Nodes = null) : Slot(Id);

// Parsed razor:preview content. Component children stay raw (ChildContentRaw)
// so they go through the same markdown + named-slot path as inline tags.
public abstract record PreviewNode;

public record PreviewTextNode(string Text) : PreviewNode;

// Plain HTML element (or an unregistered capitalised tag). Razor directive
// attributes (@onclick, @bind, …) are dropped; a null value is a bare attribute.
public record PreviewElementNode(
    string TagName,
    IReadOnlyList<KeyValuePair<string, string?>> Attributes,
    IReadOnlyList<PreviewNode> Children) : PreviewNode;

public record PreviewComponentNode(
    string TagName,
    Type ComponentType,
    IReadOnlyDictionary<string, string> Parameters,
    string? ChildContentRaw) : PreviewNode;
