namespace ShellDocs.Components.Content;

// Keeps every <CodeGroup> with the same SyncKey on the same tab, per circuit.
public class CodeGroupSyncState
{
    private readonly Dictionary<string, string> _selected = new(StringComparer.Ordinal);
    public event Action<string>? OnChange;

    public string? Get(string key) => _selected.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, string value)
    {
        if (_selected.TryGetValue(key, out var current) && current == value) return;
        _selected[key] = value;
        OnChange?.Invoke(key);
    }
}
