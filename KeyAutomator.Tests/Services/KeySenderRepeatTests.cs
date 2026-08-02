using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class KeySenderRepeatTests
{
    [TestCleanup]
    public void Cleanup() => UserDialog.ShowOkHandler = null;

    [TestMethod]
    public void ExecuteActionsWithRepeats_SimpleLoop_RunsBodyCountTimes()
    {
        var log = new List<string>();
        UserDialog.ShowOkHandler = msg => log.Add(msg ?? string.Empty);

        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "3" },
            new() { Type = "dialog", Value = "x" },
            new() { Type = "end_repeat", Value = "" },
        };

        KeySender.ExecuteActionsWithRepeats(actions, stepDelayMs: 0, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "x", "x", "x" }, log);
    }

    [TestMethod]
    public void ExecuteActionsWithRepeats_NestedLoops_MultipliesInnerBody()
    {
        var log = new List<string>();
        UserDialog.ShowOkHandler = msg => log.Add(msg ?? string.Empty);

        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "2" },
            new() { Type = "dialog", Value = "o" },
            new() { Type = "repeat", Value = "3" },
            new() { Type = "dialog", Value = "i" },
            new() { Type = "end_repeat", Value = "" },
            new() { Type = "end_repeat", Value = "" },
        };

        KeySender.ExecuteActionsWithRepeats(actions, stepDelayMs: 0, CancellationToken.None);

        // outer 2 × (o + inner 3×i) => o,i,i,i, o,i,i,i
        CollectionAssert.AreEqual(
            new[] { "o", "i", "i", "i", "o", "i", "i", "i" },
            log);
    }

    [TestMethod]
    public void ExecuteActionsWithRepeats_EmptyBody_SkipsWithoutError()
    {
        var log = new List<string>();
        UserDialog.ShowOkHandler = msg => log.Add(msg ?? string.Empty);

        var actions = new List<ActionItem>
        {
            new() { Type = "dialog", Value = "before" },
            new() { Type = "repeat", Value = "5" },
            new() { Type = "end_repeat", Value = "" },
            new() { Type = "dialog", Value = "after" },
        };

        KeySender.ExecuteActionsWithRepeats(actions, stepDelayMs: 0, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "before", "after" }, log);
    }

    [TestMethod]
    public void ExecuteActionsWithRepeats_Cancel_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "2" },
            new() { Type = "dialog", Value = "x" },
            new() { Type = "end_repeat", Value = "" },
        };

        Assert.ThrowsException<OperationCanceledException>(() =>
            KeySender.ExecuteActionsWithRepeats(actions, stepDelayMs: 0, cts.Token));
    }

    [TestMethod]
    public void ExecuteMacro_WithRepeats_RespectsMarkers()
    {
        var log = new List<string>();
        UserDialog.ShowOkHandler = msg => log.Add(msg ?? string.Empty);

        var macro = new MacroItem
        {
            DelaySec = 0,
            Actions =
            [
                new ActionItem { Type = "repeat", Value = "2" },
                new ActionItem { Type = "dialog", Value = "a" },
                new ActionItem { Type = "end_repeat", Value = "" },
                new ActionItem { Type = "dialog", Value = "b" },
            ]
        };

        KeySender.ExecuteMacro(macro, actionDelaySec: 0);

        CollectionAssert.AreEqual(new[] { "a", "a", "b" }, log);
    }
}
