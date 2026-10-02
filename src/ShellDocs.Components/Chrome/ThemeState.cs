namespace ShellDocs.Components.Chrome;

// Shared by every ThemeToggle on the page so they stay in sync.
public class ThemeState
{
    public bool IsDark { get; private set; }
    public bool IsInitialized { get; private set; }
    public event Action? OnChange;

    // Seeded once from the <html> class the pre-Blazor head script set.
    public void Init(bool isDark)
    {
        if (IsInitialized) return;
        IsDark = isDark;
        IsInitialized = true;
        OnChange?.Invoke();
    }

    public void Set(bool isDark)
    {
        if (IsDark == isDark) return;
        IsDark = isDark;
        OnChange?.Invoke();
    }

    public void Toggle() => Set(!IsDark);
}
