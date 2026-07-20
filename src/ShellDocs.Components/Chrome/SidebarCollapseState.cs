namespace ShellDocs.Components.Chrome;

/* Desktop-only collapse state for the Sidebar-variant docs layout. Distinct
   from MobileNavState — mobile drawer is temporary/overlay, this is a
   persistent "hide the sidebar until I click reopen" toggle. */
public class SidebarCollapseState
{
    public bool IsCollapsed { get; private set; }
    public event Action? OnChange;

    public void Toggle() { IsCollapsed = !IsCollapsed; OnChange?.Invoke(); }
    public void Collapse() { if (!IsCollapsed) { IsCollapsed = true;  OnChange?.Invoke(); } }
    public void Expand()   { if (IsCollapsed)  { IsCollapsed = false; OnChange?.Invoke(); } }
}
