using YamlDotNet.Serialization;

namespace ShellDocs.Core;

public static class FrontmatterParser
{
    private static readonly string[] LineSeparators = new[] { "\r\n", "\n" };
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    public static ParsedDocument Parse(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return new ParsedDocument(new Dictionary<string, object?>(), source ?? "");
        }

        var lines = source.Split(LineSeparators, StringSplitOptions.None);
        if (lines.Length < 2 || lines[0].TrimEnd() != "---")
        {
            return new ParsedDocument(new Dictionary<string, object?>(), source);
        }

        var closingIndex = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == "---")
            {
                closingIndex = i;
                break;
            }
        }
        if (closingIndex == -1)
        {
            return new ParsedDocument(new Dictionary<string, object?>(), source);
        }

        var yaml = string.Join("\n", lines[1..closingIndex]);
        var body = string.Join("\n", lines[(closingIndex + 1)..]);

        Dictionary<string, object?> frontmatter;
        try
        {
            frontmatter = YamlDeserializer.Deserialize<Dictionary<string, object?>>(yaml)
                          ?? new Dictionary<string, object?>();
        }
        catch (YamlDotNet.Core.YamlException)
        {
            frontmatter = new Dictionary<string, object?>();
        }

        return new ParsedDocument(frontmatter, body);
    }

    public static T? GetValue<T>(this IReadOnlyDictionary<string, object?> frontmatter, string key)
    {
        if (!frontmatter.TryGetValue(key, out var raw) || raw is null) return default;
        if (raw is T typed) return typed;
        try
        {
            return (T)Convert.ChangeType(raw, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}

public record ParsedDocument(IReadOnlyDictionary<string, object?> Frontmatter, string Body);
