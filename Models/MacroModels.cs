using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyAutomator.Models;

public sealed partial class MacroItem : ObservableObject
{
    private static readonly Regex AliasPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    [ObservableProperty]
    [property: JsonPropertyName("id")]
    private int _id;

    [ObservableProperty]
    [property: JsonPropertyName("name")]
    private string _name = string.Empty;

    /// <summary>CLI 引数用（英数字と _ のみ）。空なら未設定。</summary>
    [ObservableProperty]
    [property: JsonPropertyName("alias")]
    private string _alias = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("delay_sec")]
    private double _delaySec;

    [ObservableProperty]
    [property: JsonPropertyName("actions")]
    private List<ActionItem> _actions = [];

    [JsonIgnore]
    public string DelayLabel => $"開始まで {DelaySec:0.##} 秒";

    [JsonIgnore]
    public string ActionCountLabel => Actions.Count == 0 ? "手順なし" : $"{Actions.Count} 手順";

    [JsonIgnore]
    public string AliasLabel =>
        string.IsNullOrWhiteSpace(Alias) ? "CLI引数なし（任意）" : $"CLI引数: {Alias}";

    partial void OnDelaySecChanged(double value) => OnPropertyChanged(nameof(DelayLabel));

    partial void OnAliasChanged(string value) => OnPropertyChanged(nameof(AliasLabel));

    partial void OnActionsChanged(List<ActionItem> value) => OnPropertyChanged(nameof(ActionCountLabel));

    public MacroItem Clone() => new()
    {
        Id = Id,
        Name = Name,
        Alias = Alias,
        DelaySec = DelaySec,
        Actions = Actions.Select(a => a.Clone()).ToList()
    };

    public static bool IsValidAlias(string? alias, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(alias))
            return true;

        var trimmed = alias.Trim();
        if (!AliasPattern.IsMatch(trimmed))
        {
            error = "引数名は英数字と _ のみ使えます";
            return false;
        }

        return true;
    }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Alias) ? $"{Id}: {Name}" : $"{Id}: {Name} [{Alias}]";
}

public sealed class ActionItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    public ActionItem Clone() => new() { Type = Type, Value = Value };
}
