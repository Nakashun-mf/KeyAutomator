using KeyAutomator.Models;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class SaveMacroPersistenceTests
{
    [TestMethod]
    public void TrySaveMacro_DoesNotClearEditorActions_AndKeepsSameSelection()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
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
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }

    [TestMethod]
    public void CancelEdit_ReloadsWithoutClearingSelection()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
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
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }

    private static MainViewModel CreateIsolatedViewModel()
    {
        var vm = new MainViewModel();
        vm.Macros.Clear();
        vm.SelectedMacro = null;
        return vm;
    }
}

/// <summary>テスト中に config.json を退避・復元する。</summary>
internal static class ConfigPathSnapshot
{
    public static string? Begin()
    {
        var path = KeyAutomator.Services.ConfigStore.ConfigPath;
        if (!File.Exists(path))
            return null;

        var backup = path + ".testbak";
        File.Copy(path, backup, overwrite: true);
        return backup;
    }

    public static void End(string? backup)
    {
        var path = KeyAutomator.Services.ConfigStore.ConfigPath;
        try
        {
            if (backup is not null && File.Exists(backup))
            {
                File.Copy(backup, path, overwrite: true);
                File.Delete(backup);
            }
        }
        catch
        {
            // ignore
        }
    }
}
