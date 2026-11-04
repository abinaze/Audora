using System.IO;

namespace Audora.App.Infrastructure;

/// <summary>Small local log file for troubleshooting. Nothing is ever sent anywhere.</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Audora", "logs");

    public static string FilePath => Path.Combine(Folder, "audora.log");

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > 1_000_000)
                {
                    File.Move(FilePath, Path.Combine(Folder, "audora.old.log"), true);
                }

                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // logging must never break the app
        }
    }
}
