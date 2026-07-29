using System.Diagnostics;
using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class KeySenderActionDelayTests
{
    [TestCleanup]
    public void Cleanup() => UserDialog.ShowOkHandler = null;

    [TestMethod]
    public void ExecuteMacro_WithActionDelay_InsertsDelayBetweenActions()
    {
        UserDialog.ShowOkHandler = _ => { };
        var macro = new MacroItem
        {
            DelaySec = 0,
            Actions =
            [
                new ActionItem { Type = "dialog", Value = "a" },
                new ActionItem { Type = "dialog", Value = "b" }
            ]
        };

        var sw = Stopwatch.StartNew();
        KeySender.ExecuteMacro(macro, actionDelaySec: 0.2);
        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds >= 180,
            $"Expected >= 180ms between actions, got {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void ExecuteMacro_WithZeroActionDelay_DoesNotInsertExtraDelay()
    {
        UserDialog.ShowOkHandler = _ => { };
        var macro = new MacroItem
        {
            DelaySec = 0,
            Actions =
            [
                new ActionItem { Type = "dialog", Value = "a" },
                new ActionItem { Type = "dialog", Value = "b" }
            ]
        };

        var sw = Stopwatch.StartNew();
        KeySender.ExecuteMacro(macro, actionDelaySec: 0);
        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds < 100,
            $"Expected fast execution without step delay, got {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void ExecuteMacro_SingleAction_DoesNotApplyTrailingDelay()
    {
        UserDialog.ShowOkHandler = _ => { };
        var macro = new MacroItem
        {
            DelaySec = 0,
            Actions = [new ActionItem { Type = "dialog", Value = "only" }]
        };

        var sw = Stopwatch.StartNew();
        KeySender.ExecuteMacro(macro, actionDelaySec: 0.5);
        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds < 200,
            $"Single action should not sleep after itself, got {sw.ElapsedMilliseconds}ms");
    }
}

[TestClass]
public class AppSettingsTests
{
    [TestMethod]
    public void AppSettings_DefaultActionDelaySec_IsPointTwo()
    {
        var settings = new AppSettings();

        Assert.AreEqual(0.2, settings.ActionDelaySec);
        Assert.AreEqual(0.2, AppSettings.DefaultActionDelaySec);
    }
}
