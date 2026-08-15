using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class CliRunnerEdgeTests
{
    private static List<MacroItem> Macros() =>
    [
        new() { Id = 1, Name = "ログイン処理", Alias = "login_ok" },
        new() { Id = 2, Name = "全選択＆コピー", Alias = "select_copy" },
    ];

    [TestMethod]
    [DataRow("-h")]
    [DataRow("--help")]
    [DataRow("/?")]
    [DataRow("-?")]
    [DataRow("help")]
    [DataRow("HELP")]
    public void IsHelpRequest_AllDocumentedFlags_ReturnsTrue(string flag)
    {
        Assert.IsTrue(CliRunner.IsHelpRequest([flag]));
    }

    [TestMethod]
    public void IsHelpRequest_UnrelatedArg_ReturnsFalse()
    {
        Assert.IsFalse(CliRunner.IsHelpRequest(["-1"]));
        Assert.IsFalse(CliRunner.IsHelpRequest(["-alias", "login_ok"]));
        Assert.IsFalse(CliRunner.IsHelpRequest([]));
    }

    [TestMethod]
    public void ResolveMacro_IdFlagTakesPrecedenceOverAlias()
    {
        var hit = CliRunner.ResolveMacro(["-alias", "select_copy", "-id", "1"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_IdFlagWithoutValue_ReturnsNull()
    {
        Assert.IsNull(CliRunner.ResolveMacro(["-id"], Macros()));
        Assert.IsNull(CliRunner.ResolveMacro(["-alias"], Macros()));
        Assert.IsNull(CliRunner.ResolveMacro(["-name"], Macros()));
    }

    [TestMethod]
    public void ResolveMacro_DashThenAlias_ReturnsMacro()
    {
        var hit = CliRunner.ResolveMacro(["-", "login_ok"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void ResolveMacro_ShortFormUnknownId_ReturnsNull()
    {
        Assert.IsNull(CliRunner.ResolveMacro(["-99"], Macros()));
    }

    [TestMethod]
    public void ResolveMacro_NameFlagIsCaseInsensitive()
    {
        var hit = CliRunner.ResolveMacro(["-NAME", "ログイン処理"], Macros());

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void GetHelpText_DocumentsExitCodesAndLogLocation()
    {
        var help = CliRunner.GetHelpText();
        StringAssert.Contains(help, "終了コード");
        StringAssert.Contains(help, "error.log");
        StringAssert.Contains(help, "-name");
    }
}

[TestClass]
public class CliRunnerHelpRunTests : IsolatedDataTestBase
{
    [TestMethod]
    [DataRow("--help")]
    [DataRow("-?")]
    [DataRow("help")]
    public void Run_HelpFlags_ReturnsZeroWithoutTouchingConfig(string flag)
    {
        Assert.AreEqual(0, CliRunner.Run([flag]));
        Assert.IsFalse(File.Exists(ConfigStore.ConfigPath));
    }
}
