using KeyAutomator.Services;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class AtomicFileTests
{
    [TestMethod]
    public void WriteAllText_CreatesFileWithContent()
    {
        var path = Path.Combine(Path.GetTempPath(), "keyautomator-atomic-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            AtomicFile.WriteAllText(path, "[{\"id\":1}]");
            Assert.AreEqual("[{\"id\":1}]", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void BackupIfExists_CreatesTimestampedCopy()
    {
        var path = Path.Combine(Path.GetTempPath(), "keyautomator-bak-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "broken");
            var backup = AtomicFile.BackupIfExists(path);
            Assert.IsTrue(File.Exists(backup));
            Assert.AreEqual("broken", File.ReadAllText(backup));
            File.Delete(backup);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

[TestClass]
public class CliRunnerHelpTests
{
    [TestMethod]
    public void IsHelpRequest_WithDashH_ReturnsTrue()
    {
        Assert.IsTrue(CliRunner.IsHelpRequest(["-h"]));
        Assert.IsTrue(CliRunner.IsHelpRequest(["--help"]));
        Assert.IsTrue(CliRunner.IsHelpRequest(["/?"]));
    }

    [TestMethod]
    public void GetHelpText_ContainsUsageExamples()
    {
        var help = CliRunner.GetHelpText();
        StringAssert.Contains(help, "-id");
        StringAssert.Contains(help, "-alias");
        StringAssert.Contains(help, "確認アクション");
    }

    [TestMethod]
    public void Run_HelpRequest_ReturnsZero()
    {
        Assert.AreEqual(0, CliRunner.Run(["-h"]));
    }
}

[TestClass]
public class HotkeyValidationTests
{
    [TestMethod]
    public void IsValidHotkey_ModifierOnly_ReturnsFalse()
    {
        Assert.IsFalse(ActionEditItem.IsValidHotkey("CTRL+SHIFT", out var error));
        StringAssert.Contains(error, "修飾キー以外");
    }

    [TestMethod]
    public void IsValidHotkey_ValidChord_ReturnsTrue()
    {
        Assert.IsTrue(ActionEditItem.IsValidHotkey("CTRL+S", out var error));
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void IsValidHotkey_DuplicateModifier_ReturnsFalse()
    {
        Assert.IsFalse(ActionEditItem.IsValidHotkey("CTRL+CTRL+S", out _));
    }
}
