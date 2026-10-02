namespace ShellDocs.Markdown;

internal readonly record struct RazorTag(
    string Name,
    bool IsClose,
    bool IsSelfClosing,
    IReadOnlyList<KeyValuePair<string, string?>> Attributes,
    int Start,
    int End);

/* Linear tag reader for Razor markup. Regexes break on values like
   OnClick="@(() => x > y)" and misread @bind-Value as Value; this keeps full
   attribute names and skips balanced @( … ) expressions, strings included. */
internal static class RazorTagScanner
{
    public static bool TryRead(string text, int pos, out RazorTag tag)
    {
        tag = default;
        if (pos >= text.Length || text[pos] != '<') return false;

        var i = pos + 1;
        var isClose = false;
        if (i < text.Length && text[i] == '/') { isClose = true; i++; }

        var nameStart = i;
        if (i >= text.Length || !char.IsAsciiLetter(text[i])) return false;
        while (i < text.Length && IsNameChar(text[i])) i++;
        var name = text[nameStart..i];

        var attrs = new List<KeyValuePair<string, string?>>();
        var self = false;
        while (true)
        {
            var hadSpace = SkipWhitespace(text, ref i);
            if (i >= text.Length) return false;
            if (text[i] == '>') { i++; break; }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>') { self = true; i += 2; break; }
            if (isClose || !hadSpace) return false;

            var attrStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('=' or '>' or '"' or '\'')
                   && !(text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>'))
                i++;
            if (i == attrStart) return false;
            var attrName = text[attrStart..i];

            var save = i;
            SkipWhitespace(text, ref i);
            if (i < text.Length && text[i] == '=')
            {
                i++;
                SkipWhitespace(text, ref i);
                if (!TryReadValue(text, ref i, out var value)) return false;
                attrs.Add(new(attrName, value));
            }
            else
            {
                i = save;
                attrs.Add(new(attrName, null));
            }
        }

        tag = new RazorTag(name, isClose, self, attrs, pos, i);
        return true;
    }

    public static IReadOnlyList<KeyValuePair<string, string?>> ParseAttributes(string? attrsText)
    {
        if (string.IsNullOrWhiteSpace(attrsText)) return Array.Empty<KeyValuePair<string, string?>>();
        var probe = "<x " + attrsText.Trim() + ">";
        return TryRead(probe, 0, out var tag) ? tag.Attributes : Array.Empty<KeyValuePair<string, string?>>();
    }

    // Close tag balancing an open `name` tag ending at `from`; (-1, -1) when unbalanced.
    public static (int Start, int End) FindMatchingClose(string text, string name, int from, StringComparison comparison)
    {
        var depth = 1;
        var i = from;
        while (i < text.Length)
        {
            var lt = text.IndexOf('<', i);
            if (lt < 0) break;
            if (!TryRead(text, lt, out var t)) { i = lt + 1; continue; }
            if (string.Equals(t.Name, name, comparison))
            {
                if (t.IsClose)
                {
                    depth--;
                    if (depth == 0) return (t.Start, t.End);
                }
                else if (!t.IsSelfClosing)
                {
                    depth++;
                }
            }
            i = t.End;
        }
        return (-1, -1);
    }

    private static bool TryReadValue(string text, ref int i, out string value)
    {
        value = "";
        if (i >= text.Length) return false;
        var q = text[i];
        if (q is '"' or '\'')
        {
            var start = ++i;
            // `@( … )` — skip the balanced expression first so quotes and '>'
            // inside it don't terminate the attribute value.
            if (i + 1 < text.Length && text[i] == '@' && text[i + 1] == '(')
            {
                var end = SkipBalancedParens(text, i + 1);
                if (end > 0) i = end;
            }
            var close = text.IndexOf(q, i);
            if (close < 0) return false;
            value = text[start..close];
            i = close + 1;
            return true;
        }

        var s = i;
        while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '>'
               && !(text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>'))
            i++;
        if (i == s) return false;
        value = text[s..i];
        return true;
    }

    // `open` points at '('. Returns the index just past the matching ')', or -1.
    internal static int SkipBalancedParens(string text, int open) => SkipBalanced(text, open, '(', ')');

    internal static int SkipBalanced(string text, int open, char openCh, char closeCh)
    {
        var depth = 0;
        for (var j = open; j < text.Length; j++)
        {
            var c = text[j];
            if (c == openCh) depth++;
            else if (c == closeCh)
            {
                depth--;
                if (depth == 0) return j + 1;
            }
            else if (c is '"' or '\'')
            {
                j = SkipStringLiteral(text, j);
                if (j < 0) return -1;
            }
        }
        return -1;
    }

    // `start` points at the opening quote; returns the index of the closing quote.
    private static int SkipStringLiteral(string text, int start)
    {
        var q = text[start];
        for (var j = start + 1; j < text.Length; j++)
        {
            if (text[j] == '\\') { j++; continue; }
            if (text[j] == q) return j;
            if (text[j] == '\n') return j - 1; // unterminated — give up on this line
        }
        return -1;
    }

    private static bool SkipWhitespace(string text, ref int i)
    {
        var s = i;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return i > s;
    }

    private static bool IsNameChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.';
}
