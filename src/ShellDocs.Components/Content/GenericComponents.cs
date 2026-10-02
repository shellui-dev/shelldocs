using System.Collections.Concurrent;
using System.Reflection;
using ShellDocs.Markdown;

namespace ShellDocs.Components.Content;

/* Generic components register under their bare name (<BarChart>) as open
   definitions and are closed per use from attributes named after their type
   parameters, as Razor writes them: <BarChart TItem="SalesRow" />. A missing
   type argument falls back to object when the constraints allow it. */
internal static class GenericComponents
{
    private static readonly Dictionary<string, Type> Aliases = new(StringComparer.Ordinal)
    {
        ["bool"] = typeof(bool), ["byte"] = typeof(byte), ["sbyte"] = typeof(sbyte), ["char"] = typeof(char),
        ["decimal"] = typeof(decimal), ["double"] = typeof(double), ["float"] = typeof(float),
        ["int"] = typeof(int), ["uint"] = typeof(uint), ["long"] = typeof(long), ["ulong"] = typeof(ulong),
        ["short"] = typeof(short), ["ushort"] = typeof(ushort), ["nint"] = typeof(nint), ["nuint"] = typeof(nuint),
        ["object"] = typeof(object), ["string"] = typeof(string),
    };

    // Closes `type` if it's a generic definition. `remaining` is `attrs` without the
    // type-parameter attributes; `error` is set (and the result null) when it can't close.
    public static Type? Close(
        Type type,
        IReadOnlyDictionary<string, string> attrs,
        out IReadOnlyDictionary<string, string> remaining,
        out string? error,
        out IReadOnlyList<string> notes)
    {
        remaining = attrs;
        error = null;
        notes = Array.Empty<string>();
        if (!type.IsGenericTypeDefinition) return type;

        var typeParams = type.GetGenericArguments();
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in attrs) lookup[k] = v;

        var args = new Type[typeParams.Length];
        var messages = new List<string>();
        for (var i = 0; i < typeParams.Length; i++)
        {
            var name = typeParams[i].Name;
            if (lookup.TryGetValue(name, out var raw))
            {
                var resolved = ResolveTypeName(raw, type.Assembly);
                if (resolved is null)
                {
                    error = $"<{TagName(type)}>: can't resolve {name}=\"{raw}\". Use a type name the app references (e.g. \"int\", \"string\", \"MyApp.Models.Product\").";
                    return null;
                }
                args[i] = resolved;
            }
            else
            {
                args[i] = typeof(object);
                messages.Add($"<{TagName(type)}>: no {name}=\"…\" attribute; using object.");
            }
        }

        Type closed;
        try { closed = type.MakeGenericType(args); }
        catch (ArgumentException)
        {
            var names = string.Join(", ", typeParams.Select(p => p.Name + "=\"…\""));
            error = $"<{TagName(type)}> is generic and its type arguments don't satisfy its constraints. Set {names}.";
            return null;
        }

        var typeParamNames = new HashSet<string>(typeParams.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        remaining = attrs.Where(kv => !typeParamNames.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        notes = messages;
        return closed;
    }

    public static string TagName(Type type) => TypeRegistry.TagNameOf(type);

    // C# spellings: aliases, `T?`, `T[]`, `List<T>`, simple or full type names.
    // Searches the component's assembly first, then every loaded assembly.
    internal static Type? ResolveTypeName(string raw, Assembly? preferred = null)
    {
        var name = raw.Trim();
        if (name.StartsWith('@')) name = name[1..].Trim();
        if (name.Length == 0) return null;

        if (name.EndsWith('?'))
        {
            var inner = ResolveTypeName(name[..^1], preferred);
            if (inner is null) return null;
            return inner.IsValueType ? typeof(Nullable<>).MakeGenericType(inner) : inner;
        }
        if (name.EndsWith("[]", StringComparison.Ordinal))
            return ResolveTypeName(name[..^2], preferred)?.MakeArrayType();

        var lt = name.IndexOf('<');
        if (lt > 0 && name.EndsWith('>'))
        {
            var argNames = SplitTopLevel(name[(lt + 1)..^1]);
            var def = FindType($"{name[..lt].Trim()}`{argNames.Count}", preferred);
            if (def is null) return null;
            var typeArgs = new Type[argNames.Count];
            for (var i = 0; i < argNames.Count; i++)
            {
                var a = ResolveTypeName(argNames[i], preferred);
                if (a is null) return null;
                typeArgs[i] = a;
            }
            try { return def.MakeGenericType(typeArgs); }
            catch (ArgumentException) { return null; }
        }

        if (Aliases.TryGetValue(name, out var alias)) return alias;
        return FindType(name, preferred);
    }

    private static readonly ConcurrentDictionary<(string, Assembly?), Type?> _found = new();

    private static Type? FindType(string name, Assembly? preferred)
        => _found.GetOrAdd((name, preferred), key => Search(key.Item1, key.Item2));

    private static Type? Search(string name, Assembly? preferred)
    {
        var direct = Type.GetType(name, throwOnError: false) ?? Type.GetType("System." + name, throwOnError: false);
        if (direct is not null) return direct;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);
        if (preferred is not null) assemblies = assemblies.OrderBy(a => a == preferred ? 0 : 1);

        Type? bySimpleName = null;
        foreach (var assembly in assemblies)
        {
            var full = assembly.GetType(name, throwOnError: false);
            if (full is not null) return full;
            if (bySimpleName is not null || name.Contains('.')) continue;
            bySimpleName = ExportedTypes(assembly).FirstOrDefault(t => t.Name == name);
        }
        return bySimpleName;
    }

    private static IEnumerable<Type> ExportedTypes(Assembly assembly)
    {
        try { return assembly.GetExportedTypes(); }
        catch { return Array.Empty<Type>(); }
    }

    private static List<string> SplitTopLevel(string s)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '<') depth++;
            else if (s[i] == '>') depth--;
            else if (s[i] == ',' && depth == 0)
            {
                parts.Add(s[start..i].Trim());
                start = i + 1;
            }
        }
        parts.Add(s[start..].Trim());
        return parts;
    }
}
