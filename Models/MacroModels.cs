using System.Text.Json.Serialization;

namespace KeyAutomator.Models;

public sealed class MacroItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("delay_sec")]
    public double DelaySec { get; set; }

    [JsonPropertyName("actions")]
    public List<ActionItem> Actions { get; set; } = [];

    public MacroItem Clone() => new()
    {
        Id = Id,
        Name = Name,
        DelaySec = DelaySec,
        Actions = Actions.Select(a => a.Clone()).ToList()
    };

    public override string ToString() => $"{Id}: {Name}";
}

public sealed class ActionItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    public ActionItem Clone() => new() { Type = Type, Value = Value };
}
