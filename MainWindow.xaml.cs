using KeyAutomator.Services;
using KeyAutomator.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;

namespace KeyAutomator;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private string _capturedHotkey = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        TryResize(1180, 740);

        RootGrid.DataContext = _vm;
        VersionText.Text = $"v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"}";
    }

    private void TryResize(int width, int height)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(id);
            appWindow.Resize(new SizeInt32(width, height));
        }
        catch
        {
            // ignore
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs e) => _vm.NewMacroCommand.Execute(null);

    private void CloneButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasSelection) return;
        _vm.CloneMacroCommand.Execute(null);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedMacro is null) return;

        var dialog = new ContentDialog
        {
            Title = "マクロを削除",
            Content = $"ID {_vm.SelectedMacro.Id}「{_vm.SelectedMacro.Name}」を削除しますか？",
            PrimaryButtonText = "削除",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _vm.DeleteMacroCommand.Execute(null);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => _vm.SaveMacroCommand.Execute(null);

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _vm.CancelEditCommand.Execute(null);

    private void AddText_Click(object sender, RoutedEventArgs e) => _vm.AddTextActionCommand.Execute(null);
    private void AddKey_Click(object sender, RoutedEventArgs e) => _vm.AddKeyActionCommand.Execute(null);
    private void AddWait_Click(object sender, RoutedEventArgs e) => _vm.AddWaitActionCommand.Execute(null);
    private void RemoveAction_Click(object sender, RoutedEventArgs e) => _vm.RemoveActionCommand.Execute(null);
    private void MoveUp_Click(object sender, RoutedEventArgs e) => _vm.MoveActionUpCommand.Execute(null);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => _vm.MoveActionDownCommand.Execute(null);

    private async void AddHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasSelection) return;

        _capturedHotkey = string.Empty;
        HotkeyResultText.Text = "キーを押してください…";
        HotkeyDialog.IsPrimaryButtonEnabled = false;
        HotkeyDialog.XamlRoot = Content.XamlRoot;
        await HotkeyDialog.ShowAsync();
    }

    private void HotkeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(_capturedHotkey))
            _vm.AddHotkeyAction(_capturedHotkey);
    }

    private void HotkeyCaptureArea_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        e.Handled = true;
        var key = e.Key;

        if (key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
            or VirtualKey.LeftWindows or VirtualKey.RightWindows)
        {
            return;
        }

        var parts = new List<string>();
        static bool IsDown(VirtualKey vk) =>
            InputKeyboardSource.GetKeyStateForCurrentThread(vk).HasFlag(CoreVirtualKeyStates.Down);

        if (IsDown(VirtualKey.Control)) parts.Add("CTRL");
        if (IsDown(VirtualKey.Menu)) parts.Add("ALT");
        if (IsDown(VirtualKey.Shift)) parts.Add("SHIFT");

        var keyName = FormatKey(key);
        if (string.IsNullOrEmpty(keyName)) return;

        parts.Add(keyName);
        _capturedHotkey = string.Join("+", parts);
        HotkeyResultText.Text = _capturedHotkey;
        HotkeyDialog.IsPrimaryButtonEnabled = true;
    }

    private static string FormatKey(VirtualKey key) => key switch
    {
        VirtualKey.Enter => "ENTER",
        VirtualKey.Tab => "TAB",
        VirtualKey.Escape => "ESC",
        VirtualKey.Back => "BACKSPACE",
        VirtualKey.Delete => "DELETE",
        VirtualKey.Insert => "INSERT",
        VirtualKey.Home => "HOME",
        VirtualKey.End => "END",
        VirtualKey.PageUp => "PAGEUP",
        VirtualKey.PageDown => "PAGEDOWN",
        VirtualKey.Up => "UP",
        VirtualKey.Down => "DOWN",
        VirtualKey.Left => "LEFT",
        VirtualKey.Right => "RIGHT",
        VirtualKey.Space => "SPACE",
        >= VirtualKey.F1 and <= VirtualKey.F12 => key.ToString().ToUpperInvariant(),
        >= VirtualKey.A and <= VirtualKey.Z => ((char)key).ToString(),
        >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((char)key).ToString(),
        _ => key.ToString().ToUpperInvariant()
    };

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var macro = _vm.BuildCurrentMacroForRun();
        if (macro is null) return;

        var confirm = new ContentDialog
        {
            Title = "テスト実行",
            Content = $"起動前ウェイト {macro.DelaySec:0.##} 秒の間に、入力先ウィンドウをアクティブにしてください。",
            PrimaryButtonText = "実行",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            return;

        _vm.IsBusy = true;
        TestButton.IsEnabled = false;
        _vm.StatusMessage = "実行中…";
        try
        {
            AppWindow.Hide();
            await Task.Run(() => KeySender.ExecuteMacro(macro));
            _vm.StatusMessage = "テスト実行完了";
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "テスト実行");
            _vm.StatusMessage = "実行エラー（error.log を確認）";
            var err = new ContentDialog
            {
                Title = "実行エラー",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await err.ShowAsync();
        }
        finally
        {
            AppWindow.Show();
            Activate();
            _vm.IsBusy = false;
            TestButton.IsEnabled = true;
        }
    }
}
