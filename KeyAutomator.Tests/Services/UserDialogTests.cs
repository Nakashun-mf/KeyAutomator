using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class UserDialogTests
{
    [TestCleanup]
    public void Cleanup() => UserDialog.ShowOkHandler = null;

    [TestMethod]
    public void ShowOk_WithCustomMessage_PassesTrimmedMessageToHandler()
    {
        string? received = null;
        UserDialog.ShowOkHandler = msg => received = msg;

        UserDialog.ShowOk("  次へ進んでください  ");

        Assert.AreEqual("次へ進んでください", received);
    }

    [TestMethod]
    public void ShowOk_WithEmptyMessage_UsesDefaultMessage()
    {
        string? received = null;
        UserDialog.ShowOkHandler = msg => received = msg;

        UserDialog.ShowOk("   ");

        Assert.AreEqual(UserDialog.DefaultMessage, received);
    }

    [TestMethod]
    public void ShowOk_WithNullMessage_UsesDefaultMessage()
    {
        string? received = null;
        UserDialog.ShowOkHandler = msg => received = msg;

        UserDialog.ShowOk(null);

        Assert.AreEqual(UserDialog.DefaultMessage, received);
    }
}

[TestClass]
public class KeySenderDialogActionTests
{
    [TestCleanup]
    public void Cleanup() => UserDialog.ShowOkHandler = null;

    [TestMethod]
    public void ExecuteAction_DialogType_ShowsConfiguredMessage()
    {
        string? received = null;
        UserDialog.ShowOkHandler = msg => received = msg;

        KeySender.ExecuteAction(new ActionItem
        {
            Type = "dialog",
            Value = "フォーカスを移して OK"
        });

        Assert.AreEqual("フォーカスを移して OK", received);
    }

    [TestMethod]
    public void ExecuteAction_DialogTypeCaseInsensitive_ShowsDialog()
    {
        var called = false;
        UserDialog.ShowOkHandler = _ => called = true;

        KeySender.ExecuteAction(new ActionItem { Type = "DIALOG", Value = "確認" });

        Assert.IsTrue(called);
    }

    [TestMethod]
    public void ExecuteAction_DialogWithEmptyValue_UsesDefaultMessage()
    {
        string? received = null;
        UserDialog.ShowOkHandler = msg => received = msg;

        KeySender.ExecuteAction(new ActionItem { Type = "dialog", Value = "" });

        Assert.AreEqual(UserDialog.DefaultMessage, received);
    }
}
