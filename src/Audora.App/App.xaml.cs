using System.Threading;
using System.Windows;
using Audora.App.Infrastructure;

namespace Audora.App;

public partial class App : Application
{
    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Two copies would both listen to the same audio and fight over the devices.
        _singleInstance = new Mutex(true, @"Local\Audora.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("Audora is already running. Look for its window in the taskbar.", "Audora",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Registered first so errors while the window is being created are also caught.
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unexpected error in the window", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unexpected error", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Background task error", args.Exception);
            args.SetObserved();
        };

        Log.Info("Audora started");
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("Audora closed");
        try { _singleInstance?.ReleaseMutex(); } catch { }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
