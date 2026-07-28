using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShellDocs.Core;

public class MetaJson
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("pages")]
    public List<MetaJsonEntry> Pages { get; set; } = new();

    // Slugs of pages or subfolders that should route (URLs resolve) but not
    // appear in the sidebar tree. Useful for landing pages reached only via
    // the package selector, private drafts, or archived content.
    [JsonPropertyName("hidden")]
    public List<string> Hidden { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new MetaJsonEntryConverter() }
    };

    public static MetaJson? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<MetaJson>(json, Options);
    }
}

public abstract class MetaJsonEntry { }
public class MetaJsonPageRef : MetaJsonEntry { public required string Slug { get; init; } }
public class MetaJsonDivider : MetaJsonEntry { }
public class MetaJsonSubsection : MetaJsonEntry
{
    public required string Title { get; init; }
    public List<MetaJsonEntry> Pages { get; init; } = new();
}

/*
    Each entry in meta.json's "pages" array can be:
      - a string slug ("button")
      - the literal "---" for a section divider
      - an object { "title": "Data Display", "pages": [...] } for a subsection
    Custom converter dispatches on the JSON token type.
*/
internal class MetaJsonEntryConverter : JsonConverter<MetaJsonEntry>
{
    public override MetaJsonEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString() ?? "";
            return value == "---" ? new MetaJsonDivider() : new MetaJsonPageRef { Slug = value };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var pages = new List<MetaJsonEntry>();
            if (root.TryGetProperty("pages", out var p) && p.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in p.EnumerateArray())
                {
                    var elJson = el.GetRawText();
                    var childReader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(elJson));
                    childReader.Read();
                    var child = Read(ref childReader, typeof(MetaJsonEntry), options);
                    if (child is not null) pages.Add(child);
                }
            }
            return new MetaJsonSubsection { Title = title, Pages = pages };
        }

        throw new JsonException($"Unexpected token {reader.TokenType} in meta.json entry.");
    }

    public override void Write(Utf8JsonWriter writer, MetaJsonEntry value, JsonSerializerOptions options)
        => throw new NotSupportedException("Writing meta.json entries not supported.");
}
