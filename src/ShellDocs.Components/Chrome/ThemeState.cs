namespace ShellDocs.Components.Chrome;

/* Shared theme state for every ThemeToggle instance on the page.
   Without this, each toggle held its own bool and the second toggle stayed
   stale when the first one flipped the theme. */
public class ThemeState
{
    public bool IsDark { get; private set; }
    public bool IsInitialized { get; private set; }
    public event Action? OnChange;

    // Called once from the first ThemeToggle that hydrates — reads the actual
    // <html> class the pre-Blazor head script set. Later toggles skip re-init.
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
