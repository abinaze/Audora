namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private HealthViewModel? _health;

    /// <summary>The Health page: a read-only scan and Sound Check. Created the first time it is shown.</summary>
    public HealthViewModel Health =>
        _health ??= new HealthViewModel(_devices, _ui, () => _engineOn, CopiesTo, () => EngineOn = false);

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

    /// <summary>The names of the devices Audora copies the default device's sound to, or an empty string.</summary>
    private string CopiesTo() =>
        string.Join(", ", Devices.Where(d => d.IsSelected && !d.IsDefault).Select(d => d.Name));
}
