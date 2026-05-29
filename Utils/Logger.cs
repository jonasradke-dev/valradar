namespace ValRadar.Util;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        AppContext.BaseDirectory, "valradar.log"
    );

    private static readonly object Lock = new();

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (Lock)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
    }

    public static void Error(string message, Exception? ex = null)
    {
        var line = ex != null ? $"{message}: {ex.Message}" : message;
        Log($"ERROR {line}");
    }
}