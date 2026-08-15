using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class SettingsStoreTests : IsolatedDataTestBase
{
    [TestMethod]
    public void Load_WhenMissing_WritesDefaults()
    {
        Assert.IsFalse(File.Exists(SettingsStore.SettingsPath));

        var settings = SettingsStore.Load();

        Assert.IsTrue(settings.ConfirmBeforeDelete);
        Assert.AreEqual(AppSettings.DefaultActionDelaySec, settings.ActionDelaySec);
        Assert.IsTrue(File.Exists(SettingsStore.SettingsPath));
    }

    [TestMethod]
    public void SaveThenLoad_RoundTripsValues()
    {
        SettingsStore.Save(new AppSettings
        {
            ConfirmBeforeDelete = false,
            ActionDelaySec = 0.5
        });

        var loaded = SettingsStore.Load();

        Assert.IsFalse(loaded.ConfirmBeforeDelete);
        Assert.AreEqual(0.5, loaded.ActionDelaySec);
    }

    [TestMethod]
    public void Load_CorruptJson_ReturnsDefaultsWithoutThrowing()
    {
        File.WriteAllText(SettingsStore.SettingsPath, "{broken");

        var loaded = SettingsStore.Load();

        Assert.IsTrue(loaded.ConfirmBeforeDelete);
        Assert.AreEqual(AppSettings.DefaultActionDelaySec, loaded.ActionDelaySec);
        Assert.IsFalse(string.IsNullOrWhiteSpace(ErrorLogger.LastWrittenPath));
    }
}
