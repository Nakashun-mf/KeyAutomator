using KeyAutomator.Models;

namespace KeyAutomator.Tests.Models;

[TestClass]
public class MacroItemLabelTests
{
    [TestMethod]
    public void DelayLabel_FormatsSecondsWithoutTrailingZeros()
    {
        var item = new MacroItem { DelaySec = 3 };
        StringAssert.Contains(item.DelayLabel, "3");
        StringAssert.Contains(item.DelayLabel, "秒");

        item.DelaySec = 1.5;
        StringAssert.Contains(item.DelayLabel, "1.5");
    }

    [TestMethod]
    public void ActionCountLabel_EmptyAndNonEmpty()
    {
        var item = new MacroItem();
        Assert.AreEqual("手順なし", item.ActionCountLabel);

        item.Actions = [new ActionItem { Type = "text", Value = "a" }, new ActionItem { Type = "key", Value = "ENTER" }];
        Assert.AreEqual("2 手順", item.ActionCountLabel);
    }

    [TestMethod]
    public void AliasLabel_EmptyShowsHint()
    {
        var item = new MacroItem { Alias = "" };
        StringAssert.Contains(item.AliasLabel, "CLI引数なし");

        item.Alias = "login_ok";
        StringAssert.Contains(item.AliasLabel, "login_ok");
    }

    [TestMethod]
    public void ToString_IncludesIdNameAndAlias()
    {
        var unnamed = new MacroItem { Id = 1, Name = "ログイン", Alias = "" };
        Assert.AreEqual("1: ログイン", unnamed.ToString());

        var named = new MacroItem { Id = 2, Name = "コピー", Alias = "select_copy" };
        Assert.AreEqual("2: コピー [select_copy]", named.ToString());
    }

    [TestMethod]
    public void ActionItemClone_IsIndependent()
    {
        var original = new ActionItem { Type = "text", Value = "a" };
        var copy = original.Clone();
        copy.Value = "b";

        Assert.AreEqual("a", original.Value);
        Assert.AreEqual("text", copy.Type);
    }
}
