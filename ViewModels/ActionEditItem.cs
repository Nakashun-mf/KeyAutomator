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
    public static IReadOnlyList<SpecialKeyOption> All { get; } = Build();

    private static IReadOnlyList<SpecialKeyOption> Build()
    {
        var list = new List<SpecialKeyOption>();

        void Add(string code, string label) => list.Add(new SpecialKeyOption { Code = code, Label = label });

        // 編集・移動
        Add("ENTER", "Enter（確定）");
        Add("TAB", "Tab（次へ）");
        Add("ESC", "Esc（取消）");
        Add("SPACE", "Space（空白）");
        Add("BACKSPACE", "Backspace（1文字削除）");
        Add("DELETE", "Delete（削除）");
        Add("INSERT", "Insert");
        Add("HOME", "Home");
        Add("END", "End");
        Add("PAGEUP", "Page Up");
        Add("PAGEDOWN", "Page Down");
        Add("UP", "↑ 上");
        Add("DOWN", "↓ 下");
        Add("LEFT", "← 左");
        Add("RIGHT", "→ 右");

        // 修飾キー単体
        Add("CTRL", "Ctrl");
        Add("SHIFT", "Shift");
        Add("ALT", "Alt");
        Add("LWIN", "Windows（左）");
        Add("RWIN", "Windows（右）");
        Add("APPS", "Menu（アプリケーション）");
        Add("CAPITAL", "Caps Lock");
        Add("NUMLOCK", "Num Lock");
        Add("SCROLL", "Scroll Lock");
        Add("SNAPSHOT", "Print Screen");
        Add("PAUSE", "Pause");

        // 文字 A-Z
        for (var c = 'A'; c <= 'Z'; c++)
            Add(c.ToString(), $"{c}");

        // 数字 0-9
        for (var d = 0; d <= 9; d++)
            Add(d.ToString(), $"{d}");

        // ファンクション
        for (var f = 1; f <= 24; f++)
            Add($"F{f}", $"F{f}");

        // テンキー
        for (var n = 0; n <= 9; n++)
            Add($"NUMPAD{n}", $"テンキー {n}");
        Add("MULTIPLY", "テンキー *");
        Add("ADD", "テンキー +");
        Add("SUBTRACT", "テンキー -");
        Add("DECIMAL", "テンキー .");
        Add("DIVIDE", "テンキー /");
        Add("SEPARATOR", "テンキー Separator");

        // 記号（OEM）
        Add("OEM_PLUS", "= +");
        Add("OEM_COMMA", ", <");
        Add("OEM_MINUS", "- _");
        Add("OEM_PERIOD", ". >");
        Add("OEM_1", "; :");
        Add("OEM_2", "/ ?");
        Add("OEM_3", "` ~");
        Add("OEM_4", "[ {");
        Add("OEM_5", "\\ |");
        Add("OEM_6", "] }");
        Add("OEM_7", "' \"");
        Add("OEM_102", "OEM 102（\\ | など）");

        // ブラウザ / メディア
        Add("BROWSER_BACK", "ブラウザ 戻る");
        Add("BROWSER_FORWARD", "ブラウザ 進む");
        Add("BROWSER_REFRESH", "ブラウザ 更新");
        Add("BROWSER_STOP", "ブラウザ 停止");
        Add("BROWSER_SEARCH", "ブラウザ 検索");
        Add("BROWSER_FAVORITES", "ブラウザ お気に入り");
        Add("BROWSER_HOME", "ブラウザ ホーム");
        Add("VOLUME_MUTE", "音量ミュート");
        Add("VOLUME_DOWN", "音量ダウン");
        Add("VOLUME_UP", "音量アップ");
        Add("MEDIA_NEXT_TRACK", "次の曲");
        Add("MEDIA_PREV_TRACK", "前の曲");
        Add("MEDIA_STOP", "メディア停止");
        Add("MEDIA_PLAY_PAUSE", "再生/一時停止");
        Add("LAUNCH_MAIL", "メール起動");
        Add("LAUNCH_MEDIA_SELECT", "メディア選択");
        Add("LAUNCH_APP1", "アプリ1");
        Add("LAUNCH_APP2", "アプリ2");

        return list;
    }

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RETURN"] = "ENTER",
        ["ESCAPE"] = "ESC",
        ["BS"] = "BACKSPACE",
        ["DEL"] = "DELETE",
        ["INS"] = "INSERT",
        ["PGUP"] = "PAGEUP",
        ["PGDN"] = "PAGEDOWN",
        ["CONTROL"] = "CTRL",
        ["CTL"] = "CTRL",
        ["WINDOWS"] = "LWIN",
        ["WIN"] = "LWIN",
        ["CAPSLOCK"] = "CAPITAL",
        ["CAPS"] = "CAPITAL",
        ["SCROLLLOCK"] = "SCROLL",
        ["PRINTSCREEN"] = "SNAPSHOT",
        ["PRTSC"] = "SNAPSHOT",
        ["MULTIPLY_KEY"] = "MULTIPLY",
        ["ADD_KEY"] = "ADD",
        ["SUBTRACT_KEY"] = "SUBTRACT",
        ["DECIMAL_KEY"] = "DECIMAL",
        ["DIVIDE_KEY"] = "DIVIDE"
    };

    public static SpecialKeyOption Get(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return All[0];

        var normalized = code.Trim();
        if (Aliases.TryGetValue(normalized, out var alias))
            normalized = alias;

        return All.FirstOrDefault(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase))
               ?? All[0];
    }

    public static bool Contains(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var normalized = code.Trim();
        if (Aliases.TryGetValue(normalized, out var alias))
            normalized = alias;
        return All.Any(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }
}

public partial class ActionEditItem : ObservableObject
{
    [ObservableProperty] private int _step = 1;
    [ObservableProperty] private string _type = "text";
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private bool _isSelected;

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
