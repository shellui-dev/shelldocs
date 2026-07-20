namespace ShellDocs.Components.Chrome;

/* Global open/close state for <SearchDialog>. Any component (header chip,
   sidebar chip, or the Cmd+K JS shortcut) can call Open(); the dialog itself
   subscribes to OnChange to re-render. */
public class SearchState
{
    public bool IsOpen { get; private set; }
    public event Action? OnChange;

    public void Open()  { if (!IsOpen) { IsOpen = true;  OnChange?.Invoke(); } }
    public void Close() { if (IsOpen)  { IsOpen = false; OnChange?.Invoke(); } }
    public void Toggle() { IsOpen = !IsOpen; OnChange?.Invoke(); }
}
