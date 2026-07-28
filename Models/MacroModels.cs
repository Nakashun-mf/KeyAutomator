using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KeyAutomator.Models;

public sealed class MacroItem
{
    private static readonly Regex AliasPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>CLI 引数用（英数字と _ のみ）。空なら未設定。</summary>
    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("delay_sec")]
    public double DelaySec { get; set; }

    [JsonPropertyName("actions")]
    public List<ActionItem> Actions { get; set; } = [];

    [JsonIgnore]
    public string DelayLabel => $"開始まで {DelaySec:0.##} 秒";

    [JsonIgnore]
    public string ActionCountLabel => Actions.Count == 0 ? "手順なし" : $"{Actions.Count} 手順";

    [JsonIgnore]
    public string AliasLabel =>
        string.IsNullOrWhiteSpace(Alias) ? "引数名なし" : $"引数: {Alias}";

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
