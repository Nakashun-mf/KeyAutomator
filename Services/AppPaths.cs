namespace KeyAutomator.Services;

/// <summary>
/// 単一ファイル配布時も、設定は exe と同じフォルダに置く。
/// （AppContext.BaseDirectory は展開先 temp になるため使わない）
/// </summary>
public static class AppPaths
{
    public static string AppDirectory
    {
        get
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var dir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    return dir;
            }

            return AppContext.BaseDirectory;
        }
    }

    public static string ConfigPath => Path.Combine(AppDirectory, "config.json");
    public static string SettingsPath => Path.Combine(AppDirectory, "settings.json");
    public static string ErrorLogPath => Path.Combine(AppDirectory, "error.log");
}
