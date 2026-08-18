using System.Text;
using System.Text.Json;
using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class ConfigStoreEdgeTests : IsolatedDataTestBase
{
    [TestMethod]
    public void Load_EmptyArray_ReturnsEmptyWithoutInjectingSamples()
    {
        File.WriteAllText(ConfigStore.ConfigPath, "[]");

        var loaded = ConfigStore.Load();

        Assert.AreEqual(0, loaded.Count);
        Assert.AreEqual("[]", File.ReadAllText(ConfigStore.ConfigPath).Trim());
    }

    [TestMethod]
    public void Load_UnknownProperties_AreIgnored()
    {
        File.WriteAllText(
            ConfigStore.ConfigPath,
            """[{"id":7,"name":"拡張","alias":"ext","delay_sec":1,"actions":[],"future":true}]""");

        var loaded = ConfigStore.Load();

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual(7, loaded[0].Id);
        Assert.AreEqual("ext", loaded[0].Alias);
        Assert.AreEqual(0, loaded[0].Actions.Count);
    }

    [TestMethod]
    public void SaveThenLoad_PreservesJapaneseAndSymbols()
    {
        ConfigStore.Save(
        [
            new MacroItem
            {
                Id = 1,
                Name = "全選択＆コピー（確認）",
                Alias = "select_copy",
                DelaySec = 0,
                Actions = [new ActionItem { Type = "text", Value = "日本語\n改行" }]
            }
        ]);

        var loaded = ConfigStore.Load();
        Assert.AreEqual("全選択＆コピー（確認）", loaded[0].Name);
        Assert.AreEqual("日本語\n改行", loaded[0].Actions[0].Value);
    }

    [TestMethod]
    public void Load_Utf8Bom_StillDeserializes()
    {
        var json = """[{"id":1,"name":"bom","alias":"bom","delay_sec":0,"actions":[]}]""";
        File.WriteAllText(ConfigStore.ConfigPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var loaded = ConfigStore.Load();
        Assert.AreEqual("bom", loaded[0].Alias);
    }

    [TestMethod]
    public void FindByName_FirstMatchWins()
    {
        var macros = new List<MacroItem>
        {
            new() { Id = 1, Name = "同じ", Alias = "a" },
            new() { Id = 2, Name = "同じ", Alias = "b" },
        };

        Assert.AreEqual(1, ConfigStore.FindByName(macros, "同じ")!.Id);
    }

    [TestMethod]
    public void Load_InvalidJsonArrayElement_Throws()
    {
        File.WriteAllText(ConfigStore.ConfigPath, "[1,2,3]");

        Assert.ThrowsException<JsonException>(() => ConfigStore.Load());
        Assert.AreEqual("[1,2,3]", File.ReadAllText(ConfigStore.ConfigPath));
    }

    [TestMethod]
    public void Load_CorruptJson_ThrowsWithoutClobberingFile()
    {
        const string broken = "{broken";
        File.WriteAllText(ConfigStore.ConfigPath, broken);

        Assert.ThrowsException<JsonException>(() => ConfigStore.Load());
        Assert.AreEqual(broken, File.ReadAllText(ConfigStore.ConfigPath));
    }
}

[TestClass]
public class SettingsStoreEdgeTests : IsolatedDataTestBase
{
    [TestMethod]
    public void Load_UnknownProperty_KeepsKnownDefaults()
    {
        File.WriteAllText(
            SettingsStore.SettingsPath,
            """{"confirm_before_delete":false,"action_delay_sec":0,"extra":1}""");

        var loaded = SettingsStore.Load();

        Assert.IsFalse(loaded.ConfirmBeforeDelete);
        Assert.AreEqual(0, loaded.ActionDelaySec);
    }

    [TestMethod]
    public void Load_CorruptJson_DoesNotOverwriteFile()
    {
        const string broken = "{broken";
        File.WriteAllText(SettingsStore.SettingsPath, broken);

        _ = SettingsStore.Load();

        Assert.AreEqual(broken, File.ReadAllText(SettingsStore.SettingsPath));
    }

    [TestMethod]
    public void Save_ZeroActionDelay_RoundTrips()
    {
        SettingsStore.Save(new AppSettings { ActionDelaySec = 0, ConfirmBeforeDelete = true });

        Assert.AreEqual(0, SettingsStore.Load().ActionDelaySec);
    }

    [TestMethod]
    public void Load_MissingActionDelay_UsesDefault()
    {
        File.WriteAllText(SettingsStore.SettingsPath, """{"confirm_before_delete":false}""");

        var loaded = SettingsStore.Load();

        Assert.IsFalse(loaded.ConfirmBeforeDelete);
        Assert.AreEqual(AppSettings.DefaultActionDelaySec, loaded.ActionDelaySec);
        Assert.AreEqual(string.Empty, loaded.UiLanguage);
    }
}
