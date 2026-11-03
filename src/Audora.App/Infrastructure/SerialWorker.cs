namespace Audora.App.Infrastructure;

/// <summary>
/// Runs audio work one item at a time on a background thread, so the window never freezes
/// while Windows is busy and slider changes are applied in order.
/// </summary>
public static class SerialWorker
{
    private static readonly object Gate = new();
    private static Task _tail = Task.CompletedTask;

    public static void Run(Action action)
    {
        lock (Gate)
        {
            _tail = _tail.ContinueWith(_ =>
            {
                try
                {
                    action();
                }
                catch
                {
                    // a failed audio call must never crash the app
                }
            }, TaskScheduler.Default);
        }
    }
}
