using System.Windows;
using Audora.App.Infrastructure;

namespace Audora.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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
        base.OnExit(e);
    }
}
