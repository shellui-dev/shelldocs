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

public record PreviewSlot(
    string Id,
    Type? ComponentType,
    IReadOnlyDictionary<string, string> Parameters,
    string Code,
    string Language,
    string? ChildContentRaw = null,
    string? Error = null) : Slot(Id);
