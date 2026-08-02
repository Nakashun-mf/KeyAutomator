namespace KeyAutomator.Services;

/// <summary>
/// 設定・ログの保存先。
/// - ポータブル（書き込み可能な場所に exe を置いた場合）: exe と同じフォルダ
/// - 書けない／書き込みが不安定な場所: %LocalAppData%\KeyAutomator
/// - パッケージ実行時: 常に LocalAppData（インストール先へは書かない）
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
        var appData = GetLocalDataDirectory();

        // パッケージ実行や Program Files 配下など、exe 横に書くとしんどい場所は最初から AppData へ
        if (IsRunningPackaged() || IsRestrictedInstallDirectory(exeDir))
        {
            Directory.CreateDirectory(appData);
            TryMigrateSidecarFiles(exeDir, appData);
            return appData;
        }

        if (CanWriteToDirectory(exeDir))
            return exeDir;

        Directory.CreateDirectory(appData);
        TryMigrateSidecarFiles(exeDir, appData);
        return appData;
    }

    public static string GetLocalDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyAutomator");

    /// <summary>
    /// インストール先・保護フォルダなど、ユーザーデータ向きでないパスかどうか。
    /// </summary>
    public static bool IsRestrictedInstallDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return true;

        string full;
        try
        {
            full = Path.GetFullPath(directory);
        }
        catch
        {
            return true;
        }

        foreach (var root in GetRestrictedRoots())
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // WindowsApps（ストア／MSIX の実体置き場）
        if (full.Contains($"{Path.DirectorySeparatorChar}WindowsApps{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<string> GetRestrictedRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.System);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    }

    /// <summary>MSIX 等のパッケージとして動作しているか。</summary>
    public static bool IsRunningPackaged()
    {
        try
        {
            // unpackaged だと InvalidOperationException
            _ = Windows.ApplicationModel.Package.Current.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// exe 横の設定を、書き込み可能なフォルダへ一度だけコピーする。
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
