using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Linq;

namespace ShellDocs.Components.Content;

// Loads `<Assembly>.xml` sidecars once per assembly and exposes summaries by
// member id (`P:Namespace.Type.Prop`). Missing file → empty index (returns null).
internal static class XmlDocIndex
{
    private static readonly ConcurrentDictionary<Assembly, IReadOnlyDictionary<string, string>> _cache = new();

    public static string? SummaryFor(PropertyInfo prop)
    {
        var declaring = prop.DeclaringType;
        if (declaring is null) return null;
        var index = LoadFor(declaring.Assembly);
        var key = "P:" + declaring.FullName + "." + prop.Name;
        return index.TryGetValue(key, out var s) ? s : null;
    }

    private static IReadOnlyDictionary<string, string> LoadFor(Assembly asm)
    {
        return _cache.GetOrAdd(asm, a =>
        {
            var xmlPath = Path.ChangeExtension(a.Location, ".xml");
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                return new Dictionary<string, string>(0);
            try
            {
                var doc = XDocument.Load(xmlPath);
                var members = doc.Root?.Element("members")?.Elements("member") ?? Enumerable.Empty<XElement>();
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var m in members)
                {
                    var name = m.Attribute("name")?.Value;
                    var summary = m.Element("summary")?.Value;
                    if (name is null || summary is null) continue;
                    map[name] = CollapseWhitespace(summary);
                }
                return map;
            }
            catch
            {
                return new Dictionary<string, string>(0);
            }
        });
    }

    private static string CollapseWhitespace(string raw)
    {
        var trimmed = raw.Trim();
        var sb = new System.Text.StringBuilder(trimmed.Length);
        var lastWasSpace = false;
        foreach (var c in trimmed)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }
}
