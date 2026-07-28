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
            Hint = "Enter / Tab / Esc など、1つのキーを押します",
            Placeholder = "例: ENTER / TAB / ESC"
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

public partial class ActionEditItem : ObservableObject
{
    [ObservableProperty] private int _step = 1;
    [ObservableProperty] private string _type = "text";
    [ObservableProperty] private string _value = string.Empty;

    public IReadOnlyList<ActionTypeOption> TypeOptions => ActionTypeCatalog.All;

    public ActionTypeOption SelectedTypeOption
    {
        get => ActionTypeCatalog.Get(Type);
        set
        {
            if (value is null || Type == value.Code) return;
            Type = value.Code;
        }
    }

    public string TypeLabel => ActionTypeCatalog.Get(Type).Label;
    public string Hint => ActionTypeCatalog.Get(Type).Hint;
    public string Placeholder => ActionTypeCatalog.Get(Type).Placeholder;

    partial void OnTypeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedTypeOption));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Placeholder));
    }

    public ActionItem ToModel() => new() { Type = Type, Value = Value };

    public static ActionEditItem FromModel(ActionItem a) => new()
    {
        Type = string.IsNullOrWhiteSpace(a.Type) ? "text" : a.Type,
        Value = a.Value ?? string.Empty
    };
}
