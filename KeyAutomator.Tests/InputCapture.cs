using KeyAutomator.Services;

namespace KeyAutomator.Tests;

/// <summary>Win32 SendInput の代わりにイベントを記録する。実キーは送らない。</summary>
internal sealed class InputCapture : IDisposable
{
    public List<NativeMethods.INPUT> Events { get; } = [];

    public InputCapture()
    {
        KeySender.SendInputOverride = input =>
        {
            Events.Add(input);
            return 1;
        };
    }

    public void Dispose() => KeySender.SendInputOverride = null;

    public IEnumerable<NativeMethods.KEYBDINPUT> Keyboard =>
        Events.Where(e => e.type == NativeMethods.INPUT_KEYBOARD).Select(e => e.U.ki);

    public IEnumerable<NativeMethods.MOUSEINPUT> Mouse =>
        Events.Where(e => e.type == NativeMethods.INPUT_MOUSE).Select(e => e.U.mi);
}
