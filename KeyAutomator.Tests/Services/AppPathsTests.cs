using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class AppPathsTests
{
    [TestMethod]
    public void CanWriteToDirectory_WritableTemp_ReturnsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "keyautomator-write-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.IsTrue(AppPaths.CanWriteToDirectory(dir));
            Assert.IsTrue(Directory.Exists(dir));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void TryMigrateSidecarFiles_CopiesMissingConfigOnce()
    {
        var src = Path.Combine(Path.GetTempPath(), "ka-src-" + Guid.NewGuid().ToString("N"));
        var dst = Path.Combine(Path.GetTempPath(), "ka-dst-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(dst);
            File.WriteAllText(Path.Combine(src, "config.json"), "[{\"id\":1}]");
            File.WriteAllText(Path.Combine(src, "settings.json"), "{\"action_delay_sec\":0.2}");

            AppPaths.TryMigrateSidecarFiles(src, dst);

            Assert.AreEqual("[{\"id\":1}]", File.ReadAllText(Path.Combine(dst, "config.json")));
            Assert.IsTrue(File.Exists(Path.Combine(dst, "settings.json")));

            File.WriteAllText(Path.Combine(src, "config.json"), "CHANGED");
            AppPaths.TryMigrateSidecarFiles(src, dst);
            Assert.AreEqual("[{\"id\":1}]", File.ReadAllText(Path.Combine(dst, "config.json")));
        }
        finally
        {
            if (Directory.Exists(src)) Directory.Delete(src, recursive: true);
            if (Directory.Exists(dst)) Directory.Delete(dst, recursive: true);
        }
    }

    [TestMethod]
    public void TryMigrateSidecarFiles_SameDirectory_DoesNothing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ka-same-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "config.json"), "[]");
            AppPaths.TryMigrateSidecarFiles(dir, dir);
            Assert.AreEqual("[]", File.ReadAllText(Path.Combine(dir, "config.json")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void IsRestrictedInstallDirectory_ProgramFilesLikePath_ReturnsTrue()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(pf))
            Assert.Inconclusive("ProgramFiles パスが取得できない環境");

        Assert.IsTrue(AppPaths.IsRestrictedInstallDirectory(Path.Combine(pf, "KeyAutomator")));
    }

    [TestMethod]
    public void IsRestrictedInstallDirectory_TempPath_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "keyautomator-ok-" + Guid.NewGuid().ToString("N"));
        Assert.IsFalse(AppPaths.IsRestrictedInstallDirectory(dir));
    }

    [TestMethod]
    public void IsRestrictedInstallDirectory_WindowsAppsPath_ReturnsTrue()
    {
        var fake = @"C:\Program Files\WindowsApps\KeyAutomator_1.0.0.0_x64__abc\App";
        Assert.IsTrue(AppPaths.IsRestrictedInstallDirectory(fake));
    }

    [TestMethod]
    public void GetLocalDataDirectory_EndsWithKeyAutomator()
    {
        var path = AppPaths.GetLocalDataDirectory();
        Assert.IsTrue(path.EndsWith("KeyAutomator", StringComparison.OrdinalIgnoreCase));
    }
}

[TestClass]
public class ErrorLoggerTests
{
    [TestMethod]
    public void Write_SetsLastWrittenPath()
    {
        ErrorLogger.Write("unit-test-log-line");
        Assert.IsFalse(string.IsNullOrWhiteSpace(ErrorLogger.LastWrittenPath));
        Assert.IsTrue(File.Exists(ErrorLogger.LastWrittenPath));
    }
}
