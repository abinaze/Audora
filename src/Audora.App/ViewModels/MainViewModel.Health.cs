namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private HealthViewModel? _health;

    /// <summary>The Health page: a read-only scan of Windows audio. Created the first time it is shown.</summary>
    public HealthViewModel Health => _health ??= new HealthViewModel(_devices);

    public bool IsHealthTab
    {
        get => _tab == 4;
        set => SelectTab(4, value);
    }
}
