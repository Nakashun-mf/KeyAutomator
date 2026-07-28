using CommunityToolkit.Mvvm.ComponentModel;
using KeyAutomator.Models;

namespace KeyAutomator.ViewModels;

public sealed class ActionTypeOption
{
    public required string Code { get; init; }
    public required string Label { get; init; }
    public required string Hint { get; init; }
    public required string Placeholder { get; init; }

    public override string ToString() => Label;
}

public sealed class SpecialKeyOption
{
    public required string Code { get; init; }
    public required string Label { get; init; }

    public override string ToString() => Label;
}

public static class ActionTypeCatalog
{
    public static IReadOnlyList<ActionTypeOption> All { get; } =
    [
        new()
        {
            Code = "text",
            Label = "テキスト入力",
            Hint = "文字列をそのまま入力します（日本語・記号OK）",
            Placeholder = "例: user_admin"
        },
        new()
        {
            Code = "key",
            Label = "特殊キー",
            Hint = "一覧からキーを選んでください",
            Placeholder = ""
        },
        new()
        {
            Code = "hotkey",
            Label = "ショートカット",
            Hint = "Ctrl+S のような組み合わせキーです（+ でつなぐ）",
            Placeholder = "例: CTRL+S / CTRL+SHIFT+A"
        },
        new()
        {
            Code = "wait",
            Label = "待機",
            Hint = "次の操作まで待ちます（秒・小数可）",
            Placeholder = "例: 0.5"
        }
    ];

    public static ActionTypeOption Get(string? code) =>
        All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? All[0];
}

public static class SpecialKeyCatalog
{
    public static IReadOnlyList<SpecialKeyOption> All { get; } =
    [
        new() { Code = "ENTER", Label = "Enter（確定）" },
        new() { Code = "TAB", Label = "Tab（次へ）" },
        new() { Code = "ESC", Label = "Esc（取消）" },
        new() { Code = "SPACE", Label = "Space（空白）" },
        new() { Code = "BACKSPACE", Label = "Backspace（削除）" },
        new() { Code = "DELETE", Label = "Delete" },
        new() { Code = "INSERT", Label = "Insert" },
        new() { Code = "HOME", Label = "Home" },
        new() { Code = "END", Label = "End" },
        new() { Code = "PAGEUP", Label = "Page Up" },
        new() { Code = "PAGEDOWN", Label = "Page Down" },
        new() { Code = "UP", Label = "↑ 上" },
        new() { Code = "DOWN", Label = "↓ 下" },
        new() { Code = "LEFT", Label = "← 左" },
        new() { Code = "RIGHT", Label = "→ 右" },
        new() { Code = "F1", Label = "F1" },
        new() { Code = "F2", Label = "F2" },
        new() { Code = "F3", Label = "F3" },
        new() { Code = "F4", Label = "F4" },
        new() { Code = "F5", Label = "F5" },
        new() { Code = "F6", Label = "F6" },
        new() { Code = "F7", Label = "F7" },
        new() { Code = "F8", Label = "F8" },
        new() { Code = "F9", Label = "F9" },
        new() { Code = "F10", Label = "F10" },
        new() { Code = "F11", Label = "F11" },
        new() { Code = "F12", Label = "F12" }
    ];

    public static SpecialKeyOption Get(string? code)
    {
        var found = All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        if (found is not null) return found;

        // 別名の吸収
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "RETURN" => All[0],
            "ESCAPE" => All.First(x => x.Code == "ESC"),
            "BS" => All.First(x => x.Code == "BACKSPACE"),
            "DEL" => All.First(x => x.Code == "DELETE"),
            "INS" => All.First(x => x.Code == "INSERT"),
            "PGUP" => All.First(x => x.Code == "PAGEUP"),
            "PGDN" => All.First(x => x.Code == "PAGEDOWN"),
            _ => All[0]
        };
    }

    public static bool Contains(string? code) =>
        All.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
}

public partial class ActionEditItem : ObservableObject
{
    [ObservableProperty] private int _step = 1;
    [ObservableProperty] private string _type = "text";
    [ObservableProperty] private string _value = string.Empty;

    public IReadOnlyList<ActionTypeOption> TypeOptions => ActionTypeCatalog.All;
    public IReadOnlyList<SpecialKeyOption> SpecialKeyOptions => SpecialKeyCatalog.All;

    public bool IsKeyType => string.Equals(Type, "key", StringComparison.OrdinalIgnoreCase);

    public ActionTypeOption SelectedTypeOption
    {
        get => ActionTypeCatalog.Get(Type);
        set
        {
            if (value is null || Type == value.Code) return;
            Type = value.Code;
        }
    }

    public SpecialKeyOption SelectedSpecialKey
    {
        get => SpecialKeyCatalog.Get(Value);
        set
        {
            if (value is null) return;
            if (string.Equals(Value, value.Code, StringComparison.OrdinalIgnoreCase)) return;
            Value = value.Code;
            OnPropertyChanged(nameof(SelectedSpecialKey));
        }
    }

    public string TypeLabel => ActionTypeCatalog.Get(Type).Label;
    public string Hint => ActionTypeCatalog.Get(Type).Hint;
    public string Placeholder => ActionTypeCatalog.Get(Type).Placeholder;

    partial void OnTypeChanged(string value)
    {
        if (string.Equals(value, "key", StringComparison.OrdinalIgnoreCase))
        {
            // 未設定・不正値は Enter に正規化
            if (!SpecialKeyCatalog.Contains(Value))
                Value = "ENTER";
        }

        OnPropertyChanged(nameof(SelectedTypeOption));
        OnPropertyChanged(nameof(IsKeyType));
        OnPropertyChanged(nameof(SelectedSpecialKey));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Placeholder));
    }

    partial void OnValueChanged(string value)
    {
        if (IsKeyType)
            OnPropertyChanged(nameof(SelectedSpecialKey));
    }

    public ActionItem ToModel() => new() { Type = Type, Value = Value };

    public static ActionEditItem FromModel(ActionItem a)
    {
        var type = string.IsNullOrWhiteSpace(a.Type) ? "text" : a.Type;
        var value = a.Value ?? string.Empty;
        if (string.Equals(type, "key", StringComparison.OrdinalIgnoreCase))
            value = SpecialKeyCatalog.Get(value).Code;

        return new ActionEditItem
        {
            Type = type,
            Value = value
        };
    }
}
