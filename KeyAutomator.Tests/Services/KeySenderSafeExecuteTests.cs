using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

/// <summary>
/// SendInput を呼ばない経路だけを検証する（dialog / wait 0 / 空 / 未知種別 / 繰り返し制御）。
/// </summary>
[TestClass]
public class KeySenderSafeExecuteTests : IsolatedDataTestBase
{
    [TestMethod]
    public void ExecuteMacro_Null_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => KeySender.ExecuteMacro(null!));
    }

    [TestMethod]
    public void ExecuteMacro_EmptyActionsZeroDelay_DoesNotThrow()
    {
        KeySender.ExecuteMacro(new MacroItem { DelaySec = 0, Actions = [] }, actionDelaySec: 0);
    }

    [TestMethod]
    public void ExecuteAction_Null_DoesNotThrow()
    {
        KeySender.ExecuteAction(null!);
    }

    [TestMethod]
    public void ExecuteAction_RepeatMarkers_AreNoOp()
    {
        KeySender.ExecuteAction(new ActionItem { Type = "repeat", Value = "3" });
        KeySender.ExecuteAction(new ActionItem { Type = "end_repeat", Value = "" });
    }

    [TestMethod]
    public void ExecuteAction_UnknownType_WritesErrorLog()
    {
        KeySender.ExecuteAction(new ActionItem { Type = "speech", Value = "hi" });

        Assert.IsFalse(string.IsNullOrWhiteSpace(ErrorLogger.LastWrittenPath));
        StringAssert.Contains(File.ReadAllText(ErrorLogger.LastWrittenPath!), "未知のアクション種別");
    }

    [TestMethod]
    public void ExecuteAction_InvalidWait_WritesErrorLog()
    {
        KeySender.ExecuteAction(new ActionItem { Type = "wait", Value = "あとで" });

        StringAssert.Contains(File.ReadAllText(ErrorLogger.LastWrittenPath!), "wait");
    }

    [TestMethod]
    public void ExecuteAction_WaitZero_ReturnsImmediately()
    {
        KeySender.ExecuteAction(new ActionItem { Type = "wait", Value = "0" });
    }

    [TestMethod]
    public void ExecuteActionsWithRepeats_Empty_Returns()
    {
        KeySender.ExecuteActionsWithRepeats([], stepDelayMs: 0, CancellationToken.None);
    }

    [TestMethod]
    public void ExecuteActionsWithRepeats_MissingEnd_StopsWithoutRunningTail()
    {
        var log = new List<string>();
        UserDialog.ShowOkHandler = msg => log.Add(msg ?? string.Empty);

        KeySender.ExecuteActionsWithRepeats(
            [
                new ActionItem { Type = "repeat", Value = "2" },
                new ActionItem { Type = "dialog", Value = "body" },
                new ActionItem { Type = "dialog", Value = "tail" },
            ],
            stepDelayMs: 0,
            CancellationToken.None);

        CollectionAssert.AreEqual(Array.Empty<string>(), log);
        StringAssert.Contains(File.ReadAllText(ErrorLogger.LastWrittenPath!), "end_repeat");
    }

    [TestMethod]
    public void ExecuteActionsWithRepeats_OrphanEnd_Continues()
    {
        var log = new List<string>();
        UserDialog.ShowOkHandler = msg => log.Add(msg ?? string.Empty);

        KeySender.ExecuteActionsWithRepeats(
            [
                new ActionItem { Type = "end_repeat", Value = "" },
                new ActionItem { Type = "dialog", Value = "after" },
            ],
            stepDelayMs: 0,
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "after" }, log);
    }

    [TestMethod]
    public void ExecuteActionsWithRepeats_InvalidCount_SkipsStartAndContinues()
    {
        var log = new List<string>();
        UserDialog.ShowOkHandler = msg => log.Add(msg ?? string.Empty);

        KeySender.ExecuteActionsWithRepeats(
            [
                new ActionItem { Type = "repeat", Value = "abc" },
                new ActionItem { Type = "dialog", Value = "x" },
                new ActionItem { Type = "end_repeat", Value = "" },
            ],
            stepDelayMs: 0,
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "x" }, log);
    }

    [TestMethod]
    public void ExecuteMacro_CancelBeforeStart_ThrowsWithoutDialog()
    {
        var called = false;
        UserDialog.ShowOkHandler = _ => called = true;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsException<OperationCanceledException>(() =>
            KeySender.ExecuteMacro(
                new MacroItem
                {
                    DelaySec = 0,
                    Actions = [new ActionItem { Type = "dialog", Value = "x" }]
                },
                actionDelaySec: 0,
                cancellationToken: cts.Token));
        Assert.IsFalse(called);
    }
}
