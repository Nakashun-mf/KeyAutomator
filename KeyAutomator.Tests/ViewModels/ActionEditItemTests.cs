using KeyAutomator.Models;
using KeyAutomator.Services;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class ActionEditItemTests
{
    [TestMethod]
    public void FromModel_KeyAlias_NormalizesToCatalogCode()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "key", Value = "return" });

        Assert.AreEqual("key", item.Type);
        Assert.AreEqual("ENTER", item.Value);
        Assert.IsTrue(item.IsKeyType);
        Assert.IsFalse(item.IsFreeTextType);
    }

    [TestMethod]
    public void FromModel_Hotkey_SplitsParts()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "hotkey", Value = "ctrl + s" });

        Assert.IsTrue(item.IsHotkeyType);
        Assert.AreEqual(2, item.HotkeyParts.Count);
        Assert.AreEqual("CTRL", item.HotkeyParts[0].Code);
        Assert.AreEqual("S", item.HotkeyParts[1].Code);
        Assert.AreEqual("CTRL+S", item.ToModel().Value);
    }

    [TestMethod]
    public void FromModel_MouseUnknown_DefaultsToLeft()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "mouse", Value = "WHEEL" });

        Assert.AreEqual("LEFT", item.Value);
        Assert.IsTrue(item.IsMouseType);
    }

    [TestMethod]
    public void FromModel_RepeatInvalidCount_UsesDefault()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "repeat", Value = "abc" });

        Assert.AreEqual(RepeatBlock.StartType, item.Type);
        Assert.AreEqual(RepeatBlock.DefaultCount.ToString(), item.Value);
        Assert.IsTrue(item.IsRepeatType);
    }

    [TestMethod]
    public void FromModel_EndRepeat_ClearsValue()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "end_repeat", Value = "ignore" });

        Assert.AreEqual(RepeatBlock.EndType, item.Type);
        Assert.AreEqual(string.Empty, item.Value);
        Assert.IsTrue(item.IsEndRepeatType);
    }

    [TestMethod]
    public void TryValidate_WaitNonNumeric_ReturnsFalse()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "wait", Value = "soon" });

        Assert.IsFalse(item.TryValidate(out var error));
        StringAssert.Contains(error, "待機秒数");
    }

    [TestMethod]
    public void TryValidate_WaitNumeric_ReturnsTrue()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "wait", Value = "0.25" });

        Assert.IsTrue(item.TryValidate(out var error));
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void RepeatCount_ClampsToAllowedRange()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "repeat", Value = "2" });

        item.RepeatCount = 0;
        Assert.AreEqual(1, item.RepeatCount);

        item.RepeatCount = RepeatBlock.MaxCount + 10;
        Assert.AreEqual(RepeatBlock.MaxCount, item.RepeatCount);
    }

    [TestMethod]
    public void RemoveHotkeyPart_LastPart_DoesNotRemove()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "hotkey", Value = "CTRL+S" });
        Assert.AreEqual(2, item.HotkeyParts.Count);

        item.RemoveHotkeyPart(item.HotkeyParts[0]);
        Assert.AreEqual(1, item.HotkeyParts.Count);

        item.RemoveHotkeyPart(item.HotkeyParts[0]);
        Assert.AreEqual(1, item.HotkeyParts.Count);
    }

    [TestMethod]
    public void IsValidHotkey_Empty_ReturnsFalse()
    {
        Assert.IsFalse(ActionEditItem.IsValidHotkey("", out var error));
        StringAssert.Contains(error, "空");
    }

    [TestMethod]
    public void IsValidHotkey_TwoMainKeys_ReturnsFalse()
    {
        Assert.IsFalse(ActionEditItem.IsValidHotkey("CTRL+A+B", out var error));
        StringAssert.Contains(error, "メインキー");
    }

    [TestMethod]
    public void IsValidHotkey_WinPlusR_ReturnsTrue()
    {
        Assert.IsTrue(ActionEditItem.IsValidHotkey("WIN+R", out var error));
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void ChangingTypeToKey_UnknownValue_DefaultsToEnter()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "text", Value = "hello" });
        item.Type = "key";

        Assert.AreEqual("ENTER", item.Value);
        Assert.IsTrue(item.IsKeyType);
    }

    [TestMethod]
    public void ChangingTypeToDialog_NumericValue_ReplacedWithDefaultMessage()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "wait", Value = "0.5" });
        item.Type = "dialog";

        Assert.AreEqual(UserDialog.DefaultMessage, item.Value);
    }
}
