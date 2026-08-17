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
            var bytes = File.ReadAllBytes(path);
            Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void WriteAllText_OverwritesExistingWithoutLeavingTemp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "keyautomator-atomic-dir-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "config.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "old");
            AtomicFile.WriteAllText(path, "new-content");

            Assert.AreEqual("new-content", File.ReadAllText(path));
            Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp").Length);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
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

    [TestMethod]
    public void BackupIfExists_Missing_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "keyautomator-missing-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.AreEqual(string.Empty, AtomicFile.BackupIfExists(path));
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
