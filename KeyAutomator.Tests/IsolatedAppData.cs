using KeyAutomator.Services;

namespace KeyAutomator.Tests;

/// <summary>
/// 実データの config.json / settings.json / error.log を触らないよう、
/// テストごとに一時データフォルダへ差し替える。
/// </summary>
internal sealed class IsolatedAppData : IDisposable
{
    public string Directory { get; }

    public IsolatedAppData()
    {
        Directory = Path.Combine(Path.GetTempPath(), "ka-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        AppPaths.SetDataDirectoryOverride(Directory);
    }

    public void Dispose()
    {
        AppPaths.SetDataDirectoryOverride(null);
        try
        {
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch
        {
            // 一時フォルダの掃除失敗でテストを落とさない
        }
    }
}

/// <summary>ディスクを触るテストの共通セットアップ。</summary>
public abstract class IsolatedDataTestBase
{
    private IsolatedAppData? _data;

    protected string DataDirectory =>
        _data?.Directory ?? throw new InvalidOperationException("IsolatedAppData が初期化されていません");

    [TestInitialize]
    public void BeginIsolatedData() => _data = new IsolatedAppData();

    [TestCleanup]
    public void EndIsolatedData()
    {
        _data?.Dispose();
        _data = null;
        UserDialog.ShowOkHandler = null;
    }
}
