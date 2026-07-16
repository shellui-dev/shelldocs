using System.Text.RegularExpressions;

namespace ShellDocs.Markdown;

public static class CodeBlockEnhancer
{
    private static readonly Regex CodeBlock = new(
        @"<pre>\s*<code(?<attrs>[^>]*)>(?<code>[\s\S]*?)</code>\s*</pre>",
        RegexOptions.Compiled);

    private static readonly Regex LanguageClass = new(
        @"language-(?<lang>[a-zA-Z0-9+#-]+)",
        RegexOptions.Compiled);

    public static string Enhance(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return CodeBlock.Replace(html, m =>
        {
            var attrs = m.Groups["attrs"].Value;
            var code = m.Groups["code"].Value;
            var langMatch = LanguageClass.Match(attrs);
            var langName = langMatch.Success ? langMatch.Groups["lang"].Value : "";

            var langBadge = string.IsNullOrEmpty(langName)
                ? ""
                : $"<span class=\"shelldocs-code-lang\">{langName}</span>";

            return $@"<div class=""shelldocs-codeblock"">
    <div class=""shelldocs-codeblock-header"">
        {langBadge}
        <button type=""button"" class=""shelldocs-codeblock-copy"" onclick=""shelldocsCopyCode(this)"" aria-label=""Copy code"">
            <svg class=""icon-copy"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><rect x=""9"" y=""9"" width=""13"" height=""13"" rx=""2""/><path d=""M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1""/></svg>
            <svg class=""icon-check"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><polyline points=""20 6 9 17 4 12""/></svg>
        </button>
    </div>
    <pre><code{attrs}>{code}</code></pre>
</div>";
        });
    }
}
