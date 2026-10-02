using System.Text;

namespace ShellDocs.Components.Content;

// Anchors and ⋯-menu links for preview frames.
internal static class PreviewLinks
{
    // IssueTrackerUrl, else the GitHub "new issue" page for GitHubRepo, else null.
    public static string? IssueTracker(ShellDocsOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.IssueTrackerUrl)) return options.IssueTrackerUrl.Trim();
        if (!string.IsNullOrWhiteSpace(options.GitHubRepo)) return $"https://github.com/{options.GitHubRepo.Trim().Trim('/')}/issues/new";
        return null;
    }

    // Absolute when SiteUrl is set; otherwise the app-relative path.
    public static string PageUrl(ShellDocsOptions options, string path, string? anchor)
    {
        var url = (options.SiteUrl?.TrimEnd('/') ?? "") + path;
        return string.IsNullOrEmpty(anchor) ? url : url + "#" + anchor;
    }

    // Title and body ride along as query params; labels only mean something to GitHub.
    public static string Issue(string tracker, string title, string body, string label)
    {
        var sb = new StringBuilder(tracker);
        sb.Append(tracker.Contains('?') ? '&' : '?');
        sb.Append("title=").Append(Uri.EscapeDataString(title));
        sb.Append("&body=").Append(Uri.EscapeDataString(body));
        if (tracker.Contains("github.com/", StringComparison.OrdinalIgnoreCase))
            sb.Append("&labels=").Append(Uri.EscapeDataString(label));
        return sb.ToString();
    }

    public static string BugReport(string tracker, string name, string pageUrl) => Issue(tracker,
        $"Bug in the {name} example",
        $"Example: {pageUrl}\n\n**What happened?**\n\n\n**What did you expect?**\n",
        "bug");

    public static string Suggestion(string tracker, string name, string pageUrl) => Issue(tracker,
        $"Suggestion for the {name} example",
        $"Example: {pageUrl}\n\n**Suggestion**\n",
        "enhancement");

    // "ButtonClickDemo" → "button-click-demo". Lowercase ASCII letters/digits and single dashes.
    public static string Slug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (char.IsUpper(c) && sb.Length > 0 && sb[^1] != '-' && i > 0 && (char.IsLower(text[i - 1]) || char.IsDigit(text[i - 1])))
                    sb.Append('-');
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }
        return sb.ToString().Trim('-');
    }

    /* Stable across processes and renders (string.GetHashCode is randomized per
       process), so a prerendered id matches the id after Blazor re-renders. */
    public static string ShortHash(string? text)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in text ?? "") { h ^= c; h *= 16777619u; }
            return (h & 0xFFFFFF).ToString("x6");
        }
    }
}
