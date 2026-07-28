using KeyAutomator.Services;
using KeyAutomator.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace KeyAutomator;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        TryResize(1180, 740);

        RootGrid.DataContext = _vm;
        VersionText.Text = $"v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"}";

        // ComboBox 等にフォーカスがあっても Delete を拾う
        ActionList.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(ActionList_KeyDown), handledEventsToo: true);
        MacroList.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(MacroList_KeyDown), handledEventsToo: true);
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

    private async Task<bool> ConfirmDeleteAsync(string title, string message)
    {
        if (!_vm.ConfirmBeforeDelete)
            return true;

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "削除",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void NewButton_Click(object sender, RoutedEventArgs e) => _vm.NewMacroCommand.Execute(null);

    private void CloneButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasSelection) return;
        _vm.CloneMacroCommand.Execute(null);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e) => await TryDeleteMacroAsync();

    private async Task TryDeleteMacroAsync()
    {
        if (_vm.SelectedMacro is null) return;

        var ok = await ConfirmDeleteAsync(
            "マクロを削除",
            $"ID {_vm.SelectedMacro.Id}「{_vm.SelectedMacro.Name}」を削除しますか？");
        if (ok)
            _vm.DeleteSelectedMacro();
    }

    private async void MacroList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Delete)
            return;
        if (FocusManager.GetFocusedElement(Content.XamlRoot) is TextBox)
            return;

        e.Handled = true;
        await TryDeleteMacroAsync();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => _vm.SaveMacroCommand.Execute(null);

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _vm.CancelEditCommand.Execute(null);

    private void AddText_Click(object sender, RoutedEventArgs e) => _vm.AddTextActionCommand.Execute(null);
    private void AddKey_Click(object sender, RoutedEventArgs e) => _vm.AddKeyActionCommand.Execute(null);
    private void AddWait_Click(object sender, RoutedEventArgs e) => _vm.AddWaitActionCommand.Execute(null);
    private void AddHotkey_Click(object sender, RoutedEventArgs e) => _vm.AddHotkeyActionCommand.Execute(null);
    private void AddMouse_Click(object sender, RoutedEventArgs e) => _vm.AddMouseActionCommand.Execute(null);
    private async void RemoveAction_Click(object sender, RoutedEventArgs e) => await TryRemoveActionAsync();
    private void MoveUp_Click(object sender, RoutedEventArgs e) => _vm.MoveActionUpCommand.Execute(null);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => _vm.MoveActionDownCommand.Execute(null);

    private async Task TryRemoveActionAsync()
    {
        if (_vm.SelectedAction is null) return;

        var step = _vm.SelectedAction.Step;
        var label = _vm.SelectedAction.TypeLabel;
        var ok = await ConfirmDeleteAsync(
            "手順を削除",
            $"手順 {step}（{label}）を削除しますか？");
        if (ok)
            _vm.RemoveSelectedAction();
    }

    private async void ActionList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Delete)
            return;

        if (FocusManager.GetFocusedElement(Content.XamlRoot) is TextBox)
            return;

        e.Handled = true;
        await TryRemoveActionAsync();
    }

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
