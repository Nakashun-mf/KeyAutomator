using System.Text.Json;
using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class ConfigStoreLookupTests
{
    private static List<MacroItem> SampleMacros() =>
    [
        new() { Id = 1, Name = "ログイン", Alias = "login_ok" },
        new() { Id = 5, Name = "全選択＆コピー", Alias = "select_copy" },
    ];

    [TestMethod]
    public void FindById_Existing_ReturnsMacro()
    {
        var hit = ConfigStore.FindById(SampleMacros(), 5);

        Assert.IsNotNull(hit);
        Assert.AreEqual("select_copy", hit.Alias);
    }

    [TestMethod]
    public void FindById_Missing_ReturnsNull()
    {
        Assert.IsNull(ConfigStore.FindById(SampleMacros(), 99));
    }

    [TestMethod]
    public void FindByName_IsCaseInsensitive()
    {
        var macros = new List<MacroItem>
        {
            new() { Id = 1, Name = "Login Flow", Alias = "login_ok" }
        };

        var hit = ConfigStore.FindByName(macros, "login flow");

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void FindByAlias_IsCaseInsensitive()
    {
        var hit = ConfigStore.FindByAlias(SampleMacros(), "LOGIN_OK");

        Assert.IsNotNull(hit);
        Assert.AreEqual(1, hit.Id);
    }

    [TestMethod]
    public void FindByAlias_EmptyAlias_DoesNotMatch()
    {
        var macros = new List<MacroItem>
        {
            new() { Id = 1, Name = "無名", Alias = "" }
        };

        Assert.IsNull(ConfigStore.FindByAlias(macros, ""));
        Assert.IsNull(ConfigStore.FindByAlias(macros, "   "));
    }

    [TestMethod]
    public void NextId_Empty_ReturnsOne()
    {
        Assert.AreEqual(1, ConfigStore.NextId([]));
    }

    [TestMethod]
    public void NextId_Existing_ReturnsMaxPlusOne()
    {
        Assert.AreEqual(6, ConfigStore.NextId(SampleMacros()));
    }
}

[TestClass]
public class ConfigStorePersistenceTests : IsolatedDataTestBase
{
    [TestMethod]
    public void Load_WhenMissing_WritesSamplesAndReturnsThem()
    {
        Assert.IsFalse(File.Exists(ConfigStore.ConfigPath));

        var loaded = ConfigStore.Load();

        Assert.IsTrue(loaded.Count >= 3);
        Assert.IsTrue(File.Exists(ConfigStore.ConfigPath));
        Assert.IsTrue(loaded.Any(m => m.Alias == "login_ok"));
    }

    [TestMethod]
    public void SaveThenLoad_RoundTripsMacros()
    {
        var original = new List<MacroItem>
        {
            new()
            {
                Id = 10,
                Name = "往復",
                Alias = "round_trip",
                DelaySec = 1.5,
                Actions =
                [
                    new ActionItem { Type = "text", Value = "hello" },
                    new ActionItem { Type = "key", Value = "ENTER" },
                ]
            }
        };

        ConfigStore.Save(original);
        var loaded = ConfigStore.Load();

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual(10, loaded[0].Id);
        Assert.AreEqual("往復", loaded[0].Name);
        Assert.AreEqual("round_trip", loaded[0].Alias);
        Assert.AreEqual(1.5, loaded[0].DelaySec);
        Assert.AreEqual(2, loaded[0].Actions.Count);
        Assert.AreEqual("text", loaded[0].Actions[0].Type);
        Assert.AreEqual("hello", loaded[0].Actions[0].Value);
    }

    [TestMethod]
    public void Load_EmptyFile_ReturnsEmptyList()
    {
        File.WriteAllText(ConfigStore.ConfigPath, "   ");

        var loaded = ConfigStore.Load();

        Assert.AreEqual(0, loaded.Count);
    }

    [TestMethod]
    public void Load_CorruptJson_ThrowsAndDoesNotOverwrite()
    {
        const string broken = "{not-json";
        File.WriteAllText(ConfigStore.ConfigPath, broken);

        Assert.ThrowsException<JsonException>(() => ConfigStore.Load());
        Assert.AreEqual(broken, File.ReadAllText(ConfigStore.ConfigPath));
    }

    [TestMethod]
    public void Load_JsonNull_ThrowsInvalidDataException()
    {
        File.WriteAllText(ConfigStore.ConfigPath, "null");

        var ex = Assert.ThrowsException<InvalidDataException>(() => ConfigStore.Load());
        StringAssert.Contains(ex.Message, Loc.Get("Ex_ConfigUnreadable"));
        Assert.AreEqual("null", File.ReadAllText(ConfigStore.ConfigPath));
    }
}
