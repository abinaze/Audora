using Audora.App.Infrastructure;

namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private bool _closeToTray = true;
    private bool _minimizeToTray;
    private bool _trayHintShown;
    private string _startupMessage = string.Empty;

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

    public string StartupMessage
    {
        get => _startupMessage;
        private set => Set(ref _startupMessage, value);
    }

    /// <summary>Read from and written to the Windows startup list, not to the settings file.</summary>
    public bool StartWithWindows
    {
        get => StartupRegistration.IsEnabled;
        set
        {
            if (value && !StartupRegistration.Set(true))
            {
                StartupMessage = "Start with Windows only works from the installed or portable version, not from a build in a source folder. Install Audora with the installer first.";
            }
            else
            {
                if (!value)
                {
                    StartupRegistration.Set(false);
                }

                StartupMessage = string.Empty;
            }

            Raise();
        }
    }
}
