using KeyAutomator.Services;
using KeyAutomator.ViewModels;
using Windows.System;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class KeySenderCaptureTests
{
    [TestCleanup]
    public void Cleanup() => KeySender.SendInputOverride = null;

    [TestMethod]
    public void SendKey_AllMappedNames_EmitDownThenUp()
    {
        using var capture = new InputCapture();
        foreach (var name in KeySender.MappedKeys.Keys)
        {
            capture.Events.Clear();
            KeySender.SendKey(name);
            var keys = capture.Keyboard.ToList();
            Assert.AreEqual(2, keys.Count, name);
            Assert.AreEqual((ushort)KeySender.MappedKeys[name], keys[0].wVk, name);
            Assert.AreEqual(0u, keys[0].dwFlags & NativeMethods.KEYEVENTF_KEYUP, name);
            Assert.AreNotEqual(0u, keys[1].dwFlags & NativeMethods.KEYEVENTF_KEYUP, name);
        }
    }

    [TestMethod]
    public void SendKey_RepresentativeLettersAndDigits_EmitMatchingVirtualKey()
    {
        using var capture = new InputCapture();
        foreach (var name in new[] { "A", "a", "Z", "0", "5", "9" })
        {
            capture.Events.Clear();
            KeySender.SendKey(name);
            var expected = KeySender.ResolveKey(name);
            Assert.AreNotEqual(VirtualKey.None, expected, name);
            var keys = capture.Keyboard.ToList();
            Assert.AreEqual(2, keys.Count, name);
            Assert.AreEqual((ushort)expected, keys[0].wVk, name);
        }
    }

    [TestMethod]
    public void SendText_RepresentativeCharacters_EmitUnicodeDownUp()
    {
        using var capture = new InputCapture();
        foreach (var ch in new[] { ' ', 'A', 'z', '0', '~', 'あ', '漢' })
        {
            capture.Events.Clear();
            KeySender.SendText(ch.ToString());
            var keys = capture.Keyboard.ToList();
            Assert.AreEqual(2, keys.Count, $"'{ch}'");
            Assert.AreEqual(0, keys[0].wVk);
            Assert.AreEqual((ushort)ch, keys[0].wScan);
            Assert.AreNotEqual(0u, keys[0].dwFlags & NativeMethods.KEYEVENTF_UNICODE);
            Assert.AreNotEqual(0u, keys[1].dwFlags & NativeMethods.KEYEVENTF_KEYUP);
        }
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
    public void SendHotkey_RepresentativeChords_PressModifierThenMainThenRelease()
    {
        using var capture = new InputCapture();
        foreach (var (chord, mod, main) in new (string, string, string)[]
                 {
                     ("CTRL+S", "CTRL", "S"),
                     ("ALT+F4", "ALT", "F4"),
                     ("SHIFT+TAB", "SHIFT", "TAB"),
                     ("WIN+R", "WIN", "R"),
                     ("CTRL+ENTER", "CTRL", "ENTER"),
                 })
        {
            capture.Events.Clear();
            KeySender.SendHotkey(chord);
            var keys = capture.Keyboard.ToList();
            Assert.AreEqual(4, keys.Count, chord);
            Assert.AreEqual((ushort)KeySender.ResolveKey(mod), keys[0].wVk, chord);
            Assert.AreEqual((ushort)KeySender.ResolveKey(main), keys[1].wVk, chord);
            Assert.AreNotEqual(0u, keys[2].dwFlags & NativeMethods.KEYEVENTF_KEYUP, chord);
            Assert.AreEqual((ushort)KeySender.ResolveKey(mod), keys[3].wVk, chord);
        }
    }

    [TestMethod]
    [DataRow("LEFT", NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 1)]
    [DataRow("RIGHT", NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP, 1)]
    [DataRow("MIDDLE", NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP, 1)]
    [DataRow("LEFT_DOUBLE", NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 2)]
    [DataRow("DOUBLE", NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 2)]
    [DataRow("DBLCLICK", NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 2)]
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
