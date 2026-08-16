using KeyAutomator.Services;
using KeyAutomator.ViewModels;
using Windows.System;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class KeySenderCaptureTests
{
    [TestCleanup]
    public void Cleanup() => KeySender.SendInputOverride = null;

    public static IEnumerable<object[]> MappedKeyNames()
    {
        foreach (var name in KeySender.MappedKeys.Keys)
            yield return new object[] { name };
    }

    public static IEnumerable<object[]> LettersAndDigits()
    {
        for (var c = 'A'; c <= 'Z'; c++)
        {
            yield return new object[] { c.ToString() };
            yield return new object[] { char.ToLowerInvariant(c).ToString() };
        }

        for (var d = '0'; d <= '9'; d++)
            yield return new object[] { d.ToString() };
    }

    public static IEnumerable<object[]> PrintableAscii()
    {
        for (var c = (char)0x20; c <= 0x7E; c++)
            yield return new object[] { c };
    }

    public static IEnumerable<object[]> HotkeyMatrix()
    {
        string[] mods = ["CTRL", "ALT", "SHIFT", "WIN"];
        var mains = new List<string>();
        for (var c = 'A'; c <= 'Z'; c++)
            mains.Add(c.ToString());
        for (var d = 0; d <= 9; d++)
            mains.Add(d.ToString());
        for (var f = 1; f <= 12; f++)
            mains.Add($"F{f}");
        mains.Add("ENTER");
        mains.Add("TAB");

        foreach (var mod in mods)
        {
            foreach (var main in mains)
                yield return new object[] { $"{mod}+{main}", mod, main };
        }
    }

    public static IEnumerable<object[]> MouseActions()
    {
        yield return new object[] { "LEFT", NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 1 };
        yield return new object[] { "RIGHT", NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP, 1 };
        yield return new object[] { "MIDDLE", NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP, 1 };
        yield return new object[] { "LEFT_DOUBLE", NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 2 };
        yield return new object[] { "DOUBLE", NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 2 };
        yield return new object[] { "DBLCLICK", NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 2 };
    }

    [TestMethod]
    [DynamicData(nameof(MappedKeyNames), DynamicDataSourceType.Method)]
    public void SendKey_MappedName_EmitsDownThenUp(string name)
    {
        using var capture = new InputCapture();
        KeySender.SendKey(name);

        var keys = capture.Keyboard.ToList();
        Assert.AreEqual(2, keys.Count, name);
        Assert.AreEqual((ushort)KeySender.MappedKeys[name], keys[0].wVk);
        Assert.AreEqual(0u, keys[0].dwFlags & NativeMethods.KEYEVENTF_KEYUP);
        Assert.AreEqual((ushort)KeySender.MappedKeys[name], keys[1].wVk);
        Assert.AreNotEqual(0u, keys[1].dwFlags & NativeMethods.KEYEVENTF_KEYUP);
    }

    [TestMethod]
    [DynamicData(nameof(LettersAndDigits), DynamicDataSourceType.Method)]
    public void SendKey_LetterOrDigit_EmitsMatchingVirtualKey(string name)
    {
        using var capture = new InputCapture();
        KeySender.SendKey(name);

        var expected = KeySender.ResolveKey(name);
        Assert.AreNotEqual(VirtualKey.None, expected, name);
        var keys = capture.Keyboard.ToList();
        Assert.AreEqual(2, keys.Count, name);
        Assert.AreEqual((ushort)expected, keys[0].wVk);
    }

    [TestMethod]
    [DynamicData(nameof(PrintableAscii), DynamicDataSourceType.Method)]
    public void SendText_PrintableAscii_EmitsUnicodeDownUp(char ch)
    {
        using var capture = new InputCapture();
        KeySender.SendText(ch.ToString());

        var keys = capture.Keyboard.ToList();
        Assert.AreEqual(2, keys.Count, $"'{ch}'");
        Assert.AreEqual(0, keys[0].wVk);
        Assert.AreEqual((ushort)ch, keys[0].wScan);
        Assert.AreNotEqual(0u, keys[0].dwFlags & NativeMethods.KEYEVENTF_UNICODE);
        Assert.AreNotEqual(0u, keys[1].dwFlags & NativeMethods.KEYEVENTF_KEYUP);
    }

    [TestMethod]
    public void SendText_Newline_UsesEnterVirtualKey_AndIgnoresCr()
    {
        using var capture = new InputCapture();
        KeySender.SendText("\r\n");

        var keys = capture.Keyboard.ToList();
        Assert.AreEqual(2, keys.Count);
        Assert.AreEqual((ushort)VirtualKey.Enter, keys[0].wVk);
        Assert.AreEqual((ushort)VirtualKey.Enter, keys[1].wVk);
        Assert.AreNotEqual(0u, keys[1].dwFlags & NativeMethods.KEYEVENTF_KEYUP);
    }

    [TestMethod]
    [DynamicData(nameof(HotkeyMatrix), DynamicDataSourceType.Method)]
    public void SendHotkey_ModifierPlusMain_PressesModifierThenMainThenReleases(string chord, string mod, string main)
    {
        using var capture = new InputCapture();
        KeySender.SendHotkey(chord);

        var keys = capture.Keyboard.ToList();
        Assert.AreEqual(4, keys.Count, chord);
        Assert.AreEqual((ushort)KeySender.ResolveKey(mod), keys[0].wVk, chord);
        Assert.AreEqual(0u, keys[0].dwFlags & NativeMethods.KEYEVENTF_KEYUP, chord);
        Assert.AreEqual((ushort)KeySender.ResolveKey(main), keys[1].wVk, chord);
        Assert.AreEqual((ushort)KeySender.ResolveKey(main), keys[2].wVk, chord);
        Assert.AreNotEqual(0u, keys[2].dwFlags & NativeMethods.KEYEVENTF_KEYUP, chord);
        Assert.AreEqual((ushort)KeySender.ResolveKey(mod), keys[3].wVk, chord);
        Assert.AreNotEqual(0u, keys[3].dwFlags & NativeMethods.KEYEVENTF_KEYUP, chord);
    }

    [TestMethod]
    [DynamicData(nameof(MouseActions), DynamicDataSourceType.Method)]
    public void SendMouse_KnownAction_EmitsDownUpPairs(string action, uint down, uint up, int clicks)
    {
        using var capture = new InputCapture();
        KeySender.SendMouse(action);

        var mouse = capture.Mouse.ToList();
        Assert.AreEqual(clicks * 2, mouse.Count, action);
        for (var i = 0; i < clicks; i++)
        {
            Assert.AreEqual(down, mouse[i * 2].dwFlags, action);
            Assert.AreEqual(up, mouse[i * 2 + 1].dwFlags, action);
        }
    }

    [TestMethod]
    public void SendKey_Unknown_DoesNotEmitInput()
    {
        using var capture = new InputCapture();
        KeySender.SendKey("NOT_A_KEY_ZZZ");
        Assert.AreEqual(0, capture.Events.Count);
    }

    [TestMethod]
    public void CatalogKeys_SendKey_NeverNoOp()
    {
        using var capture = new InputCapture();
        foreach (var option in SpecialKeyCatalog.All)
        {
            capture.Events.Clear();
            KeySender.SendKey(option.Code);
            Assert.AreEqual(2, capture.Events.Count, option.Code);
        }
    }
}
