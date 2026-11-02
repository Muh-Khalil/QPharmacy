namespace QPharmacy.Services;

public class SidebarService
{
    public bool IsMobileOpen { get; private set; } = false;
    public event Action? OnStateChanged;

    public void Toggle()
    {
        IsMobileOpen = !IsMobileOpen;
        OnStateChanged?.Invoke();
    }

    public void Close()
    {
        IsMobileOpen = false;
        OnStateChanged?.Invoke();
    }
}
