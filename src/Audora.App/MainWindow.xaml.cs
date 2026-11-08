using System.ComponentModel;
using System.Windows;
using Audora.App.Infrastructure;
using Audora.App.ViewModels;

namespace Audora.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TrayIconService _tray;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(Dispatcher);
        DataContext = _viewModel;
        _tray = new TrayIconService(this, _viewModel);

        SourceInitialized += (_, _) => TitleBar.Apply(this, _viewModel.IsDark);
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
            {
                HideToTray();
            }
        };
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _tray.Dispose();
            _viewModel.Dispose();
        };
    }

    /// <summary>Shows the window, or keeps it in the tray when Audora was started with Windows.</summary>
    public void ShowAtStart(bool startMinimized)
    {
        if (!startMinimized)
        {
            Show();
            return;
        }

        if (_viewModel.CloseToTray || _viewModel.MinimizeToTray)
        {
            return; // stays in the tray
        }

        WindowState = WindowState.Minimized;
        Show();
    }

    public void HideToTray()
    {
        Hide();
        _tray.ShowHintOnce();
    }

    public void ShowFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (App.IsQuitting || !_viewModel.CloseToTray || !_viewModel.EngineOn)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }
}
