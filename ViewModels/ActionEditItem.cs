using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

public sealed class MouseActionOption
{
    public required string Code { get; init; }
    public required string Label { get; init; }

    public override string ToString() => Label;
}

public static class MouseActionCatalog
{
    public static IReadOnlyList<MouseActionOption> All { get; } =
    [
        new() { Code = "LEFT", Label = "左クリック" },
        new() { Code = "RIGHT", Label = "右クリック" },
        new() { Code = "MIDDLE", Label = "中クリック" },
        new() { Code = "LEFT_DOUBLE", Label = "左ダブルクリック" }
    ];

    public static MouseActionOption Get(string? code) =>
        All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? All[0];

    public static bool Contains(string? code) =>
        All.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
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
            Hint = "一覧からキーを1つ選んでください",
            Placeholder = ""
        },
        new()
        {
            Code = "hotkey",
            Label = "ショートカット",
            Hint = "同時押しするキーをプルダウンで追加してください",
            Placeholder = ""
        },
        new()
        {
            Code = "mouse",
            Label = "マウスクリック",
            Hint = "現在のマウス位置でクリックします（右クリックなど）",
            Placeholder = ""
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

/// <summary>実用的なキー候補（特殊キー／ショートカット共通）</summary>
public static class SpecialKeyCatalog
{
    public static IReadOnlyList<SpecialKeyOption> All { get; } = Build();

    private static IReadOnlyList<SpecialKeyOption> Build()
    {
        var list = new List<SpecialKeyOption>();
        void Add(string code, string label) => list.Add(new SpecialKeyOption { Code = code, Label = label });

        Add("CTRL", "Ctrl");
        Add("SHIFT", "Shift");
        Add("ALT", "Alt");
        Add("LWIN", "Windows");

        Add("ENTER", "Enter");
        Add("TAB", "Tab");
        Add("ESC", "Esc");
        Add("SPACE", "Space");
        Add("BACKSPACE", "Backspace");
        Add("DELETE", "Delete");
        Add("INSERT", "Insert");
        Add("HOME", "Home");
        Add("END", "End");
        Add("PAGEUP", "Page Up");
        Add("PAGEDOWN", "Page Down");
        Add("UP", "↑");
        Add("DOWN", "↓");
        Add("LEFT", "←");
        Add("RIGHT", "→");

        for (var c = 'A'; c <= 'Z'; c++)
            Add(c.ToString(), c.ToString());

        for (var d = 0; d <= 9; d++)
            Add(d.ToString(), d.ToString());

        for (var f = 1; f <= 12; f++)
            Add($"F{f}", $"F{f}");

        for (var n = 0; n <= 9; n++)
            Add($"NUMPAD{n}", $"テンキー {n}");
        Add("MULTIPLY", "テンキー *");
        Add("ADD", "テンキー +");
        Add("SUBTRACT", "テンキー -");
        Add("DECIMAL", "テンキー .");
        Add("DIVIDE", "テンキー /");

        Add("OEM_PLUS", "= +");
        Add("OEM_MINUS", "- _");
        Add("OEM_COMMA", ",");
        Add("OEM_PERIOD", ".");
        Add("OEM_1", "; :");
        Add("OEM_2", "/ ?");
        Add("OEM_3", "` ~");
        Add("OEM_4", "[ {");
        Add("OEM_5", "\\ |");
        Add("OEM_6", "] }");
        Add("OEM_7", "' \"");

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
        ["RWIN"] = "LWIN"
    };

    public static SpecialKeyOption Get(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return All[0];

        var normalized = Normalize(code);
        return All.FirstOrDefault(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase))
               ?? All.First(x => x.Code == "ENTER");
    }

    public static bool Contains(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var normalized = Normalize(code);
        return All.Any(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string code)
    {
        var normalized = code.Trim();
        return Aliases.TryGetValue(normalized, out var alias) ? alias : normalized.ToUpperInvariant();
    }
}

public partial class HotkeyPartEditItem : ObservableObject
{
    private readonly ActionEditItem _owner;

    public HotkeyPartEditItem(ActionEditItem owner, string code)
    {
        _owner = owner;
        _code = SpecialKeyCatalog.Normalize(code);
    }

    [ObservableProperty] private string _code = "CTRL";

    public IReadOnlyList<SpecialKeyOption> KeyOptions => SpecialKeyCatalog.All;

    public SpecialKeyOption SelectedOption
    {
        get => SpecialKeyCatalog.Get(Code);
        set
        {
            if (value is null) return;
            if (string.Equals(Code, value.Code, StringComparison.OrdinalIgnoreCase)) return;
            Code = value.Code;
        }
    }

    partial void OnCodeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedOption));
        _owner.SyncHotkeyValueFromParts();
    }

    [RelayCommand]
    private void Remove() => _owner.RemoveHotkeyPart(this);
}

public partial class ActionEditItem : ObservableObject
{
    private bool _syncingHotkey;

    [ObservableProperty] private int _step = 1;
    [ObservableProperty] private string _type = "text";
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private bool _isSelected;

    public ObservableCollection<HotkeyPartEditItem> HotkeyParts { get; } = [];

    public IReadOnlyList<ActionTypeOption> TypeOptions => ActionTypeCatalog.All;
    public IReadOnlyList<SpecialKeyOption> SpecialKeyOptions => SpecialKeyCatalog.All;
    public IReadOnlyList<MouseActionOption> MouseActionOptions => MouseActionCatalog.All;

