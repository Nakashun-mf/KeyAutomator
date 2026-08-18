using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.ViewModels;

public sealed class ActionTypeOption
{
    public required string Code { get; init; }
    public required string LabelKey { get; init; }
    public required string HintKey { get; init; }
    public string PlaceholderKey { get; init; } = string.Empty;

    public string Label => Loc.Get(LabelKey);
    public string Hint => Loc.Get(HintKey);
    public string Placeholder =>
        PlaceholderKey.Length == 0 ? string.Empty : Loc.Get(PlaceholderKey);

    public override string ToString() => Label;
}

public sealed class SpecialKeyOption
{
    public required string Code { get; init; }
    public required string FixedLabel { get; init; }
    public string? LabelKey { get; init; }
    public string? LabelArg { get; init; }

    public string Label =>
        LabelKey is null
            ? FixedLabel
            : LabelArg is null
                ? Loc.Get(LabelKey)
                : Loc.Format(LabelKey, LabelArg);

    public override string ToString() => Label;
}

public sealed class MouseActionOption
{
    public required string Code { get; init; }
    public required string LabelKey { get; init; }

    public string Label => Loc.Get(LabelKey);

    public override string ToString() => Label;
}

public static class MouseActionCatalog
{
    public static IReadOnlyList<MouseActionOption> All { get; } =
    [
        new() { Code = "LEFT", LabelKey = "Mouse_LEFT" },
        new() { Code = "RIGHT", LabelKey = "Mouse_RIGHT" },
        new() { Code = "MIDDLE", LabelKey = "Mouse_MIDDLE" },
        new() { Code = "LEFT_DOUBLE", LabelKey = "Mouse_LEFT_DOUBLE" }
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
            LabelKey = "ActionType_text",
            HintKey = "ActionHint_text",
            PlaceholderKey = "ActionPlaceholder_text"
        },
        new()
        {
            Code = "key",
            LabelKey = "ActionType_key",
            HintKey = "ActionHint_key"
        },
        new()
        {
            Code = "hotkey",
            LabelKey = "ActionType_hotkey",
            HintKey = "ActionHint_hotkey"
        },
        new()
        {
            Code = "mouse",
            LabelKey = "ActionType_mouse",
            HintKey = "ActionHint_mouse"
        },
        new()
        {
            Code = "wait",
            LabelKey = "ActionType_wait",
            HintKey = "ActionHint_wait",
            PlaceholderKey = "ActionPlaceholder_wait"
        },
        new()
        {
            Code = "dialog",
            LabelKey = "ActionType_dialog",
            HintKey = "ActionHint_dialog",
            PlaceholderKey = "ActionPlaceholder_dialog"
        },
        new()
        {
            Code = RepeatBlock.StartType,
            LabelKey = "ActionType_repeat",
            HintKey = "ActionHint_repeat",
            PlaceholderKey = "ActionPlaceholder_repeat"
        },
        new()
        {
            Code = RepeatBlock.EndType,
            LabelKey = "ActionType_end_repeat",
            HintKey = "ActionHint_end_repeat"
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
        void Add(string code, string label) =>
            list.Add(new SpecialKeyOption { Code = code, FixedLabel = label });

        void AddLoc(string code, string key, string arg) =>
            list.Add(new SpecialKeyOption { Code = code, FixedLabel = arg, LabelKey = key, LabelArg = arg });

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
            AddLoc($"NUMPAD{n}", "Key_NumpadDigit", n.ToString(CultureInfo.InvariantCulture));
        AddLoc("MULTIPLY", "Key_NumpadSymbol", "*");
        AddLoc("ADD", "Key_NumpadSymbol", "+");
        AddLoc("SUBTRACT", "Key_NumpadSymbol", "-");
        AddLoc("DECIMAL", "Key_NumpadSymbol", ".");
        AddLoc("DIVIDE", "Key_NumpadSymbol", "/");

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
    private const double IndentPerLevel = 18;

    [ObservableProperty] private int _step = 1;
    [ObservableProperty] private string _type = "text";
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _depth;

    public ObservableCollection<HotkeyPartEditItem> HotkeyParts { get; } = [];

    public IReadOnlyList<ActionTypeOption> TypeOptions => ActionTypeCatalog.All;
    public IReadOnlyList<SpecialKeyOption> SpecialKeyOptions => SpecialKeyCatalog.All;
    public IReadOnlyList<MouseActionOption> MouseActionOptions => MouseActionCatalog.All;

    public bool IsKeyType => string.Equals(Type, "key", StringComparison.OrdinalIgnoreCase);
    public bool IsHotkeyType => string.Equals(Type, "hotkey", StringComparison.OrdinalIgnoreCase);
    public bool IsMouseType => string.Equals(Type, "mouse", StringComparison.OrdinalIgnoreCase);
    public bool IsRepeatType => RepeatBlock.IsStart(Type);
    public bool IsEndRepeatType => RepeatBlock.IsEnd(Type);
    public bool IsLoopMarker => IsRepeatType || IsEndRepeatType;
    public bool IsFreeTextType =>
        !IsKeyType && !IsHotkeyType && !IsMouseType && !IsRepeatType && !IsEndRepeatType;

    /// <summary>ネスト表示用の左余白（px）。</summary>
    public double IndentWidth => Depth * IndentPerLevel;

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

    public double RepeatCount
    {
        get => RepeatBlock.TryParseCount(Value, out var n) ? n : RepeatBlock.DefaultCount;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return;

            var n = (int)Math.Clamp(Math.Round(value), 1, RepeatBlock.MaxCount);
            var text = n.ToString(CultureInfo.InvariantCulture);
            if (string.Equals(Value, text, StringComparison.Ordinal))
                return;
            Value = text;
            OnPropertyChanged(nameof(RepeatCount));
        }
    }

