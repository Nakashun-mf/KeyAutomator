using KeyAutomator.Models;
using KeyAutomator.Services;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class MacroCrudTests : IsolatedDataTestBase
{
    [TestMethod]
    public void NewMacro_EmptyList_AssignsIdOneAndPersists()
    {
        var vm = TestViewModels.CreateEmpty();

        vm.NewMacroCommand.Execute(null);

        Assert.AreEqual(1, vm.Macros.Count);
        Assert.AreEqual(1, vm.Macros[0].Id);
        Assert.AreEqual("新しいマクロ", vm.Macros[0].Name);
        Assert.AreSame(vm.Macros[0], vm.SelectedMacro);
        Assert.IsTrue(File.Exists(ConfigStore.ConfigPath));
        Assert.AreEqual(1, ConfigStore.Load().Count);
    }

    [TestMethod]
    public void CloneMacro_ClearsAliasAndAssignsNextId()
    {
        var source = new MacroItem
        {
            Id = 4,
            Name = "原本",
            Alias = "orig",
            Actions = [new ActionItem { Type = "text", Value = "a" }]
        };
        var vm = TestViewModels.CreateWithMacro(source);

        vm.CloneMacroCommand.Execute(null);

        Assert.AreEqual(2, vm.Macros.Count);
        var copy = vm.SelectedMacro;
        Assert.IsNotNull(copy);
        Assert.AreEqual(5, copy.Id);
        Assert.AreEqual("原本 (コピー)", copy.Name);
        Assert.AreEqual(string.Empty, copy.Alias);
        Assert.AreEqual("a", copy.Actions[0].Value);
        Assert.AreNotSame(source.Actions[0], copy.Actions[0]);
    }

    [TestMethod]
    public void DeleteSelectedMacros_RemovesAndPersists()
    {
        var keep = new MacroItem { Id = 1, Name = "残す", Actions = [] };
        var drop = new MacroItem { Id = 2, Name = "消す", Actions = [] };
        var vm = TestViewModels.CreateEmpty();
        vm.Macros.Add(keep);
        vm.Macros.Add(drop);
        vm.SelectedMacro = drop;
        vm.SyncMacroSelection([drop]);

        vm.DeleteSelectedMacros();

        Assert.AreEqual(1, vm.Macros.Count);
        Assert.AreSame(keep, vm.SelectedMacro);
        Assert.AreEqual(1, ConfigStore.Load().Single().Id);
    }

    [TestMethod]
    public void TrySaveMacro_EmptyName_Fails()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem { Id = 1, Name = "A", Actions = [] });
        vm.EditName = "   ";

        Assert.IsFalse(vm.TrySaveMacro());
        StringAssert.Contains(vm.StatusMessage, "マクロ名");
    }

    [TestMethod]
    public void TrySaveMacro_NonIntegerId_Fails()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem { Id = 1, Name = "A", Actions = [] });
        vm.EditId = 1.5;

        Assert.IsFalse(vm.TrySaveMacro());
        StringAssert.Contains(vm.StatusMessage, "整数");
    }

    [TestMethod]
    public void TrySaveMacro_IdLessThanOne_Fails()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem { Id = 1, Name = "A", Actions = [] });
        vm.EditId = 0;

        Assert.IsFalse(vm.TrySaveMacro());
        StringAssert.Contains(vm.StatusMessage, "1 以上");
    }

    [TestMethod]
    public void TrySaveMacro_DuplicateId_Fails()
    {
        var vm = TestViewModels.CreateEmpty();
        vm.Macros.Add(new MacroItem { Id = 1, Name = "A", Actions = [] });
        var target = new MacroItem { Id = 2, Name = "B", Actions = [] };
        vm.Macros.Add(target);
        vm.SelectedMacro = target;
        vm.EditId = 1;
        vm.EditName = "B";

        Assert.IsFalse(vm.TrySaveMacro());
        StringAssert.Contains(vm.StatusMessage, "既に使用");
    }

    [TestMethod]
    public void TrySaveMacro_InvalidWait_Fails()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem { Id = 1, Name = "A", Actions = [] });
        vm.AddWaitActionCommand.Execute(null);
        vm.Actions[0].Value = "あとで";

        Assert.IsFalse(vm.TrySaveMacro());
        StringAssert.Contains(vm.StatusMessage, "手順 1");
    }

    [TestMethod]
    public void Reload_CorruptConfig_DoesNotOverwriteFile()
    {
        const string broken = "{not-json";
        File.WriteAllText(ConfigStore.ConfigPath, broken);

        var vm = new MainViewModel();

        Assert.AreEqual(0, vm.Macros.Count);
        Assert.IsTrue(vm.IsMacroListEmpty);
        StringAssert.Contains(vm.StatusMessage, "読み込みに失敗");
        Assert.AreEqual(broken, File.ReadAllText(ConfigStore.ConfigPath));
    }

    [TestMethod]
    public void LoadSampleMacros_ReplacesListAndPersists()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem { Id = 99, Name = "仮", Actions = [] });

        Assert.IsTrue(vm.LoadSampleMacros());
        Assert.IsTrue(vm.Macros.Count >= 3);
        Assert.IsTrue(vm.Macros.Any(m => m.Alias == "login_ok"));
        Assert.IsTrue(ConfigStore.Load().Any(m => m.Alias == "select_copy"));
    }

    [TestMethod]
    public void MoveActionDown_SwapsWithNext()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem
        {
            Id = 1,
            Name = "A",
            Actions =
            [
                new ActionItem { Type = "text", Value = "first" },
                new ActionItem { Type = "text", Value = "second" },
            ]
        });
        vm.SelectedAction = vm.Actions[0];

        vm.MoveActionDownCommand.Execute(null);

        Assert.AreEqual("second", vm.Actions[0].Value);
        Assert.AreEqual("first", vm.Actions[1].Value);
        Assert.AreEqual(1, vm.Actions[0].Step);
        Assert.AreEqual(2, vm.Actions[1].Step);
    }

    [TestMethod]
    public void BuildCurrentMacroForRun_UsesEditorNotSavedModel()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem
        {
            Id = 1,
            Name = "保存済",
            DelaySec = 3,
            Actions = [new ActionItem { Type = "text", Value = "old" }]
        });
        vm.EditName = "実行用";
        vm.EditDelaySec = 0;
        vm.Actions[0].Value = "new";

        var built = vm.BuildCurrentMacroForRun();

        Assert.IsNotNull(built);
        Assert.AreEqual("実行用", built.Name);
        Assert.AreEqual(0, built.DelaySec);
        Assert.AreEqual("new", built.Actions[0].Value);
        Assert.AreEqual("保存済", vm.SelectedMacro!.Name);
    }

    [TestMethod]
    public void TryPrepareMacroSwitch_WhenDirty_ReturnsFalse()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem { Id = 1, Name = "A", Actions = [] });
        vm.EditName = "変更";

        Assert.IsTrue(vm.IsDirty);
        Assert.IsFalse(vm.TryPrepareMacroSwitch());

        vm.CancelEditCommand.Execute(null);
        Assert.IsTrue(vm.TryPrepareMacroSwitch());
    }

    [TestMethod]
    public void IsBusy_UpdatesTestButtonLabelAndIdle()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem { Id = 1, Name = "A", Actions = [] });
        Assert.AreEqual("テスト実行", vm.TestButtonLabel);
        Assert.IsTrue(vm.IsIdle);
        Assert.IsTrue(vm.SaveMacroCommand.CanExecute(null));

        vm.IsBusy = true;

        Assert.AreEqual("中断", vm.TestButtonLabel);
        Assert.IsFalse(vm.IsIdle);
        Assert.IsFalse(vm.SaveMacroCommand.CanExecute(null));
        Assert.IsFalse(vm.CloneMacroCommand.CanExecute(null));
    }

    [TestMethod]
    public void ConfirmBeforeDelete_PersistsToSettingsStore()
    {
        var vm = TestViewModels.CreateEmpty();
        vm.ConfirmBeforeDelete = false;

        var loaded = SettingsStore.Load();
        Assert.IsFalse(loaded.ConfirmBeforeDelete);
    }

    [TestMethod]
    public void RemoveSelectedActions_KeepsRemainingAndSelectsLast()
    {
        var vm = TestViewModels.CreateWithMacro(new MacroItem
        {
            Id = 1,
            Name = "A",
            Actions =
            [
                new ActionItem { Type = "text", Value = "a" },
                new ActionItem { Type = "text", Value = "b" },
                new ActionItem { Type = "text", Value = "c" },
            ]
        });
        vm.SyncActionSelection([vm.Actions[0], vm.Actions[1]]);

        vm.RemoveSelectedActions();

        Assert.AreEqual(1, vm.Actions.Count);
        Assert.AreEqual("c", vm.Actions[0].Value);
        Assert.AreSame(vm.Actions[0], vm.SelectedAction);
    }

    [TestMethod]
    public void PersistMacroOrder_WritesCurrentListOrder()
    {
        var vm = TestViewModels.CreateEmpty();
        vm.Macros.Add(new MacroItem { Id = 2, Name = "後", Actions = [] });
        vm.Macros.Add(new MacroItem { Id = 1, Name = "先", Actions = [] });

        vm.PersistMacroOrder();

        var loaded = ConfigStore.Load();
        Assert.AreEqual(2, loaded[0].Id);
        Assert.AreEqual(1, loaded[1].Id);
        StringAssert.Contains(vm.StatusMessage, "順序");
    }
}
