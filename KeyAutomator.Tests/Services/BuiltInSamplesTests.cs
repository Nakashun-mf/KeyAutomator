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
}
