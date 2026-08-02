using KeyAutomator.Models;
using KeyAutomator.Services;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class WrapInRepeatTests
{
    [TestMethod]
    public void WrapInRepeat_WithSelection_WrapsRangeWithStartAndEnd()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = CreateVmWithActions("a", "b", "c");
            vm.SelectedActions.Clear();
            vm.SelectedActions.Add(vm.Actions[0]);
            vm.SelectedActions.Add(vm.Actions[1]);
            vm.SelectedAction = vm.Actions[0];

            vm.WrapInRepeatCommand.Execute(null);

            Assert.AreEqual(5, vm.Actions.Count);
            Assert.AreEqual(RepeatBlock.StartType, vm.Actions[0].Type);
            Assert.AreEqual("2", vm.Actions[0].Value);
            Assert.AreEqual("a", vm.Actions[1].Value);
            Assert.AreEqual("b", vm.Actions[2].Value);
            Assert.AreEqual(RepeatBlock.EndType, vm.Actions[3].Type);
            Assert.AreEqual("c", vm.Actions[4].Value);
            Assert.AreEqual(0, vm.Actions[0].Depth);
            Assert.AreEqual(1, vm.Actions[1].Depth);
            Assert.AreEqual(0, vm.Actions[3].Depth);
        }
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }

    [TestMethod]
    public void WrapInRepeat_WithNoSelection_InsertsEmptyBlock()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = CreateVmWithActions("a");
            vm.SelectedAction = null;
            vm.SelectedActions.Clear();

            vm.WrapInRepeatCommand.Execute(null);

            Assert.AreEqual(3, vm.Actions.Count);
            Assert.AreEqual(RepeatBlock.StartType, vm.Actions[1].Type);
            Assert.AreEqual(RepeatBlock.EndType, vm.Actions[2].Type);
            Assert.IsTrue(vm.StatusMessage.Contains("ここまで"));
        }
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }

    [TestMethod]
    public void TrySaveMacro_UnclosedRepeat_FailsValidation()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = CreateVmWithActions("a");
            vm.Actions.Insert(0, ActionEditItem.FromModel(new ActionItem
            {
                Type = RepeatBlock.StartType,
                Value = "2"
            }));

            var saved = vm.TrySaveMacro();

            Assert.IsFalse(saved);
            Assert.IsTrue(vm.StatusMessage.Contains("ここまで"));
        }
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }

    private static MainViewModel CreateVmWithActions(params string[] values)
    {
        var vm = new MainViewModel();
        vm.Macros.Clear();
        var macro = new MacroItem
        {
            Id = 1,
            Name = "test",
            Alias = "wrap_test",
            DelaySec = 1,
            Actions = values.Select(v => new ActionItem { Type = "text", Value = v }).ToList()
        };
        vm.Macros.Add(macro);
        vm.SelectedMacro = macro;
        return vm;
    }
}
