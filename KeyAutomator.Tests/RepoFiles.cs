namespace KeyAutomator.Tests;

/// <summary>テストからリポジトリのソース／マニフェストを読むためのルート解決。</summary>
internal static class RepoFiles
{
    public static string Root { get; } = FindRoot();

    public static string Combine(params string[] relative) =>
        Path.Combine(new[] { Root }.Concat(relative).ToArray());

    public static string Read(params string[] relative) =>
        File.ReadAllText(Combine(relative));

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KeyAutomator.csproj")) &&
                File.Exists(Path.Combine(dir.FullName, "config.sample.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        Assert.Fail("リポジトリルート（KeyAutomator.csproj）が見つかりません");
        return string.Empty;
    }
}
