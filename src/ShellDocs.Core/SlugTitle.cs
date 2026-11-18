namespace ShellDocs.Core;

// The title for a page or folder that doesn't set one: "getting-started" → "Getting Started".
public static class SlugTitle
{
    // Words that capitalising the first letter gets wrong: acronyms, and names with inner capitals.
    public static IReadOnlyDictionary<string, string> KnownWords { get; } = new[]
    {
        "AI", "API", "APIs", "CD", "CDN", "CI", "CLI", "CSS", "CSV", "DNS", "FAQ", "HTML", "HTTP", "HTTPS",
        "IO", "JSON", "JWT", "LLM", "LLMs", "MAUI", "MVC", "PDF", "RCL", "RSS", "SDK", "SEO", "SPA", "SQL",
        "SSH", "SSO", "SSR", "SVG", "UI", "URL", "URLs", "UX", "WASM", "XAML", "XML", "YAML",
        "GitHub", "GraphQL", "iOS", "JavaScript", "macOS", "NuGet", "OAuth", "TypeScript", "WebAssembly",
    }.ToDictionary(w => w, StringComparer.OrdinalIgnoreCase);

    public static string FromSlug(string slug) => string.Join(' ', slug.Split('-').Select(Word));

    public static string Word(string word) =>
        word.Length == 0 ? word
        : KnownWords.TryGetValue(word, out var known) ? known
        : char.ToUpper(word[0]) + word[1..];
}
