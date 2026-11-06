using System.Threading;
using System.Windows;
using Audora.App.Infrastructure;

namespace Audora.App;

public partial class App : Application
{
    private const string ShowSignalName = @"Local\Audora.Show";

    private static Mutex? _singleInstance;
    private static EventWaitHandle? _showSignal;

    /// <summary>True once the user chose Quit (or Windows is closing), so closing really closes.</summary>
    public static bool IsQuitting { get; private set; }

    public static void Quit()
    {
        IsQuitting = true;
        Current.Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Two copies would both listen to the same audio and fight over the devices.
        _singleInstance = new Mutex(true, @"Local\Audora.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            SignalRunningCopy();
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
        SessionEnding += (_, _) => IsQuitting = true;

        Log.Info("Audora started");
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.ShowAtStart(e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase));
        ListenForShowSignal(window);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("Audora closed");
        try { _singleInstance?.ReleaseMutex(); } catch { }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    // A second launch asks the running copy to show its window.
    private static void SignalRunningCopy()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(ShowSignalName);
            signal.Set();
        }
        catch
        {
            // the first copy is still starting up; nothing to do
        }
    }

    private void ListenForShowSignal(MainWindow window)
    {
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        var thread = new Thread(() =>
        {
            while (_showSignal.WaitOne())
            {
                if (IsQuitting)
                {
                    break;
                }

                Dispatcher.BeginInvoke(window.ShowFromTray);
            }
        })
        {
            IsBackground = true
        };
        thread.Start();
    }
}
