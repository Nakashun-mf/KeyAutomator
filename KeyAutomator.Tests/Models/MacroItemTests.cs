using KeyAutomator.Models;

namespace KeyAutomator.Tests.Models;

[TestClass]
public class MacroItemTests
{
    [TestMethod]
    public void IsValidAlias_Empty_ReturnsTrue()
    {
        Assert.IsTrue(MacroItem.IsValidAlias(null, out var error));
        Assert.AreEqual(string.Empty, error);
        Assert.IsTrue(MacroItem.IsValidAlias("", out _));
        Assert.IsTrue(MacroItem.IsValidAlias("   ", out _));
    }

    [TestMethod]
    [DataRow("login_ok")]
    [DataRow("A")]
    [DataRow("select_copy")]
    [DataRow("n9")]
    public void IsValidAlias_AlphanumericAndUnderscore_ReturnsTrue(string alias)
    {
        Assert.IsTrue(MacroItem.IsValidAlias(alias, out var error));
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    [DataRow("ログイン")]
    [DataRow("has space")]
    [DataRow("dash-name")]
    [DataRow("a.b")]
    public void IsValidAlias_InvalidCharacters_ReturnsFalse(string alias)
    {
        Assert.IsFalse(MacroItem.IsValidAlias(alias, out var error));
        StringAssert.Contains(error, "英数字");
    }

    [TestMethod]
    public void Clone_CopiesActionsIndependently()
    {
        var original = new MacroItem
        {
            Id = 3,
            Name = "元",
            Alias = "src",
            DelaySec = 2,
            Actions = [new ActionItem { Type = "text", Value = "a" }]
        };

        var copy = original.Clone();
        copy.Name = "写";
        copy.Actions[0].Value = "changed";

        Assert.AreEqual("元", original.Name);
        Assert.AreEqual("a", original.Actions[0].Value);
        Assert.AreEqual("写", copy.Name);
        Assert.AreEqual(3, copy.Id);
    }
}
