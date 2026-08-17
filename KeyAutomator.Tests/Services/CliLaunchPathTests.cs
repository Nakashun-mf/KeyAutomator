using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class CliLaunchPathTests
{
    [TestMethod]
    public void ResolveCliLaunchPath_PackagedWhenAliasExists_ReturnsAlias()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ka-alias-" + Guid.NewGuid().ToString("N"));
        var alias = Path.Combine(dir, "KeyAutomator.exe");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(alias, "x");
            var process = @"C:\Program Files\WindowsApps\pryzo.KeyAutomator\KeyAutomator.exe";

            var path = AppPaths.ResolveCliLaunchPath(true, process, alias, File.Exists);

            Assert.AreEqual(alias, path);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveCliLaunchPath_PackagedWhenAliasMissing_ReturnsProcessPath()
    {
        var process = @"C:\Program Files\WindowsApps\pryzo.KeyAutomator\KeyAutomator.exe";
        var alias = Path.Combine(Path.GetTempPath(), "ka-missing-" + Guid.NewGuid().ToString("N"), "KeyAutomator.exe");

        var path = AppPaths.ResolveCliLaunchPath(true, process, alias, File.Exists);

        Assert.AreEqual(process, path);
    }

    [TestMethod]
    public void ResolveCliLaunchPath_Unpackaged_ReturnsProcessPathEvenIfAliasExists()
    {
        var process = @"D:\Tools\KeyAutomator.exe";
        var alias = @"C:\Users\me\AppData\Local\Microsoft\WindowsApps\KeyAutomator.exe";

        var path = AppPaths.ResolveCliLaunchPath(false, process, alias, _ => true);

        Assert.AreEqual(process, path);
    }

    [TestMethod]
    public void ResolveCliLaunchPath_EmptyProcessPath_FallsBackToBaseDirectory()
    {
        var path = AppPaths.ResolveCliLaunchPath(false, "  ", "unused", _ => false);

        Assert.AreEqual(
            Path.Combine(AppContext.BaseDirectory, AppPaths.CliExecutableFileName),
            path);
    }

    [TestMethod]
    public void GetAppExecutionAliasPath_EndsWithWindowsAppsExe()
    {
        var expected = Path.Combine("Microsoft", "WindowsApps", AppPaths.CliExecutableFileName);
        Assert.IsTrue(
            AppPaths.GetAppExecutionAliasPath().EndsWith(expected, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GetCliLaunchPath_ReturnsNonEmptyPath()
    {
        var path = AppPaths.GetCliLaunchPath();
        Assert.IsFalse(string.IsNullOrWhiteSpace(path));
        Assert.IsTrue(path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void QuoteCliPath_WrapsInDoubleQuotes()
    {
        Assert.AreEqual(
            "\"C:\\Prog Files\\KeyAutomator.exe\"",
            AppPaths.QuoteCliPath(@"C:\Prog Files\KeyAutomator.exe"));
    }

    [TestMethod]
    public void QuoteCliPath_Empty_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => AppPaths.QuoteCliPath(" "));
    }

    [TestMethod]
    public void QuoteCliPath_ContainsQuote_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => AppPaths.QuoteCliPath("C:\\a\"b.exe"));
    }

    [TestMethod]
    public void FormatLaunchCommand_WithAlias_PrefersAliasOverId()
    {
        var cmd = CliRunner.FormatLaunchCommand(@"C:\a b\KeyAutomator.exe", "login_ok", 1);
        Assert.AreEqual("\"C:\\a b\\KeyAutomator.exe\" -alias login_ok", cmd);
    }

    [TestMethod]
    public void FormatLaunchCommand_WithoutAlias_UsesId()
    {
        var cmd = CliRunner.FormatLaunchCommand(@"C:\KA\KeyAutomator.exe", "  ", 3);
        Assert.AreEqual("\"C:\\KA\\KeyAutomator.exe\" -id 3", cmd);
    }

    [TestMethod]
    public void FormatLaunchCommand_WithoutAliasOrId_ReturnsQuotedPath()
    {
        var cmd = CliRunner.FormatLaunchCommand(@"C:\KA\KeyAutomator.exe");
        Assert.AreEqual("\"C:\\KA\\KeyAutomator.exe\"", cmd);
    }
}
