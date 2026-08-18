using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class BuiltInSamplesTests
{
    [TestMethod]
    public void Create_ReturnsSampleMacrosIncludingRepeat()
    {
        var list = BuiltInSamples.Create();
        Assert.AreEqual(3, list.Count);
        Assert.AreEqual("login_ok", list[0].Alias);
        Assert.AreEqual("select_copy", list[1].Alias);
        Assert.AreEqual("enter_x3", list[2].Alias);
        Assert.IsTrue(list[0].Actions.Count > 0);
        Assert.IsTrue(list[2].Actions.Any(a => a.Type == "repeat"));
        Assert.IsTrue(list[2].Actions.Any(a => a.Type == "end_repeat"));
    }

    [TestMethod]
    public void LoadSampleMacros_ReturnsNonEmpty()
    {
        var list = ConfigStore.LoadSampleMacros();
        Assert.IsTrue(list.Count >= 3);
    }

    [TestMethod]
    public void ApplyCurrentLanguage_English_UsesEnglishSampleNames()
    {
        try
        {
            Loc.Initialize(Loc.English);
            var list = BuiltInSamples.Create();
            Assert.AreEqual("Login and type fixed data", list[0].Name);
            Assert.AreEqual("Select all and copy", list[1].Name);
            Assert.AreEqual("Enter 3 times", list[2].Name);
            Assert.AreEqual("Press OK after you confirm login succeeded", list[0].Actions.First(a => a.Type == "dialog").Value);
        }
        finally
        {
            Loc.Initialize(Loc.Japanese);
        }
    }

    [TestMethod]
    public void ApplyCurrentLanguage_UnknownAlias_LeavesNameUnchanged()
    {
        var list = new List<MacroItem>
        {
            new() { Alias = "custom", Name = "Keep me" }
        };

        BuiltInSamples.ApplyCurrentLanguage(list);

        Assert.AreEqual("Keep me", list[0].Name);
    }
}

[TestClass]
public class LoadSampleMacrosFromFileTests : IsolatedDataTestBase
{
    [TestMethod]
    public void LoadSampleMacros_JapaneseFile_UsesEnglishNamesWhenUiIsEnglish()
    {
        File.WriteAllText(
            Path.Combine(DataDirectory, "config.sample.json"),
            RepoFiles.Read("config.sample.json"));

        try
        {
            Loc.Initialize(Loc.English);
            var list = ConfigStore.LoadSampleMacros();
            Assert.AreEqual("Login and type fixed data", list[0].Name);
            Assert.AreEqual("Press OK after you confirm login succeeded",
                list[0].Actions.First(a => a.Type == "dialog").Value);
        }
        finally
        {
            Loc.Initialize(Loc.Japanese);
        }
    }
}
