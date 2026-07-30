using KeyAutomator.Models;
using KeyAutomator.ViewModels;

namespace KeyAutomator.Tests.ViewModels;

[TestClass]
public class AddActionInsertTests
{
    [TestMethod]
    public void AddTextAction_WithSelectedAction_InsertsBelowSelection()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = CreateVmWithActions("a", "b", "c");
            vm.SelectedAction = vm.Actions[0];
            vm.SelectedActions.Clear();
            vm.SelectedActions.Add(vm.Actions[0]);

            vm.AddTextActionCommand.Execute(null);

            Assert.AreEqual(4, vm.Actions.Count);
            Assert.AreEqual("a", vm.Actions[0].Value);
            Assert.AreEqual(string.Empty, vm.Actions[1].Value);
            Assert.AreEqual("text", vm.Actions[1].Type);
            Assert.AreEqual("b", vm.Actions[2].Value);
            Assert.AreEqual("c", vm.Actions[3].Value);
            Assert.AreSame(vm.Actions[1], vm.SelectedAction);
        }
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }

    [TestMethod]
    public void AddTextAction_WithNoSelection_AppendsAtEnd()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = CreateVmWithActions("a", "b");
            vm.SelectedAction = null;
            vm.SelectedActions.Clear();

            vm.AddWaitActionCommand.Execute(null);

            Assert.AreEqual(3, vm.Actions.Count);
            Assert.AreEqual("wait", vm.Actions[2].Type);
            Assert.AreEqual("0.5", vm.Actions[2].Value);
            Assert.AreSame(vm.Actions[2], vm.SelectedAction);
        }
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }

    [TestMethod]
    public void AddTextAction_WithMultiSelection_InsertsBelowLowestSelected()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = CreateVmWithActions("a", "b", "c", "d");
            vm.SelectedActions.Clear();
            vm.SelectedActions.Add(vm.Actions[0]);
            vm.SelectedActions.Add(vm.Actions[2]);
            vm.SelectedAction = vm.Actions[0];

            vm.AddKeyActionCommand.Execute(null);

            Assert.AreEqual(5, vm.Actions.Count);
            Assert.AreEqual("key", vm.Actions[3].Type);
            Assert.AreEqual("ENTER", vm.Actions[3].Value);
            Assert.AreEqual("d", vm.Actions[4].Value);
        }
        finally
        {
            ConfigPathSnapshot.End(configPath);
        }
    }

    [TestMethod]
    public void ActionSummary_DoesNotIncludeActionDelayParentheses()
    {
        var configPath = ConfigPathSnapshot.Begin();
        try
        {
            var vm = CreateVmWithActions("x");
            Assert.AreEqual("上から順に 1 手順を実行します", vm.ActionSummary);
            Assert.IsFalse(vm.ActionSummary.Contains('（'));
            Assert.IsFalse(vm.ActionSummary.Contains("手順間隔"));
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
            Actions = values.Select(v => new ActionItem { Type = "text", Value = v }).ToList()
        };
        vm.Macros.Add(macro);
        vm.SelectedMacro = macro;
        return vm;
    }
}
