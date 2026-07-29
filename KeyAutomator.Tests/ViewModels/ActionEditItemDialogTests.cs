using KeyAutomator.Models;
using KeyAutomator.Services;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class ActionEditItemDialogTests
{
    [TestMethod]
    public void ActionTypeCatalog_ContainsDialogType()
    {
        var option = ActionTypeCatalog.Get("dialog");

        Assert.AreEqual("dialog", option.Code);
        Assert.AreEqual("確認ダイアログ", option.Label);
    }

    [TestMethod]
    public void FromModel_DialogWithEmptyValue_UsesDefaultMessage()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "dialog", Value = "" });

        Assert.AreEqual("dialog", item.Type);
        Assert.AreEqual(UserDialog.DefaultMessage, item.Value);
        Assert.IsTrue(item.IsFreeTextType);
    }

    [TestMethod]
    public void FromModel_DialogWithMessage_KeepsMessage()
    {
        var item = ActionEditItem.FromModel(new ActionItem
        {
            Type = "dialog",
            Value = "準備ができたら OK"
        });

        Assert.AreEqual("準備ができたら OK", item.Value);
    }

    [TestMethod]
    public void ToModel_Dialog_RoundTripsTypeAndValue()
    {
        var item = ActionEditItem.FromModel(new ActionItem
        {
            Type = "dialog",
            Value = "次へ"
        });

        var model = item.ToModel();

        Assert.AreEqual("dialog", model.Type);
        Assert.AreEqual("次へ", model.Value);
    }

    [TestMethod]
    public void OnTypeChanged_ToDialogFromWait_ReplacesNumericValueWithDefault()
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = "wait", Value = "0.5" });

        item.Type = "dialog";

        Assert.AreEqual(UserDialog.DefaultMessage, item.Value);
    }
}

[TestClass]
public class MainViewModelDialogTests
{
    [TestMethod]
    public void AddDialogAction_WhenMacroSelected_AddsDialogStep()
    {
        var vm = new MainViewModel();
        vm.NewMacroCommand.Execute(null);

        vm.AddDialogActionCommand.Execute(null);

        Assert.AreEqual(1, vm.Actions.Count);
        Assert.AreEqual("dialog", vm.Actions[0].Type);
        Assert.AreEqual(UserDialog.DefaultMessage, vm.Actions[0].Value);
    }
}
