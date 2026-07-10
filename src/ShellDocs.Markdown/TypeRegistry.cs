namespace ShellDocs.Markdown;

public class TypeRegistry
{
    private readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);

    public TypeRegistry Register<T>() where T : class => Register(typeof(T));

    public TypeRegistry Register(Type type)
    {
        _byName[type.Name] = type;
        return this;
    }

    public TypeRegistry Register(string tagName, Type type)
    {
        _byName[tagName] = type;
        return this;
    }

    public Type? Resolve(string tagName)
        => _byName.TryGetValue(tagName, out var t) ? t : null;

    public bool IsRegistered(string tagName) => _byName.ContainsKey(tagName);

    public IReadOnlyDictionary<string, Type> All => _byName;
}
