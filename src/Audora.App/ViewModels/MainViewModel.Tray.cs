using Audora.App.Infrastructure;

namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private bool _closeToTray = true;
    private bool _minimizeToTray;
    private bool _trayHintShown;

    /// <summary>Closing the window keeps Audora running in the tray, so the devices keep playing.</summary>
    public bool CloseToTray
    {
        get => _closeToTray;
        set => Set(ref _closeToTray, value);
    }

    /// <summary>Minimizing hides the window from the taskbar.</summary>
    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => Set(ref _minimizeToTray, value);
    }

    public bool TrayHintShown
    {
        get => _trayHintShown;
        set => Set(ref _trayHintShown, value);
    }

    /// <summary>Read from and written to the Windows startup list, not to the settings file.</summary>
    public bool StartWithWindows
    {
        get => StartupRegistration.IsEnabled;
        set
        {
            StartupRegistration.Set(value);
            Raise();
        }
    }
}
