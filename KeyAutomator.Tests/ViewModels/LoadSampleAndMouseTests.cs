using KeyAutomator.Models;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class LoadSampleMacrosTests
{
    [TestMethod]
    public void LoadSampleMacros_ReplacesListAndSelectsFirst()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = new MainViewModel();
            vm.Macros.Clear();
            vm.Macros.Add(new MacroItem { Id = 99, Name = "古い", Actions = [] });
            vm.SelectedMacro = vm.Macros[0];

            var ok = vm.LoadSampleMacros();

            Assert.IsTrue(ok);
            Assert.IsTrue(vm.Macros.Count >= 2);
            Assert.AreNotEqual(99, vm.Macros[0].Id);
            Assert.AreSame(vm.Macros[0], vm.SelectedMacro);
            Assert.IsFalse(vm.IsMacroListEmpty);
        }
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }
}

[TestClass]
public class AddMouseDefaultTests
{
    [TestMethod]
    public void AddMouseAction_DefaultsToLeftClick()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = new MainViewModel();
            vm.Macros.Clear();
            var macro = new MacroItem { Id = 1, Name = "m", Actions = [] };
            vm.Macros.Add(macro);
            vm.SelectedMacro = macro;

            vm.AddMouseActionCommand.Execute(null);

            Assert.AreEqual("mouse", vm.Actions[^1].Type);
            Assert.AreEqual("LEFT", vm.Actions[^1].Value);
        }
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }
}
