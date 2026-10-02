namespace ShellDocs.Components.Chrome;

// Desktop collapse for the Sidebar layout — persistent, unlike the
// temporary mobile drawer in MobileNavState.
public class SidebarCollapseState
{
    public bool IsCollapsed { get; private set; }
    public event Action? OnChange;

    public void Toggle() { IsCollapsed = !IsCollapsed; OnChange?.Invoke(); }
    public void Collapse() { if (!IsCollapsed) { IsCollapsed = true;  OnChange?.Invoke(); } }
    public void Expand()   { if (IsCollapsed)  { IsCollapsed = false; OnChange?.Invoke(); } }
}