    public ActionEditItem()
    {
        HotkeyParts.CollectionChanged += OnHotkeyPartsChanged;
    }

    private void OnHotkeyPartsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SyncHotkeyValueFromParts();

    partial void OnDepthChanged(int value) => OnPropertyChanged(nameof(IndentWidth));

    partial void OnValueChanged(string value)
    {
        if (IsKeyType)
            OnPropertyChanged(nameof(SelectedSpecialKey));
        if (IsMouseType)
            OnPropertyChanged(nameof(SelectedMouseAction));
        if (IsRepeatType)
            OnPropertyChanged(nameof(RepeatCount));
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
                Value = "LEFT";
        }
        else if (string.Equals(value, "dialog", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Value) ||
                double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ||
                double.TryParse(Value, out _))
            {
                Value = UserDialog.DefaultMessage;
            }
        }
        else if (RepeatBlock.IsStart(value))
        {
            if (!RepeatBlock.TryParseCount(Value, out _))
                Value = RepeatBlock.DefaultCount.ToString(CultureInfo.InvariantCulture);
        }
        else if (RepeatBlock.IsEnd(value))
        {
            Value = string.Empty;
        }

        OnPropertyChanged(nameof(SelectedTypeOption));
        OnPropertyChanged(nameof(IsKeyType));
        OnPropertyChanged(nameof(IsHotkeyType));
        OnPropertyChanged(nameof(IsMouseType));
        OnPropertyChanged(nameof(IsRepeatType));
        OnPropertyChanged(nameof(IsEndRepeatType));
        OnPropertyChanged(nameof(IsLoopMarker));
        OnPropertyChanged(nameof(IsFreeTextType));
        OnPropertyChanged(nameof(SelectedSpecialKey));
        OnPropertyChanged(nameof(SelectedMouseAction));
        OnPropertyChanged(nameof(RepeatCount));
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

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (IsHotkeyType)
        {
            SyncHotkeyValueFromParts();
            if (!IsValidHotkey(Value, out error))
                return false;
        }
        else if (string.Equals(Type, "wait", StringComparison.OrdinalIgnoreCase))
        {
            if (!double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                !double.TryParse(Value, out _))
            {
                error = Loc.Get("Error_WaitNumeric");
                return false;
            }
        }
        else if (IsRepeatType)
        {
            if (!RepeatBlock.TryParseCount(Value, out _))
            {
                error = Loc.Format("Error_RepeatCount", RepeatBlock.MaxCount);
                return false;
            }
        }

        return true;
    }

    public static bool IsValidHotkey(string? hotkey, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            error = Loc.Get("Error_HotkeyEmpty");
            return false;
        }

        var parts = hotkey.Split(['+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            error = Loc.Get("Error_HotkeyEmpty");
            return false;
        }

        var modifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMain = false;
        foreach (var part in parts)
        {
            var normalized = SpecialKeyCatalog.Normalize(part);
            if (IsModifier(normalized))
            {
                if (!modifiers.Add(normalized))
                {
                    error = Loc.Format("Error_ModifierDuplicate", normalized);
                    return false;
                }
            }
            else
            {
                if (hasMain)
                {
                    error = Loc.Get("Error_MainKeyOnce");
                    return false;
                }

                hasMain = true;
            }
        }

        if (!hasMain)
        {
            error = Loc.Get("Error_NeedMainKey");
            return false;
        }

        return true;
    }

    private static bool IsModifier(string code) =>
        code is "CTRL" or "SHIFT" or "ALT" or "LWIN" or "RWIN" or "WIN" or "WINDOWS"
            or "CONTROL" or "CTL" or "MENU";

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
        else if (string.Equals(type, "dialog", StringComparison.OrdinalIgnoreCase))
        {
            item.Type = "dialog";
            item.Value = string.IsNullOrWhiteSpace(value) ? UserDialog.DefaultMessage : value;
        }
        else if (RepeatBlock.IsStart(type))
        {
            item.Type = RepeatBlock.StartType;
            item.Value = RepeatBlock.TryParseCount(value, out var n)
                ? n.ToString(CultureInfo.InvariantCulture)
                : RepeatBlock.DefaultCount.ToString(CultureInfo.InvariantCulture);
        }
        else if (RepeatBlock.IsEnd(type))
        {
            item.Type = RepeatBlock.EndType;
            item.Value = string.Empty;
        }
        else
        {
            item.Type = type;
            item.Value = value;
        }

        return item;
    }
}
