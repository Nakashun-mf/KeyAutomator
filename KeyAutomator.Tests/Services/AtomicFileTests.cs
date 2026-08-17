using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class AtomicFileTests : IsolatedDataTestBase
{
    [TestMethod]
    public void WriteAllText_NewFile_CreatesWithoutBom()
    {
        var path = Path.Combine(DataDirectory, "atomic.json");

        AtomicFile.WriteAllText(path, "{\"ok\":true}");

        var bytes = File.ReadAllBytes(path);
        Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.AreEqual("{\"ok\":true}", File.ReadAllText(path));
        Assert.AreEqual(0, Directory.GetFiles(DataDirectory, "*.tmp").Length);
    }

    [TestMethod]
    public void WriteAllText_ExistingFile_ReplacesContents()
    {
        var path = Path.Combine(DataDirectory, "atomic.json");
        File.WriteAllText(path, "old");

        AtomicFile.WriteAllText(path, "new");

        Assert.AreEqual("new", File.ReadAllText(path));
        Assert.AreEqual(0, Directory.GetFiles(DataDirectory, "*.tmp").Length);
    }

    [TestMethod]
    public void BackupIfExists_Missing_ReturnsEmpty()
    {
        var path = Path.Combine(DataDirectory, "missing.json");
        Assert.AreEqual(string.Empty, AtomicFile.BackupIfExists(path));
    }

    [TestMethod]
    public void BackupIfExists_Present_CopiesAndKeepsOriginal()
    {
        var path = Path.Combine(DataDirectory, "config.json");
        File.WriteAllText(path, "keep-me");

        var backup = AtomicFile.BackupIfExists(path);

        Assert.IsTrue(File.Exists(backup));
        Assert.AreEqual("keep-me", File.ReadAllText(path));
        Assert.AreEqual("keep-me", File.ReadAllText(backup));
        StringAssert.Contains(backup, ".bak.");
    }
}
