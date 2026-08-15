using KeyAutomator.Models;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class SaveMacroPersistenceTests : IsolatedDataTestBase
{
    [TestMethod]
    public void TrySaveMacro_DoesNotClearEditorActions_AndKeepsSameSelection()
    {
        var vm = CreateIsolatedViewModel();
        var macro = new MacroItem
        {
            Id = 1,
            Name = "元",
            DelaySec = 1,
            Actions = [new ActionItem { Type = "text", Value = "old" }]
        };
        vm.Macros.Add(macro);
        vm.SelectedMacro = macro;

        vm.EditName = "更新後";
        vm.AddTextActionCommand.Execute(null);
        vm.Actions[^1].Value = "new-step";
        Assert.IsTrue(vm.IsDirty);

        var ok = vm.TrySaveMacro();

        Assert.IsTrue(ok);
        Assert.IsFalse(vm.IsDirty);
        Assert.AreSame(macro, vm.SelectedMacro);
        Assert.AreEqual("更新後", macro.Name);
        Assert.AreEqual(2, macro.Actions.Count);
        Assert.AreEqual("new-step", macro.Actions[^1].Value);
        Assert.AreEqual(2, vm.Actions.Count);
        Assert.AreEqual("new-step", vm.Actions[^1].Value);
    }

    [TestMethod]
    public void CancelEdit_ReloadsWithoutClearingSelection()
    {
        var vm = CreateIsolatedViewModel();
        var macro = new MacroItem
        {
            Id = 2,
            Name = "A",
            Actions = [new ActionItem { Type = "key", Value = "ENTER" }]
        };
        vm.Macros.Add(macro);
        vm.SelectedMacro = macro;
        vm.EditName = "変更中";
        Assert.IsTrue(vm.IsDirty);

        vm.CancelEditCommand.Execute(null);

        Assert.AreSame(macro, vm.SelectedMacro);
        Assert.AreEqual("A", vm.EditName);
        Assert.IsFalse(vm.IsDirty);
        Assert.AreEqual(1, vm.Actions.Count);
    }

    [TestMethod]
    public void TrySaveMacro_InvalidAlias_FailsWithoutWriting()
    {
        var vm = CreateIsolatedViewModel();
        var macro = new MacroItem { Id = 1, Name = "A", Alias = "", Actions = [] };
        vm.Macros.Add(macro);
        vm.SelectedMacro = macro;
        vm.EditAlias = "だめな名前";

        var saved = vm.TrySaveMacro();

        Assert.IsFalse(saved);
        Assert.IsTrue(vm.StatusMessage.Contains("英数字"));
        Assert.AreEqual(string.Empty, macro.Alias);
    }

    [TestMethod]
    public void TrySaveMacro_DuplicateAlias_Fails()
    {
        var vm = CreateIsolatedViewModel();
        vm.Macros.Add(new MacroItem { Id = 1, Name = "A", Alias = "taken", Actions = [] });
        var target = new MacroItem { Id = 2, Name = "B", Alias = "", Actions = [] };
        vm.Macros.Add(target);
        vm.SelectedMacro = target;
        vm.EditAlias = "taken";

        var saved = vm.TrySaveMacro();

        Assert.IsFalse(saved);
        StringAssert.Contains(vm.StatusMessage, "既に使用");
    }

    private static MainViewModel CreateIsolatedViewModel()
    {
        var vm = new MainViewModel();
        vm.Macros.Clear();
        vm.SelectedMacro = null;
        return vm;
    }
}
