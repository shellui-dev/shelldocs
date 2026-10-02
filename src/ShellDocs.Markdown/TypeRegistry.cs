namespace ShellDocs.Markdown;

public class TypeRegistry
{
    private readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);
    private readonly List<TypeCollision> _collisions = new();

    public TypeRegistry Register<T>() where T : class => Register(typeof(T));

    public TypeRegistry Register(Type type) => Register(type.Name, type);

    // Last registration wins; a tag re-pointed at a different type is recorded in Collisions.
    public TypeRegistry Register(string tagName, Type type)
    {
        if (_byName.TryGetValue(tagName, out var existing) && existing != type)
            _collisions.Add(new TypeCollision(tagName, existing, type));
        _byName[tagName] = type;
        return this;
    }

    public Type? Resolve(string tagName)
        => _byName.TryGetValue(tagName, out var t) ? t : null;

    public bool IsRegistered(string tagName) => _byName.ContainsKey(tagName);

    public IReadOnlyDictionary<string, Type> All => _byName;

    public IReadOnlyList<TypeCollision> Collisions => _collisions;
}

public record TypeCollision(string TagName, Type Replaced, Type Winner);
