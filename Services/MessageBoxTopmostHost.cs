using System.Text;

namespace KeyAutomator.Services;

/// <summary>
/// 確認 MessageBox を他ウィンドウの裏に回さないための補助。
/// MB_TOPMOST だけではフォーカス制限で背面に出ることがあるため、
/// 最前面オーナー＋表示中の再適用で押し続ける。
/// </summary>
internal static class MessageBoxTopmostHost
{
    private const string DialogClassName = "#32770";

    public static void ShowOk(string text, string caption)
    {
        var owner = CreateTopmostOwner();
        using var keeper = new TopmostKeeper(caption);

        try
        {
            NativeMethods.MessageBoxW(
                owner,
                text,
                caption,
                NativeMethods.MB_OK
                | NativeMethods.MB_ICONINFORMATION
                | NativeMethods.MB_SETFOREGROUND
                | NativeMethods.MB_TOPMOST
                | NativeMethods.MB_SYSTEMMODAL);
        }
        finally
        {
            if (owner != IntPtr.Zero)
                NativeMethods.DestroyWindow(owner);
        }
    }

    /// <summary>サイズ 0 の TOPMOST オーナー。MessageBox の親にして Z 順を引き上げる。</summary>
    internal static IntPtr CreateTopmostOwner()
    {
        try
        {
            var module = NativeMethods.GetModuleHandleW(null);
            var hwnd = NativeMethods.CreateWindowExW(
                NativeMethods.WsExTopmost | NativeMethods.WsExToolwindow,
                "Static",
                "KeyAutomatorDialogOwner",
                NativeMethods.WsPopup,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                module,
                IntPtr.Zero);

            if (hwnd == IntPtr.Zero)
                return IntPtr.Zero;

            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HwndTopmost,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNomove
                | NativeMethods.SwpNosize
                | NativeMethods.SwpNoactivate
                | NativeMethods.SwpShowwindow);
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwShownoactivate);
            return hwnd;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    internal static IntPtr FindVisibleDialog(uint processId, string caption)
    {
        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != processId || !NativeMethods.IsWindowVisible(hWnd))
                return true;

            var className = new StringBuilder(64);
            NativeMethods.GetClassNameW(hWnd, className, className.Capacity);
            if (!string.Equals(className.ToString(), DialogClassName, StringComparison.Ordinal))
                return true;

            var title = new StringBuilder(256);
            NativeMethods.GetWindowTextW(hWnd, title, title.Capacity);
            if (!string.Equals(title.ToString(), caption, StringComparison.Ordinal))
                return true;

            found = hWnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    internal static void PromoteToTopmost(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpShowwindow);
        NativeMethods.BringWindowToTop(hwnd);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    private sealed class TopmostKeeper : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Thread _thread;

        public TopmostKeeper(string caption)
        {
            var captionCopy = caption;
            var pid = (uint)Environment.ProcessId;
            _thread = new Thread(() =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var hwnd = FindVisibleDialog(pid, captionCopy);
                        if (hwnd != IntPtr.Zero)
                            PromoteToTopmost(hwnd);
                        Thread.Sleep(120);
                    }
                    catch (ThreadInterruptedException)
                    {
                        break;
                    }
                    catch
                    {
                        // 最前面維持の失敗で本体を落とさない
                    }
                }
            })
            {
                IsBackground = true,
                Name = "KeyAutomator.MessageBoxTopmostKeeper"
            };
            _thread.Start();
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _ = _thread.Join(800);
            }
            catch
            {
                // ignore
            }

            _cts.Dispose();
        }
    }
}
