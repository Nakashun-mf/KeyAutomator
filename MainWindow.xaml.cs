using System.ComponentModel;
using KeyAutomator.Models;
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
    private bool _syncingMacroSelection;
    private bool _syncingActionSelection;
    private bool _isHandlingDelete;
    private CancellationTokenSource? _runCts;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        TryResize(1180, 740);

        RootGrid.DataContext = _vm;
        VersionText.Text = $"v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"}";
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        ActionList.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(ActionList_KeyDown), handledEventsToo: true);
        MacroList.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(MacroList_KeyDown), handledEventsToo: true);

        SyncMacroListSelectionFromVm();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SelectedMacro))
            return;
        if (_syncingMacroSelection)
            return;

        if (ReferenceEquals(MacroList.SelectedItem, _vm.SelectedMacro) &&
            MacroList.SelectedItems.Count == (_vm.SelectedMacro is null ? 0 : 1))
            return;

        SyncMacroListSelectionFromVm();
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

        if (Content?.XamlRoot is null)
            return false;

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

    private async Task<ContentDialogResult> PromptDirtyAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "未保存の変更",
            Content = "編集中の内容が保存されていません。どうしますか？",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "破棄",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync();
    }

    private async Task<bool> EnsureCanLeaveEditorAsync()
    {
        if (!_vm.IsDirty)
            return true;

        var result = await PromptDirtyAsync();
        if (result == ContentDialogResult.None)
            return false;
        if (result == ContentDialogResult.Primary)
            return _vm.TrySaveMacro();
        // Secondary = discard
        _vm.CancelEditCommand.Execute(null);
        return true;
    }

    private async void NewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.IsBusy) return;
        if (!await EnsureCanLeaveEditorAsync()) return;
        _vm.NewMacroCommand.Execute(null);
        SyncMacroListSelectionFromVm();
    }

    private async void CloneButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasSelection || _vm.IsBusy) return;
        if (!await EnsureCanLeaveEditorAsync()) return;
        _vm.CloneMacroCommand.Execute(null);
        SyncMacroListSelectionFromVm();
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e) => await TryDeleteMacroAsync();

    private async Task TryDeleteMacroAsync()
    {
        if (_isHandlingDelete || _vm.IsBusy) return;
        var targets = _vm.GetMacrosPendingDelete();
        if (targets.Count == 0) return;

        _isHandlingDelete = true;
        try
        {
            var message = targets.Count == 1
                ? $"ID {targets[0].Id}「{targets[0].Name}」を削除しますか？"
                : $"選択した {targets.Count} 件のマクロを削除しますか？";

            var ok = await ConfirmDeleteAsync("マクロを削除", message);
            if (!ok) return;

            _vm.DeleteSelectedMacros();
            SyncMacroListSelectionFromVm();
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "マクロ削除");
            _vm.StatusMessage = "削除中にエラーが発生しました";
        }
        finally
        {
            _isHandlingDelete = false;
        }
    }

    private async void MacroList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingMacroSelection) return;

        var selected = MacroList.SelectedItems.OfType<MacroItem>().ToList();
        var primary = MacroList.SelectedItem as MacroItem;

        // ListView が一時的に選択解除することがある。そのときは復元して終了。
        if (primary is null && selected.Count == 0 && _vm.SelectedMacro is not null)
        {
            RestoreMacroSelection(_vm.SelectedMacro);
            return;
        }

        if (ReferenceEquals(primary, _vm.SelectedMacro))
        {
            _vm.SyncMacroSelection(selected);
            return;
        }

        if (_vm.IsDirty)
        {
            var result = await PromptDirtyAsync();
            if (result == ContentDialogResult.None)
            {
                RestoreMacroSelection(_vm.SelectedMacro);
                return;
            }

            if (result == ContentDialogResult.Primary && !_vm.TrySaveMacro())
            {
                RestoreMacroSelection(_vm.SelectedMacro);
                return;
            }

            if (result == ContentDialogResult.Secondary)
                _vm.CancelEditCommand.Execute(null);
        }

        _vm.SyncMacroSelection(selected);
        _vm.SelectedMacro = primary;
    }

    private void RestoreMacroSelection(MacroItem? macro)
    {
        _syncingMacroSelection = true;
        try
        {
            MacroList.SelectedItems.Clear();
            if (macro is not null)
                MacroList.SelectedItems.Add(macro);
        }
        finally
        {
            _syncingMacroSelection = false;
        }
    }

    private void SyncMacroListSelectionFromVm()
    {
        _syncingMacroSelection = true;
        try
        {
            MacroList.SelectedItems.Clear();
            if (_vm.SelectedMacro is not null)
                MacroList.SelectedItems.Add(_vm.SelectedMacro);
        }
        finally
        {
            _syncingMacroSelection = false;
        }
    }

    private void ActionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingActionSelection) return;
        var selected = ActionList.SelectedItems.Cast<ActionEditItem>().ToList();
        _vm.SyncActionSelection(selected);
        _vm.SelectedAction = ActionList.SelectedItem as ActionEditItem;
    }

    private static bool IsEditingTextualControl(object? focused) =>
        focused is TextBox or ComboBox or NumberBox or AutoSuggestBox;

    private object? TryGetFocusedElement()
    {
        try
        {
            if (Content?.XamlRoot is null) return null;
            return FocusManager.GetFocusedElement(Content.XamlRoot);
        }
        catch
        {
            return null;
        }
    }

    private async void MacroList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Delete)
            return;
        if (IsEditingTextualControl(TryGetFocusedElement()))
            return;

        e.Handled = true;
        // KeyDown 中に ContentDialog を出すと落ちることがあるため、次のティックへ退避
        DispatcherQueue.TryEnqueue(() => _ = TryDeleteMacroAsync());
    }

    private void MacroList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) =>
        _vm.PersistMacroOrder();

    private void ActionList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) =>
        _vm.OnActionsReordered();

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // 保存中の選択イベントで未保存ダイアログが出ないようにする
        _syncingMacroSelection = true;
        try
        {
            _vm.SaveMacroCommand.Execute(null);
        }
        finally
        {
            _syncingMacroSelection = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _vm.CancelEditCommand.Execute(null);

    private void AddText_Click(object sender, RoutedEventArgs e) => _vm.AddTextActionCommand.Execute(null);
    private void AddKey_Click(object sender, RoutedEventArgs e) => _vm.AddKeyActionCommand.Execute(null);
    private void AddWait_Click(object sender, RoutedEventArgs e) => _vm.AddWaitActionCommand.Execute(null);
    private void AddDialog_Click(object sender, RoutedEventArgs e) => _vm.AddDialogActionCommand.Execute(null);
    private void AddHotkey_Click(object sender, RoutedEventArgs e) => _vm.AddHotkeyActionCommand.Execute(null);
    private void AddMouse_Click(object sender, RoutedEventArgs e) => _vm.AddMouseActionCommand.Execute(null);
    private async void RemoveAction_Click(object sender, RoutedEventArgs e) => await TryRemoveActionAsync();
    private void MoveUp_Click(object sender, RoutedEventArgs e) => _vm.MoveActionUpCommand.Execute(null);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => _vm.MoveActionDownCommand.Execute(null);

    private async Task TryRemoveActionAsync()
    {
        if (_isHandlingDelete || _vm.IsBusy) return;
        var targets = _vm.GetActionsPendingDelete();
        if (targets.Count == 0) return;

        _isHandlingDelete = true;
        try
        {
            var message = targets.Count == 1
                ? $"手順 {targets[0].Step}（{targets[0].TypeLabel}）を削除しますか？"
                : $"選択した {targets.Count} 件の手順を削除しますか？";

            var ok = await ConfirmDeleteAsync("手順を削除", message);
            if (ok)
                _vm.RemoveSelectedActions();
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "手順削除");
            _vm.StatusMessage = "削除中にエラーが発生しました";
        }
        finally
        {
            _isHandlingDelete = false;
        }
    }

    private void ActionList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Delete)
            return;

        if (IsEditingTextualControl(TryGetFocusedElement()))
            return;

        e.Handled = true;
        DispatcherQueue.TryEnqueue(() => _ = TryRemoveActionAsync());
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.IsBusy)
        {
            _runCts?.Cancel();
            _vm.StatusMessage = "中断しています…";
            return;
        }

        var macro = _vm.BuildCurrentMacroForRun();
        if (macro is null) return;

        var confirm = new ContentDialog
        {
            Title = "テスト実行",
            Content = $"起動前ウェイト {macro.DelaySec:0.##} 秒の間に、入力先ウィンドウをアクティブにしてください。\n実行中は「中断」で止められます。",
            PrimaryButtonText = "実行",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            return;

        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;
        _vm.IsBusy = true;
        _vm.StatusMessage = "実行中…（中断ボタンで停止）";
        try
        {
            // ウィンドウは隠さず、ダイアログ／フォーカスの問題を避ける
            await Task.Run(() => KeySender.ExecuteMacro(macro, _vm.ActionDelaySec, token), token);
            _vm.StatusMessage = token.IsCancellationRequested ? "実行を中断しました" : "テスト実行完了";
        }
        catch (OperationCanceledException)
        {
            _vm.StatusMessage = "実行を中断しました";
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
            Activate();
            _vm.IsBusy = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }
}
