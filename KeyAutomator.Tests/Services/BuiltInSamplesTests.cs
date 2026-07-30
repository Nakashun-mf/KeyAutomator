using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class BuiltInSamplesTests
{
    [TestMethod]
    public void Create_ReturnsTwoSampleMacros()
    {
        var list = BuiltInSamples.Create();
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("login_ok", list[0].Alias);
        Assert.AreEqual("select_copy", list[1].Alias);
        Assert.IsTrue(list[0].Actions.Count > 0);
    }

    [TestMethod]
    public void LoadSampleMacros_ReturnsNonEmpty()
    {
        var list = ConfigStore.LoadSampleMacros();
        Assert.IsTrue(list.Count >= 2);
    }
}
