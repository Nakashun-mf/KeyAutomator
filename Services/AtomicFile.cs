using System.Text;

namespace KeyAutomator.Services;

/// <summary>一時ファイル経由で置換し、途中クラッシュで JSON が壊れにくくする。</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string contents, Encoding? encoding = null)
    {
        encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, contents, encoding);

            if (File.Exists(path))
            {
                try
                {
                    // Windows で上書き置換が安定しやすい
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                    return;
                }
                catch
                {
                    File.Copy(tempPath, path, overwrite: true);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    public static string BackupIfExists(string path)
    {
        if (!File.Exists(path))
            return string.Empty;

        var backup = path + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss");
        File.Copy(path, backup, overwrite: true);
        return backup;
    }
}
