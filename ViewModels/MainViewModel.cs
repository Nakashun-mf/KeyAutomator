using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IReadOnlyList<ActionTypeOption> ActionTypeOptions { get; } = ActionTypeCatalog.All;

    public ObservableCollection<MacroItem> Macros { get; } = [];
    public ObservableCollection<ActionEditItem> Actions { get; } = [];
    public ObservableCollection<MacroItem> SelectedMacros { get; } = [];
    public ObservableCollection<ActionEditItem> SelectedActions { get; } = [];

    [ObservableProperty] private MacroItem? _selectedMacro;
    [ObservableProperty] private ActionEditItem? _selectedAction;
    [ObservableProperty] private double _editId = 1;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editAlias = string.Empty;
    [ObservableProperty] private double _editDelaySec;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _hasMacroMultiSelection;
    [ObservableProperty] private bool _hasActionSelection;
    [ObservableProperty] private string _statusMessage = "準備完了";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isIdle = true;
    [ObservableProperty] private string _actionSummary = "手順はまだありません";
    [ObservableProperty] private bool _confirmBeforeDelete = true;
    [ObservableProperty] private double _actionDelaySec = AppSettings.DefaultActionDelaySec;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _testButtonLabel = "テスト実行";
    [ObservableProperty] private bool _isMacroListEmpty = true;

    private AppSettings _settings = new();
    private bool _loadingSettings;
    private bool _loadingEditor;
    private bool _suppressDirty;

    public MainViewModel()
    {
        Actions.CollectionChanged += OnActionsChanged;
        Macros.CollectionChanged += (_, _) => IsMacroListEmpty = Macros.Count == 0;
        _loadingSettings = true;
        _settings = SettingsStore.Load();
        ConfirmBeforeDelete = _settings.ConfirmBeforeDelete;
        ActionDelaySec = _settings.ActionDelaySec;
        _loadingSettings = false;
        Reload();
    }

    partial void OnConfirmBeforeDeleteChanged(bool value)
    {
        if (_loadingSettings) return;
        _settings.ConfirmBeforeDelete = value;
        SettingsStore.Save(_settings);
    }

    partial void OnActionDelaySecChanged(double value)
    {
        if (_loadingSettings) return;
        _settings.ActionDelaySec = Math.Max(0, value);
        SettingsStore.Save(_settings);
    }

    partial void OnEditIdChanged(double value) => MarkDirty();
    partial void OnEditNameChanged(string value) => MarkDirty();
    partial void OnEditAliasChanged(string value) => MarkDirty();
    partial void OnEditDelaySecChanged(double value) => MarkDirty();

    private void MarkDirty()
    {
        if (_suppressDirty || _loadingEditor) return;
        IsDirty = true;
    }

    private void ClearDirty() => IsDirty = false;

    private void OnActionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenumberSteps();
        UpdateActionSummary();
        SyncActionSelectionHighlight();
        MarkDirty();

        if (e.NewItems is not null)
        {
            foreach (ActionEditItem item in e.NewItems)
                item.PropertyChanged += OnActionItemPropertyChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (ActionEditItem item in e.OldItems)
                item.PropertyChanged -= OnActionItemPropertyChanged;
        }
    }

    private void OnActionItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ActionEditItem.Type) or nameof(ActionEditItem.Value))
            MarkDirty();
    }

    partial void OnSelectedActionChanged(ActionEditItem? value)
    {
        SyncActionSelectionHighlight();
        UpdateActionSelectionFlags();
    }

    private void SyncActionSelectionHighlight()
    {
        foreach (var action in Actions)
            action.IsSelected = SelectedActions.Contains(action) || ReferenceEquals(action, SelectedAction);
    }

    private void RenumberSteps()
    {
        for (var i = 0; i < Actions.Count; i++)
            Actions[i].Step = i + 1;
    }

    private void UpdateActionSummary()
    {
        ActionSummary = Actions.Count == 0
            ? "手順はまだありません。下のボタンから追加してください。"
            : $"上から順に {Actions.Count} 手順を実行します";
    }

    partial void OnSelectedMacroChanged(MacroItem? value)
    {
        HasSelection = value is not null || SelectedMacros.Count > 0;
        LoadEditorFrom(value);
    }

    public void LoadEditorFrom(MacroItem? value)
    {
        _loadingEditor = true;
        _suppressDirty = true;
        try
        {
            if (value is null)
            {
                ClearEditor();
                return;
            }

            EditId = value.Id;
            EditName = value.Name;
            EditAlias = value.Alias;
            EditDelaySec = value.DelaySec;
            Actions.Clear();
            foreach (var a in value.Actions)
                Actions.Add(ActionEditItem.FromModel(a));
            UpdateActionSummary();
            ClearDirty();
        }
        finally
        {
            _loadingEditor = false;
            _suppressDirty = false;
        }
    }

    public void SyncMacroSelection(IReadOnlyList<MacroItem> selected)
    {
        SelectedMacros.Clear();
        foreach (var m in selected)
            SelectedMacros.Add(m);

        HasMacroMultiSelection = SelectedMacros.Count > 1;
        HasSelection = SelectedMacro is not null || SelectedMacros.Count > 0;
        DeleteMacroCommand.NotifyCanExecuteChanged();
        CloneMacroCommand.NotifyCanExecuteChanged();
    }

    public void SyncActionSelection(IReadOnlyList<ActionEditItem> selected)
    {
        SelectedActions.Clear();
        foreach (var a in selected)
            SelectedActions.Add(a);

        UpdateActionSelectionFlags();
        SyncActionSelectionHighlight();
    }

    private void UpdateActionSelectionFlags()
    {
        HasActionSelection = SelectedAction is not null || SelectedActions.Count > 0;
        RemoveActionCommand.NotifyCanExecuteChanged();
        MoveActionUpCommand.NotifyCanExecuteChanged();
        MoveActionDownCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Reload()
    {
        try
        {
            var list = ConfigStore.Load();
            Macros.Clear();
            foreach (var m in list)
                Macros.Add(m);

            StatusMessage = AppPaths.IsUsingFallbackDirectory
                ? $"読込完了: {Macros.Count} 件（{ConfigStore.ConfigPath} ※exeフォルダが書けないため LocalAppData を使用）"
                : $"読込完了: {Macros.Count} 件（{ConfigStore.ConfigPath}）";
            SelectedMacro = Macros.FirstOrDefault();
            IsMacroListEmpty = Macros.Count == 0;
            ClearDirty();
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "GUI Load");
            var backup = string.Empty;
            try { backup = AtomicFile.BackupIfExists(ConfigStore.ConfigPath); }
            catch { /* ignore */ }

            Macros.Clear();
            SelectedMacro = null;
            IsMacroListEmpty = true;
            var logHint = FormatLogHint();
            StatusMessage = string.IsNullOrEmpty(backup)
                ? $"設定の読み込みに失敗しました（{ConfigStore.ConfigPath}）。破損ファイルは上書きしていません{logHint}"
                : $"設定の読み込みに失敗しました。バックアップ: {Path.GetFileName(backup)}{logHint}";
            ClearDirty();
        }
    }

    /// <summary>サンプルマクロで一覧を置き換える。既存がある場合は呼び出し側で確認すること。</summary>
    public bool LoadSampleMacros()
    {
        var samples = ConfigStore.LoadSampleMacros();
        if (samples.Count == 0)
        {
            StatusMessage = "サンプルマクロを取得できませんでした";
            return false;
        }

        Macros.Clear();
        foreach (var m in samples)
            Macros.Add(m);

        if (!Persist())
            return false;

        SelectedMacro = Macros.FirstOrDefault();
        IsMacroListEmpty = Macros.Count == 0;
        ClearDirty();
        StatusMessage = $"サンプルを読み込みました: {Macros.Count} 件 → {ConfigStore.ConfigPath}";
        return true;
    }

    public bool TryPrepareMacroSwitch() => !IsDirty;

    [RelayCommand]
    private void NewMacro()
    {
        var item = new MacroItem
        {
            Id = ConfigStore.NextId(Macros),
            Name = "新しいマクロ",
            DelaySec = 3.0,
            Actions = []
        };
        Macros.Add(item);
        if (!Persist())
            return;
        SelectedMacro = item;
        ClearDirty();
        StatusMessage = $"新規作成: ID {item.Id}";
    }

    [RelayCommand(CanExecute = nameof(CanCloneMacro))]
    private void CloneMacro()
    {
        if (SelectedMacro is null) return;
        var copy = SelectedMacro.Clone();
        copy.Id = ConfigStore.NextId(Macros);
        copy.Name = SelectedMacro.Name + " (コピー)";
        copy.Alias = string.Empty;
        Macros.Add(copy);
        if (!Persist())
            return;
        SelectedMacro = copy;
        ClearDirty();
        StatusMessage = $"複製: ID {copy.Id}";
    }

    private bool CanCloneMacro() => SelectedMacro is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDeleteMacros))]
    private void DeleteMacro() => DeleteSelectedMacros();

    private bool CanDeleteMacros() =>
        !IsBusy && (SelectedMacro is not null || SelectedMacros.Count > 0);

    public void DeleteSelectedMacros()
    {
        var targets = SelectedMacros.Count > 0
            ? SelectedMacros.ToList()
            : SelectedMacro is null ? [] : [SelectedMacro];

        if (targets.Count == 0) return;

        foreach (var m in targets)
            Macros.Remove(m);

        if (!Persist())
            return;
        SelectedMacros.Clear();
        SelectedMacro = Macros.FirstOrDefault();
        ClearDirty();
        StatusMessage = targets.Count == 1
            ? $"削除: ID {targets[0].Id}"
            : $"削除: {targets.Count} 件";
    }

    public IReadOnlyList<MacroItem> GetMacrosPendingDelete() =>
        SelectedMacros.Count > 0
            ? SelectedMacros.ToList()
            : SelectedMacro is null ? [] : [SelectedMacro];

    [RelayCommand(CanExecute = nameof(CanEditMacro))]
    private void SaveMacro() => TrySaveMacro();

    private bool CanEditMacro() => SelectedMacro is not null && !IsBusy;

    public bool TrySaveMacro()
    {
        if (SelectedMacro is null) return false;

        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "マクロ名を入力してください";
            return false;
        }

        if (Math.Abs(EditId - Math.Round(EditId)) > 0.001)
        {
            StatusMessage = "ID は整数で入力してください";
            return false;
        }

        var id = (int)Math.Round(EditId);
        if (id < 1)
        {
            StatusMessage = "ID は 1 以上にしてください";
            return false;
        }

        var alias = (EditAlias ?? string.Empty).Trim();
        if (!MacroItem.IsValidAlias(alias, out var aliasError))
        {
            StatusMessage = aliasError;
            return false;
        }

        var originalId = SelectedMacro.Id;
        if (Macros.Any(m => m.Id == id && m.Id != originalId))
        {
            StatusMessage = $"ID {id} は既に使用されています";
            return false;
        }

        if (!string.IsNullOrEmpty(alias) &&
            Macros.Any(m => m.Id != originalId &&
                            string.Equals(m.Alias, alias, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"引数名「{alias}」は既に使用されています";
            return false;
        }

        foreach (var action in Actions)
        {
            if (!action.TryValidate(out var actionError))
            {
                StatusMessage = $"手順 {action.Step}: {actionError}";
                return false;
            }
        }

        SelectedMacro.Id = id;
        SelectedMacro.Name = EditName.Trim();
        SelectedMacro.Alias = alias;
        SelectedMacro.DelaySec = EditDelaySec;
        SelectedMacro.Actions = Actions.Select(a => a.ToModel()).ToList();

        if (!Persist())
            return false;

        // 選択を null に差し替えると ListView の SelectionChanged が走り、
        // 空の手順で再保存されてデータが消えることがあるため、差し替えない。
        ClearDirty();
        StatusMessage = string.IsNullOrEmpty(alias)
            ? $"保存しました → {ConfigStore.ConfigPath}"
            : $"保存しました（引数: -alias {alias} / -{alias}）→ {ConfigStore.ConfigPath}";
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanEditMacro))]
    private void CancelEdit()
    {
        if (SelectedMacro is null) return;
        LoadEditorFrom(SelectedMacro);
        StatusMessage = "編集を破棄しました";
    }

    [RelayCommand(CanExecute = nameof(CanEditMacro))]
    private void AddTextAction() => AddAction("text", string.Empty);

    [RelayCommand(CanExecute = nameof(CanEditMacro))]
    private void AddKeyAction() => AddAction("key", "ENTER");

    [RelayCommand(CanExecute = nameof(CanEditMacro))]
    private void AddHotkeyAction() => AddAction("hotkey", "CTRL+S");

    [RelayCommand(CanExecute = nameof(CanEditMacro))]
    private void AddMouseAction() => AddAction("mouse", "LEFT");

    [RelayCommand(CanExecute = nameof(CanEditMacro))]
    private void AddWaitAction() => AddAction("wait", "0.5");

    [RelayCommand(CanExecute = nameof(CanEditMacro))]
    private void AddDialogAction() => AddAction("dialog", UserDialog.DefaultMessage);

    private void AddAction(string type, string value)
    {
        var item = ActionEditItem.FromModel(new ActionItem { Type = type, Value = value });
        var insertAt = ResolveActionInsertIndex();
        if (insertAt >= Actions.Count)
            Actions.Add(item);
        else
            Actions.Insert(insertAt, item);

        SelectedAction = item;
        SelectedActions.Clear();
        SelectedActions.Add(item);
        SyncActionSelectionHighlight();
    }

    /// <summary>
    /// 選択中のアクションの直後に挿入。未選択／見つからない場合は末尾。
    /// 複数選択時は一覧上でもっとも下の選択の直後。
    /// </summary>
    private int ResolveActionInsertIndex()
    {
        var anchorIndex = -1;

        if (SelectedActions.Count > 0)
        {
            foreach (var selected in SelectedActions)
            {
                var i = Actions.IndexOf(selected);
                if (i > anchorIndex)
                    anchorIndex = i;
            }
        }
        else if (SelectedAction is not null)
        {
            anchorIndex = Actions.IndexOf(SelectedAction);
        }

        return anchorIndex < 0 ? Actions.Count : anchorIndex + 1;
    }

    [RelayCommand(CanExecute = nameof(HasActionSelection))]
    private void RemoveAction() => RemoveSelectedActions();

    public void RemoveSelectedActions()
    {
        var targets = SelectedActions.Count > 0
            ? SelectedActions.ToList()
            : SelectedAction is null ? [] : [SelectedAction];

        if (targets.Count == 0) return;

        foreach (var a in targets)
            Actions.Remove(a);

        SelectedActions.Clear();
        SelectedAction = Actions.LastOrDefault();
        UpdateActionSelectionFlags();
    }

    public IReadOnlyList<ActionEditItem> GetActionsPendingDelete() =>
        SelectedActions.Count > 0
            ? SelectedActions.ToList()
            : SelectedAction is null ? [] : [SelectedAction];

    /// <summary>確認なしで手順を削除（UI側で確認済みのとき呼ぶ）</summary>
    public void RemoveSelectedAction() => RemoveSelectedActions();

    /// <summary>確認なしでマクロ削除（UI側で確認済みのとき呼ぶ）</summary>
    public void DeleteSelectedMacro() => DeleteSelectedMacros();

    [RelayCommand(CanExecute = nameof(CanMoveActionUp))]
    private void MoveActionUp() => MoveAction(-1);

    [RelayCommand(CanExecute = nameof(CanMoveActionDown))]
    private void MoveActionDown() => MoveAction(1);

    private bool CanMoveActionUp() =>
        !IsBusy && SelectedAction is not null && Actions.IndexOf(SelectedAction) > 0;

    private bool CanMoveActionDown()
    {
        if (IsBusy || SelectedAction is null) return false;
        var idx = Actions.IndexOf(SelectedAction);
        return idx >= 0 && idx < Actions.Count - 1;
    }

    private void MoveAction(int delta)
    {
        if (SelectedAction is null) return;
        var idx = Actions.IndexOf(SelectedAction);
        var newIdx = idx + delta;
        if (idx < 0 || newIdx < 0 || newIdx >= Actions.Count) return;
        Actions.Move(idx, newIdx);
        RenumberSteps();
        MoveActionUpCommand.NotifyCanExecuteChanged();
        MoveActionDownCommand.NotifyCanExecuteChanged();
    }

    public MacroItem? BuildCurrentMacroForRun()
    {
        if (SelectedMacro is null) return null;
        return new MacroItem
        {
            Id = (int)Math.Round(EditId),
            Name = EditName.Trim(),
            Alias = (EditAlias ?? string.Empty).Trim(),
            DelaySec = EditDelaySec,
            Actions = Actions.Select(a => a.ToModel()).ToList()
        };
    }

    private void ClearEditor()
    {
        EditId = 1;
        EditName = string.Empty;
        EditAlias = string.Empty;
        EditDelaySec = 0;
        Actions.Clear();
        SelectedAction = null;
        SelectedActions.Clear();
        UpdateActionSummary();
        ClearDirty();
    }

    /// <summary>マクロ一覧の DnD 後に順序を保存</summary>
    public void PersistMacroOrder()
    {
        if (!Persist())
            return;
        StatusMessage = "マクロの順序を保存しました";
    }

    /// <summary>手順 DnD 後に番号を振り直す</summary>
    public void OnActionsReordered()
    {
        UpdateActionSummary();
        StatusMessage = "手順の順序を変更しました（保存で確定）";
        MarkDirty();
    }

    /// <returns>ディスクへの書き込みが成功したら true</returns>
    private bool Persist()
    {
        try
        {
            ConfigStore.Save(Macros);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "保存失敗");
            StatusMessage = $"保存に失敗しました: {ConfigStore.ConfigPath}{FormatLogHint()}";
            return false;
        }
    }

    private static string FormatLogHint()
    {
        return ErrorLogger.LastWrittenPath is { Length: > 0 } path
            ? $"（詳細: {path}）"
            : "（ログを書き込めませんでした。書き込み可能なフォルダへ exe を置き直してください）";
    }

    partial void OnHasSelectionChanged(bool value) => NotifyEditCommands();

    partial void OnIsBusyChanged(bool value)
    {
        IsIdle = !value;
        TestButtonLabel = value ? "中断" : "テスト実行";
        NotifyEditCommands();
        DeleteMacroCommand.NotifyCanExecuteChanged();
        CloneMacroCommand.NotifyCanExecuteChanged();
        RemoveActionCommand.NotifyCanExecuteChanged();
        MoveActionUpCommand.NotifyCanExecuteChanged();
        MoveActionDownCommand.NotifyCanExecuteChanged();
    }

    private void NotifyEditCommands()
    {
        CloneMacroCommand.NotifyCanExecuteChanged();
        DeleteMacroCommand.NotifyCanExecuteChanged();
        SaveMacroCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
        AddTextActionCommand.NotifyCanExecuteChanged();
        AddKeyActionCommand.NotifyCanExecuteChanged();
        AddHotkeyActionCommand.NotifyCanExecuteChanged();
        AddMouseActionCommand.NotifyCanExecuteChanged();
        AddWaitActionCommand.NotifyCanExecuteChanged();
        AddDialogActionCommand.NotifyCanExecuteChanged();
    }
}
