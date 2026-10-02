namespace ShellDocs.Components.Chrome;

public class SearchState
{
    public bool IsOpen { get; private set; }
    public event Action? OnChange;

    public void Open()  { if (!IsOpen) { IsOpen = true;  OnChange?.Invoke(); } }
    public void Close() { if (IsOpen)  { IsOpen = false; OnChange?.Invoke(); } }
    public void Toggle() { IsOpen = !IsOpen; OnChange?.Invoke(); }
}
