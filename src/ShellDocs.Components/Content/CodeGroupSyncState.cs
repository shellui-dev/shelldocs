namespace ShellDocs.Components.Content;

/* Cross-page sync for <CodeGroup SyncKey="package-manager">. When the reader
   picks "pnpm" on one CodeGroup, every other CodeGroup on the page with the
   same SyncKey jumps to "pnpm" too. Scoped per circuit so the choice sticks
   within a session; localStorage persistence is a follow-up. */
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
