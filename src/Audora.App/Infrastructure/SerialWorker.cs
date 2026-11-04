namespace Audora.App.Infrastructure;

/// <summary>
/// Runs audio work one item at a time on a background thread, so the window never freezes
/// while Windows is busy and slider changes are applied in order.
/// </summary>
public static class SerialWorker
{
    private static readonly object Gate = new();
    private static Task _tail = Task.CompletedTask;
    private static bool _closed;

    public static void Run(Action action)
    {
        lock (Gate)
        {
            if (_closed)
            {
                return;
            }

            _tail = _tail.ContinueWith(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error("Audio task failed", ex);
                }
            }, TaskScheduler.Default);
        }
    }

    /// <summary>Finishes queued work (up to the timeout) and refuses anything new. Used when the app closes.</summary>
    public static void Shutdown(TimeSpan timeout)
    {
        Task tail;
        lock (Gate)
        {
            _closed = true;
            tail = _tail;
        }

        try
        {
            tail.Wait(timeout);
        }
        catch
        {
            // already logged inside the task
        }
    }
}
