using System.Text;

namespace KeyAutomator.Services;

public static class ErrorLogger
{
    /// <summary>直近に書き込めたログファイルのフルパス。書けなければ null。</summary>
    public static string? LastWrittenPath { get; private set; }

    public static void Write(Exception ex, string? context = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context ?? "Error"}");
        sb.AppendLine(ex.ToString());
        sb.AppendLine(new string('-', 60));
        WriteRaw(sb.ToString());
    }

    public static void Write(string message)
    {
        WriteRaw($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void WriteRaw(string text)
    {
        // 主経路（DataDirectory）→ だめなら Temp
        var candidates = new[]
        {
            AppPaths.ErrorLogPath,
            Path.Combine(Path.GetTempPath(), "KeyAutomator-error.log")
        };

        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(path, text, Encoding.UTF8);
                LastWrittenPath = path;
                return;
            }
            catch
            {
                // try next
            }
        }
    }
}
