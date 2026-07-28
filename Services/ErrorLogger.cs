using System.Text;

namespace KeyAutomator.Services;

public static class ErrorLogger
{
    private static string LogPath => AppPaths.ErrorLogPath;

    public static void Write(Exception ex, string? context = null)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context ?? "Error"}");
            sb.AppendLine(ex.ToString());
            sb.AppendLine(new string('-', 60));
            File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // ignore
        }
    }

    public static void Write(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // ignore
        }
    }
}
