namespace ShellDocs.Components.Chrome;

// No longer used: the sidebar collapse is driven by shelldocs.js. Kept so existing code compiles.
public class SidebarCollapseState
{
    public bool IsCollapsed { get; private set; }
    public event Action? OnChange;

    public void Toggle() { IsCollapsed = !IsCollapsed; OnChange?.Invoke(); }
    public void Collapse() { if (!IsCollapsed) { IsCollapsed = true;  OnChange?.Invoke(); } }
    public void Expand()   { if (IsCollapsed)  { IsCollapsed = false; OnChange?.Invoke(); } }
}
