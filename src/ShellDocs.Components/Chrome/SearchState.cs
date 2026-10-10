namespace ShellDocs.Components.Chrome;

// No longer used: search is driven by shelldocs.js (shelldocsSearch.open()). Kept so existing code compiles.
public class SearchState
{
    public bool IsOpen { get; private set; }
    public event Action? OnChange;

    public void Open()  { if (!IsOpen) { IsOpen = true;  OnChange?.Invoke(); } }
    public void Close() { if (IsOpen)  { IsOpen = false; OnChange?.Invoke(); } }
    public void Toggle() { IsOpen = !IsOpen; OnChange?.Invoke(); }
}
