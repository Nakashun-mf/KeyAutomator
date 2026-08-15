using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class CliRunnerResolveTests
{
    private static List<MacroItem> Macros() =>
    [
        new() { Id = 1, Name = "ログイン処理", Alias = "login_ok" },
        new() { Id = 2, Name = "全選択＆コピー", Alias = "select_copy" },
    ];

    [TestMethod]
    public void IsCliMode_Empty_ReturnsFalse()
    {
        Assert.IsFalse(CliRunner.IsCliMode([]));
        Assert.IsTrue(CliRunner.IsCliMode(["-1"]));
        Assert.IsFalse(CliRunner.IsCliMode(null!));
    }

    [TestMethod]
    [DataRow("-id", "1", 1)]
    [DataRow("-ID", "2", 2)]
    public void ResolveMacro_ByIdFlag_ReturnsMacro(string flag, string value, int expectedId)
    {
        var hit = CliRunner.ResolveMacro([flag, value], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(expectedId, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_ByAliasFlag_ReturnsMacro()
    {
        var hit = CliRunner.ResolveMacro(["-alias", "select_copy"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(2, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_ByShortAliasFlag_ReturnsMacro()
    {
        var hit = CliRunner.ResolveMacro(["-a", "login_ok"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_ByNameFlag_ReturnsMacro()
    {
        var hit = CliRunner.ResolveMacro(["-name", "全選択＆コピー"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(2, hit.Id);
    }

    [TestMethod]
    [DataRow("-1", 1)]
    [DataRow("-2", 2)]
    public void ResolveMacro_ShortIdForm_ReturnsMacro(string arg, int expectedId)
    {
        var hit = CliRunner.ResolveMacro([arg], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(expectedId, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_DashThenId_ReturnsMacro()
    {
        var hit = CliRunner.ResolveMacro(["-", "1"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_ShortAliasForm_ReturnsMacro()
    {
        var hit = CliRunner.ResolveMacro(["-login_ok"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_BareAlias_ReturnsMacro()
    {
        var hit = CliRunner.ResolveMacro(["login_ok"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_Unknown_ReturnsNull()
    {
        Assert.IsNull(CliRunner.ResolveMacro(["-id", "99"], Macros()));
        Assert.IsNull(CliRunner.ResolveMacro(["-alias", "missing"], Macros()));
        Assert.IsNull(CliRunner.ResolveMacro(["-name", "存在しない"], Macros()));
        Assert.IsNull(CliRunner.ResolveMacro(["no_such"], Macros()));
        Assert.IsNull(CliRunner.ResolveMacro([], Macros()));
    }

    [TestMethod]
    public void ResolveMacro_NullMacros_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            CliRunner.ResolveMacro(["-1"], null!));
    }
}

[TestClass]
public class CliRunnerRunTests : IsolatedDataTestBase
{
    [TestMethod]
    public void Run_MissingMacro_ReturnsOneWithoutSendingKeys()
    {
        ConfigStore.Save([new MacroItem { Id = 1, Name = "only", Alias = "only" }]);

        var code = CliRunner.Run(["-id", "99"]);

        Assert.AreEqual(1, code);
        Assert.IsFalse(string.IsNullOrWhiteSpace(ErrorLogger.LastWrittenPath));
    }

    [TestMethod]
    public void Run_CorruptConfig_ReturnsOne()
    {
        File.WriteAllText(ConfigStore.ConfigPath, "{broken");

        var code = CliRunner.Run(["-1"]);

        Assert.AreEqual(1, code);
    }
}
