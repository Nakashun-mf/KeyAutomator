using System.Runtime.InteropServices;
using KeyAutomator.Models;
using Windows.System;

namespace KeyAutomator.Services;

public static class KeySender
{
    private static readonly Dictionary<string, VirtualKey> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENTER"] = VirtualKey.Enter,
        ["RETURN"] = VirtualKey.Enter,
        ["TAB"] = VirtualKey.Tab,
        ["ESC"] = VirtualKey.Escape,
        ["ESCAPE"] = VirtualKey.Escape,
        ["BACKSPACE"] = VirtualKey.Back,
        ["BS"] = VirtualKey.Back,
        ["DELETE"] = VirtualKey.Delete,
        ["DEL"] = VirtualKey.Delete,
        ["INSERT"] = VirtualKey.Insert,
        ["INS"] = VirtualKey.Insert,
        ["HOME"] = VirtualKey.Home,
        ["END"] = VirtualKey.End,
        ["PAGEUP"] = VirtualKey.PageUp,
        ["PGUP"] = VirtualKey.PageUp,
        ["PAGEDOWN"] = VirtualKey.PageDown,
        ["PGDN"] = VirtualKey.PageDown,
        ["UP"] = VirtualKey.Up,
        ["DOWN"] = VirtualKey.Down,
        ["LEFT"] = VirtualKey.Left,
        ["RIGHT"] = VirtualKey.Right,
        ["SPACE"] = VirtualKey.Space,
        ["F1"] = VirtualKey.F1, ["F2"] = VirtualKey.F2, ["F3"] = VirtualKey.F3, ["F4"] = VirtualKey.F4,
        ["F5"] = VirtualKey.F5, ["F6"] = VirtualKey.F6, ["F7"] = VirtualKey.F7, ["F8"] = VirtualKey.F8,
        ["F9"] = VirtualKey.F9, ["F10"] = VirtualKey.F10, ["F11"] = VirtualKey.F11, ["F12"] = VirtualKey.F12,
    };

    private static readonly HashSet<VirtualKey> ExtendedKeys =
    [
        VirtualKey.Up, VirtualKey.Down, VirtualKey.Left, VirtualKey.Right,
        VirtualKey.Home, VirtualKey.End, VirtualKey.PageUp, VirtualKey.PageDown,
        VirtualKey.Insert, VirtualKey.Delete
    ];

    public static void ExecuteMacro(MacroItem macro)
    {
        ArgumentNullException.ThrowIfNull(macro);

        var delayMs = (int)(Math.Max(0, macro.DelaySec) * 1000);
        if (delayMs > 0)
            Thread.Sleep(delayMs);

        foreach (var action in macro.Actions)
            ExecuteAction(action);
    }

    public static void ExecuteAction(ActionItem action)
    {
        if (action is null) return;

        switch ((action.Type ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "text":
                SendText(action.Value ?? string.Empty);
                break;
            case "key":
                SendKey(action.Value ?? string.Empty);
                break;
            case "hotkey":
                SendHotkey(action.Value ?? string.Empty);
                break;
            case "wait":
                if (double.TryParse(action.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var sec) ||
                    double.TryParse(action.Value, out sec))
                {
                    Thread.Sleep((int)(Math.Max(0, sec) * 1000));
                }
                break;
            default:
                ErrorLogger.Write($"未知のアクション種別: {action.Type}");
                break;
        }
    }

    public static void SendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                SendVirtualKey(VirtualKey.Enter, keyDown: true);
                SendVirtualKey(VirtualKey.Enter, keyDown: false);
            }
            else if (ch == '\r')
            {
                // ignore CR
            }
            else
            {
                SendUnicodeChar(ch, keyDown: true);
                SendUnicodeChar(ch, keyDown: false);
            }

            Thread.Sleep(5);
        }
    }

    public static void SendKey(string keyName)
    {
        var key = ResolveKey(keyName);
        if (key == VirtualKey.None)
        {
            ErrorLogger.Write($"未対応のキー名: {keyName}");
            return;
        }

        SendVirtualKey(key, keyDown: true);
        Thread.Sleep(20);
        SendVirtualKey(key, keyDown: false);
        Thread.Sleep(10);
    }

    public static void SendHotkey(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return;

        var parts = hotkey.Split(['+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modifiers = new List<VirtualKey>();
        var mainKey = VirtualKey.None;

        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                case "CTL":
                    modifiers.Add(VirtualKey.Control);
                    break;
                case "ALT":
                case "MENU":
                    modifiers.Add(VirtualKey.Menu);
                    break;
                case "SHIFT":
                    modifiers.Add(VirtualKey.Shift);
                    break;
                case "WIN":
                case "WINDOWS":
                case "LWIN":
                    modifiers.Add(VirtualKey.LeftWindows);
                    break;
                default:
                    mainKey = ResolveKey(part);
                    break;
            }
        }

        if (mainKey == VirtualKey.None)
        {
            ErrorLogger.Write($"ホットキーのメインキーが不正: {hotkey}");
            return;
        }

        foreach (var mod in modifiers)
        {
            SendVirtualKey(mod, keyDown: true);
            Thread.Sleep(10);
        }

        SendVirtualKey(mainKey, keyDown: true);
        Thread.Sleep(20);
        SendVirtualKey(mainKey, keyDown: false);
        Thread.Sleep(10);

        for (var i = modifiers.Count - 1; i >= 0; i--)
        {
            SendVirtualKey(modifiers[i], keyDown: false);
            Thread.Sleep(10);
        }
    }

    public static VirtualKey ResolveKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return VirtualKey.None;
        var n = name.Trim();

        if (KeyMap.TryGetValue(n, out var mapped))
            return mapped;

        if (n.Length == 1)
        {
            var c = char.ToUpperInvariant(n[0]);
            if (c is >= 'A' and <= 'Z')
                return (VirtualKey)c;
            if (c is >= '0' and <= '9')
                return (VirtualKey)c;
        }

        return Enum.TryParse<VirtualKey>(n, ignoreCase: true, out var parsed)
            ? parsed
            : VirtualKey.None;
    }

    private static void SendUnicodeChar(char ch, bool keyDown)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = ch,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE | (keyDown ? 0u : NativeMethods.KEYEVENTF_KEYUP),
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        Send(input);
    }

    private static void SendVirtualKey(VirtualKey key, bool keyDown)
    {
        var flags = keyDown ? NativeMethods.KEYEVENTF_KEYDOWN : NativeMethods.KEYEVENTF_KEYUP;
        if (ExtendedKeys.Contains(key))
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        Send(input);
    }

    private static void Send(NativeMethods.INPUT input)
    {
        var sent = NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent == 0)
            ErrorLogger.Write($"SendInput 失敗 (GetLastError={Marshal.GetLastWin32Error()})");
    }
}