    public bool IsKeyType => string.Equals(Type, "key", StringComparison.OrdinalIgnoreCase);
    public bool IsHotkeyType => string.Equals(Type, "hotkey", StringComparison.OrdinalIgnoreCase);
    public bool IsMouseType => string.Equals(Type, "mouse", StringComparison.OrdinalIgnoreCase);
    public bool IsFreeTextType => !IsKeyType && !IsHotkeyType && !IsMouseType;

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

    public MouseActionOption SelectedMouseAction
    {
        get => MouseActionCatalog.Get(Value);
        set
        {
            if (value is null) return;
            if (string.Equals(Value, value.Code, StringComparison.OrdinalIgnoreCase)) return;
            Value = value.Code;
            OnPropertyChanged(nameof(SelectedMouseAction));
        }
    }

    public string TypeLabel => ActionTypeCatalog.Get(Type).Label;
    public string Hint => ActionTypeCatalog.Get(Type).Hint;
    public string Placeholder => ActionTypeCatalog.Get(Type).Placeholder;

    public ActionEditItem()
    {
        HotkeyParts.CollectionChanged += OnHotkeyPartsChanged;
    }

    private void OnHotkeyPartsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SyncHotkeyValueFromParts();

    partial void OnValueChanged(string value)
    {
        if (IsKeyType)
            OnPropertyChanged(nameof(SelectedSpecialKey));
        if (IsMouseType)
            OnPropertyChanged(nameof(SelectedMouseAction));
    }

    public void SyncHotkeyValueFromParts()
    {
        if (_syncingHotkey) return;

        var codes = HotkeyParts
            .Select(p => SpecialKeyCatalog.Normalize(p.Code))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        var joined = codes.Count == 0 ? "CTRL+S" : string.Join("+", codes);
        if (!string.Equals(Value, joined, StringComparison.Ordinal))
            Value = joined;
    }

    public void LoadHotkeyPartsFromValue(string hotkey)
    {
        _syncingHotkey = true;
        try
        {
            HotkeyParts.Clear();
            var parts = hotkey.Split(['+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                parts = ["CTRL", "S"];

            foreach (var part in parts)
                HotkeyParts.Add(new HotkeyPartEditItem(this, part));

            Value = string.Join("+", HotkeyParts.Select(p => SpecialKeyCatalog.Normalize(p.Code)));
        }
        finally
        {
            _syncingHotkey = false;
        }
    }

    partial void OnTypeChanged(string value)
    {
        if (string.Equals(value, "key", StringComparison.OrdinalIgnoreCase))
        {
            if (!SpecialKeyCatalog.Contains(Value))
                Value = "ENTER";
        }
        else if (string.Equals(value, "hotkey", StringComparison.OrdinalIgnoreCase))
        {
            if (HotkeyParts.Count == 0)
                LoadHotkeyPartsFromValue(string.IsNullOrWhiteSpace(Value) ? "CTRL+S" : Value);
            else
                SyncHotkeyValueFromParts();
        }
        else if (string.Equals(value, "mouse", StringComparison.OrdinalIgnoreCase))
        {
            if (!MouseActionCatalog.Contains(Value))
                Value = "RIGHT";
        }

        OnPropertyChanged(nameof(SelectedTypeOption));
        OnPropertyChanged(nameof(IsKeyType));
        OnPropertyChanged(nameof(IsHotkeyType));
        OnPropertyChanged(nameof(IsMouseType));
        OnPropertyChanged(nameof(IsFreeTextType));
        OnPropertyChanged(nameof(SelectedSpecialKey));
        OnPropertyChanged(nameof(SelectedMouseAction));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(Placeholder));
    }

    [RelayCommand]
    private void AddHotkeyPart()
    {
        if (!IsHotkeyType) return;
        HotkeyParts.Add(new HotkeyPartEditItem(this, "A"));
    }

    public void RemoveHotkeyPart(HotkeyPartEditItem part)
    {
        if (HotkeyParts.Count <= 1) return;
        HotkeyParts.Remove(part);
    }

    public ActionItem ToModel()
    {
        if (IsHotkeyType)
            SyncHotkeyValueFromParts();
        return new ActionItem { Type = Type, Value = Value };
    }

    public static ActionEditItem FromModel(ActionItem a)
    {
        var type = string.IsNullOrWhiteSpace(a.Type) ? "text" : a.Type;
        var value = a.Value ?? string.Empty;
        var item = new ActionEditItem();

        if (string.Equals(type, "key", StringComparison.OrdinalIgnoreCase))
        {
            item.Type = "key";
            item.Value = SpecialKeyCatalog.Get(value).Code;
        }
        else if (string.Equals(type, "hotkey", StringComparison.OrdinalIgnoreCase))
        {
            item.LoadHotkeyPartsFromValue(string.IsNullOrWhiteSpace(value) ? "CTRL+S" : value);
            item.Type = "hotkey";
        }
        else if (string.Equals(type, "mouse", StringComparison.OrdinalIgnoreCase))
        {
            item.Type = "mouse";
            item.Value = MouseActionCatalog.Get(value).Code;
        }
        else
        {
            item.Type = type;
            item.Value = value;
        }

        return item;
    }
}
