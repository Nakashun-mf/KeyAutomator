using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.ViewModels;

public partial class ActionEditItem : ObservableObject
{
    [ObservableProperty] private string _type = "text";
    [ObservableProperty] private string _value = string.Empty;

    public ActionItem ToModel() => new() { Type = Type, Value = Value };

    public static ActionEditItem FromModel(ActionItem a) => new()
    {
        Type = a.Type,
        Value = a.Value
    };
}

public partial class MainViewModel : ObservableObject
{
    public static IReadOnlyList<string> ActionTypes { get; } = ["text", "key", "hotkey", "wait"];

    public ObservableCollection<MacroItem> Macros { get; } = [];
    public ObservableCollection<ActionEditItem> Actions { get; } = [];

    [ObservableProperty] private MacroItem? _selectedMacro;
    [ObservableProperty] private ActionEditItem? _selectedAction;
    [ObservableProperty] private double _editId = 1;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private double _editDelaySec;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _statusMessage = "準備完了";
    [ObservableProperty] private bool _isBusy;

    public MainViewModel()
    {
        Reload();
    }

    partial void OnSelectedMacroChanged(MacroItem? value)
    {
        HasSelection = value is not null;
        if (value is null)
        {
            ClearEditor();
            return;
        }

        EditId = value.Id;
        EditName = value.Name;
        EditDelaySec = value.DelaySec;
        Actions.Clear();
        foreach (var a in value.Actions)
            Actions.Add(ActionEditItem.FromModel(a));
    }

    [RelayCommand]
    private void Reload()
    {
        try
        {
            var list = ConfigStore.Load().OrderBy(m => m.Id).ToList();
            Macros.Clear();
            foreach (var m in list)
                Macros.Add(m);

            StatusMessage = $"読込完了: {Macros.Count} 件";
            SelectedMacro = Macros.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "GUI Load");
            Macros.Clear();
            try { ConfigStore.Save([]); } catch { /* ignore */ }
            StatusMessage = "設定の読み込みに失敗したため空で開始しました";
        }
    }

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
        Persist();
        SelectedMacro = item;
        StatusMessage = $"新規作成: ID {item.Id}";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CloneMacro()
    {
        if (SelectedMacro is null) return;
        var copy = SelectedMacro.Clone();
        copy.Id = ConfigStore.NextId(Macros);
        copy.Name = SelectedMacro.Name + " (コピー)";
        Macros.Add(copy);
        Persist();
        SelectedMacro = copy;
        StatusMessage = $"複製: ID {copy.Id}";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteMacro()
    {
        if (SelectedMacro is null) return;
        var id = SelectedMacro.Id;
        Macros.Remove(SelectedMacro);
        Persist();
        SelectedMacro = Macros.FirstOrDefault();
        StatusMessage = $"削除: ID {id}";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SaveMacro()
    {
        if (SelectedMacro is null) return;

        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "マクロ名を入力してください";
            return;
        }

        var originalId = SelectedMacro.Id;
        if (Macros.Any(m => m.Id == (int)EditId && m.Id != originalId))
        {
            StatusMessage = $"ID {(int)EditId} は既に使用されています";
            return;
        }

        SelectedMacro.Id = (int)EditId;
        SelectedMacro.Name = EditName.Trim();
        SelectedMacro.DelaySec = EditDelaySec;
        SelectedMacro.Actions = Actions.Select(a => a.ToModel()).ToList();

        Persist();
        // UI 更新のため再選択
        var saved = SelectedMacro;
        SelectedMacro = null;
        SelectedMacro = saved;
        StatusMessage = "保存しました";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CancelEdit()
    {
        if (SelectedMacro is null) return;
        var current = SelectedMacro;
        SelectedMacro = null;
        SelectedMacro = current;
        StatusMessage = "編集を破棄しました";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AddTextAction() => AddAction("text", string.Empty);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AddKeyAction() => AddAction("key", "ENTER");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AddWaitAction() => AddAction("wait", "0.5");

    public void AddHotkeyAction(string hotkey)
    {
        if (!HasSelection || string.IsNullOrWhiteSpace(hotkey)) return;
        AddAction("hotkey", hotkey);
    }

    private void AddAction(string type, string value)
    {
        var item = new ActionEditItem { Type = type, Value = value };
        Actions.Add(item);
        SelectedAction = item;
    }

    [RelayCommand]
    private void RemoveAction()
    {
        if (SelectedAction is null) return;
        Actions.Remove(SelectedAction);
        SelectedAction = Actions.LastOrDefault();
    }

    [RelayCommand]
    private void MoveActionUp() => MoveAction(-1);

    [RelayCommand]
    private void MoveActionDown() => MoveAction(1);

    private void MoveAction(int delta)
    {
        if (SelectedAction is null) return;
        var idx = Actions.IndexOf(SelectedAction);
        var newIdx = idx + delta;
        if (idx < 0 || newIdx < 0 || newIdx >= Actions.Count) return;
        Actions.Move(idx, newIdx);
    }

    public MacroItem? BuildCurrentMacroForRun()
    {
        if (SelectedMacro is null) return null;
        return new MacroItem
        {
            Id = (int)EditId,
            Name = EditName.Trim(),
            DelaySec = EditDelaySec,
            Actions = Actions.Select(a => a.ToModel()).ToList()
        };
    }

    private void ClearEditor()
    {
        EditId = 1;
        EditName = string.Empty;
        EditDelaySec = 0;
        Actions.Clear();
        SelectedAction = null;
    }

    private void Persist()
    {
        try
        {
            ConfigStore.Save(Macros);
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "保存失敗");
            StatusMessage = "保存に失敗しました";
        }
    }

    partial void OnHasSelectionChanged(bool value)
    {
        CloneMacroCommand.NotifyCanExecuteChanged();
        DeleteMacroCommand.NotifyCanExecuteChanged();
        SaveMacroCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
        AddTextActionCommand.NotifyCanExecuteChanged();
        AddKeyActionCommand.NotifyCanExecuteChanged();
        AddWaitActionCommand.NotifyCanExecuteChanged();
    }
}
