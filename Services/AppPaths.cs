namespace KeyAutomator.Services;

/// <summary>
/// 設定・ログの保存先。
/// exe と同じフォルダが書き込み可能ならそこ（ポータブル）、
/// Program Files など書けない場所なら LocalAppData にフォールバックする。
/// （単一ファイル配布時、AppContext.BaseDirectory は展開先 temp になるため使わない）
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> DataDirectoryLazy = new(ResolveDataDirectory);

    /// <summary>設定ファイルを置くディレクトリ（書き込み可能であることが保証される想定）。</summary>
    public static string DataDirectory => DataDirectoryLazy.Value;

    /// <summary>互換用。DataDirectory と同じ。</summary>
    public static string AppDirectory => DataDirectory;

    public static string ExeDirectory
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

    public static bool IsUsingFallbackDirectory =>
        !string.Equals(DataDirectory, ExeDirectory, StringComparison.OrdinalIgnoreCase);

    public static string ConfigPath => Path.Combine(DataDirectory, "config.json");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string ErrorLogPath => Path.Combine(DataDirectory, "error.log");

    private static string ResolveDataDirectory()
    {
        var exeDir = ExeDirectory;
        if (CanWriteToDirectory(exeDir))
            return exeDir;

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyAutomator");
        Directory.CreateDirectory(fallback);
        TryMigrateSidecarFiles(exeDir, fallback);
        return fallback;
    }

    /// <summary>
    /// Program Files 等に置いた exe 横の設定を、書き込み可能なフォルダへ一度だけコピーする。
    /// </summary>
    public static void TryMigrateSidecarFiles(string sourceDirectory, string destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) ||
            string.IsNullOrWhiteSpace(destinationDirectory) ||
            string.Equals(sourceDirectory, destinationDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var name in new[] { "config.json", "settings.json" })
        {
            var src = Path.Combine(sourceDirectory, name);
            var dst = Path.Combine(destinationDirectory, name);
            if (!File.Exists(src) || File.Exists(dst))
                continue;

            try
            {
                File.Copy(src, dst);
            }
            catch
            {
                // 読めない／コピー失敗でも起動は続行（空の設定で始める）
            }
        }
    }

    public static bool CanWriteToDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".write_probe_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
