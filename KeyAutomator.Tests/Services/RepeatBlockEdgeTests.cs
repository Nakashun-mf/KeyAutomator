using KeyAutomator.Models;
using KeyAutomator.Services;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class RepeatBlockEdgeTests
{
    [TestMethod]
    public void TryParseCount_MaxCount_ReturnsTrue()
    {
        Assert.IsTrue(RepeatBlock.TryParseCount(RepeatBlock.MaxCount.ToString(), out var count));
        Assert.AreEqual(RepeatBlock.MaxCount, count);
    }

    [TestMethod]
    public void TryParseCount_Whitespace_ReturnsFalse()
    {
        Assert.IsFalse(RepeatBlock.TryParseCount("   ", out _));
    }

    [TestMethod]
    public void FindMatchingEnd_WhenIndexIsNotStart_ReturnsMinusOne()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "text", Value = "a" },
            new() { Type = "end_repeat", Value = "" },
        };

        Assert.AreEqual(-1, RepeatBlock.FindMatchingEnd(actions, 0));
        Assert.AreEqual(-1, RepeatBlock.FindMatchingEnd(actions, -1));
        Assert.AreEqual(-1, RepeatBlock.FindMatchingEnd(actions, 99));
    }

    [TestMethod]
    public void IsMarker_RecognizesStartAndEndCaseInsensitive()
    {
        Assert.IsTrue(RepeatBlock.IsMarker("REPEAT"));
        Assert.IsTrue(RepeatBlock.IsMarker("End_Repeat"));
        Assert.IsFalse(RepeatBlock.IsMarker("text"));
        Assert.IsFalse(RepeatBlock.IsMarker(null));
    }

    [TestMethod]
    public void TryValidate_NestedBalanced_ReturnsTrue()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "2" },
            new() { Type = "repeat", Value = "3" },
            new() { Type = "text", Value = "a" },
            new() { Type = "end_repeat", Value = "" },
            new() { Type = "end_repeat", Value = "" },
        };

        Assert.IsTrue(RepeatBlock.TryValidate(actions, out var error));
        Assert.AreEqual(string.Empty, error);
    }
}

[TestClass]
public class AtomicFileEdgeTests
{
    [TestMethod]
    public void BackupIfExists_MissingFile_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "ka-missing-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.AreEqual(string.Empty, AtomicFile.BackupIfExists(path));
    }

    [TestMethod]
    public void WriteAllText_CreatesMissingDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ka-atomic-nested-" + Guid.NewGuid().ToString("N"), "sub");
        var path = Path.Combine(dir, "settings.json");
        try
        {
            AtomicFile.WriteAllText(path, "{\"ok\":true}");
            Assert.AreEqual("{\"ok\":true}", File.ReadAllText(path));
        }
        finally
        {
            var root = Directory.GetParent(dir)!.FullName;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

[TestClass]
public class ErrorLoggerExceptionTests : IsolatedDataTestBase
{
    [TestMethod]
    public void Write_Exception_IncludesContextAndType()
    {
        ErrorLogger.Write(new InvalidOperationException("boom"), Loc.Get("Log_SaveFailed"));

        var text = File.ReadAllText(ErrorLogger.LastWrittenPath!);
        StringAssert.Contains(text, Loc.Get("Log_SaveFailed"));
        StringAssert.Contains(text, "InvalidOperationException");
        StringAssert.Contains(text, "boom");
    }
}

[TestClass]
public class AppPathsEdgeTests
{
    [TestMethod]
    public void IsRestrictedInstallDirectory_Empty_ReturnsTrue()
    {
        Assert.IsTrue(AppPaths.IsRestrictedInstallDirectory(null));
        Assert.IsTrue(AppPaths.IsRestrictedInstallDirectory(""));
        Assert.IsTrue(AppPaths.IsRestrictedInstallDirectory("   "));
    }

    [TestMethod]
    public void TryMigrateSidecarFiles_EmptySource_DoesNothing()
    {
        var dst = Path.Combine(Path.GetTempPath(), "ka-dst-empty-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dst);
            AppPaths.TryMigrateSidecarFiles("", dst);
            AppPaths.TryMigrateSidecarFiles(dst, "");
            Assert.AreEqual(0, Directory.GetFiles(dst).Length);
        }
        finally
        {
            if (Directory.Exists(dst))
                Directory.Delete(dst, recursive: true);
        }
    }

    [TestMethod]
    public void CanWriteToDirectory_InvalidPath_ReturnsFalse()
    {
        Assert.IsFalse(AppPaths.CanWriteToDirectory("::not-a-path::"));
    }
}

[TestClass]
public class BuiltInSamplesContractTests
{
    [TestMethod]
    public void Create_HasUniqueIdsAndValidAliases()
    {
        var samples = BuiltInSamples.Create();
        Assert.IsTrue(samples.Count >= 3);
        Assert.AreEqual(samples.Count, samples.Select(m => m.Id).Distinct().Count());
        Assert.AreEqual(
            samples.Count(m => !string.IsNullOrWhiteSpace(m.Alias)),
            samples.Select(m => m.Alias.ToLowerInvariant()).Distinct().Count());

        foreach (var macro in samples)
        {
            Assert.IsTrue(MacroItem.IsValidAlias(macro.Alias, out _), macro.Alias);
            Assert.IsFalse(string.IsNullOrWhiteSpace(macro.Name));
            Assert.IsTrue(RepeatBlock.TryValidate(macro.Actions, out var loopError), loopError);
            foreach (var action in macro.Actions)
            {
                if (string.Equals(action.Type, "hotkey", StringComparison.OrdinalIgnoreCase))
                    Assert.IsTrue(ActionEditItem.IsValidHotkey(action.Value, out _), action.Value);
            }
        }
    }
}
