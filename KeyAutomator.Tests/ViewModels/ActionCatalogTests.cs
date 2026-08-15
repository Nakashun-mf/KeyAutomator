using KeyAutomator.Services;
using KeyAutomator.ViewModels;
using Windows.System;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class ActionCatalogTests
{
    [TestMethod]
    public void ActionTypeCatalog_ContainsAllSupportedActionTypes()
    {
        var codes = ActionTypeCatalog.All.Select(x => x.Code).ToList();

        CollectionAssert.AreEquivalent(
            new[] { "text", "key", "hotkey", "mouse", "wait", "dialog", RepeatBlock.StartType, RepeatBlock.EndType },
            codes);
        Assert.AreEqual(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [TestMethod]
    public void ActionTypeCatalog_Get_Unknown_FallsBackToText()
    {
        Assert.AreEqual("text", ActionTypeCatalog.Get("nope").Code);
        Assert.AreEqual("text", ActionTypeCatalog.Get(null).Code);
    }

    [TestMethod]
    public void MouseActionCatalog_ContainsClickVariants()
    {
        Assert.IsTrue(MouseActionCatalog.Contains("LEFT"));
        Assert.IsTrue(MouseActionCatalog.Contains("right"));
        Assert.IsTrue(MouseActionCatalog.Contains("MIDDLE"));
        Assert.IsTrue(MouseActionCatalog.Contains("LEFT_DOUBLE"));
        Assert.IsFalse(MouseActionCatalog.Contains("WHEEL"));
        Assert.AreEqual("LEFT", MouseActionCatalog.Get("unknown").Code);
    }

    [TestMethod]
    [DataRow("RETURN", "ENTER")]
    [DataRow("ESCAPE", "ESC")]
    [DataRow("BS", "BACKSPACE")]
    [DataRow("DEL", "DELETE")]
    [DataRow("WIN", "LWIN")]
    [DataRow("CONTROL", "CTRL")]
    [DataRow("PGDN", "PAGEDOWN")]
    public void SpecialKeyCatalog_Normalize_MapsAliases(string input, string expected)
    {
        Assert.AreEqual(expected, SpecialKeyCatalog.Normalize(input));
        Assert.IsTrue(SpecialKeyCatalog.Contains(input));
    }

    [TestMethod]
    public void SpecialKeyCatalog_AllCodes_ResolveToVirtualKey()
    {
        foreach (var option in SpecialKeyCatalog.All)
        {
            var key = KeySender.ResolveKey(option.Code);
            Assert.AreNotEqual(VirtualKey.None, key, option.Code);
        }
    }

    [TestMethod]
    public void SpecialKeyCatalog_Get_Empty_ReturnsFirstOption()
    {
        Assert.AreSame(SpecialKeyCatalog.All[0], SpecialKeyCatalog.Get(""));
        Assert.AreEqual("ENTER", SpecialKeyCatalog.Get("NOT_A_KEY").Code);
    }
}
