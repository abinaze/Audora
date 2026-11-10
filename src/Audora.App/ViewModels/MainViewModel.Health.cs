namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private HealthViewModel? _health;

    /// <summary>The Health page: a read-only scan and Sound Check. Created the first time it is shown.</summary>
    public HealthViewModel Health => _health ??= new HealthViewModel(_devices, _ui, () => _engineOn);

    public bool IsHealthTab
    {
        get => _tab == 4;
        set
        {
            if (!value)
            {
                _health?.StopSounds();
            }

            SelectTab(4, value);
        }
    }
}
