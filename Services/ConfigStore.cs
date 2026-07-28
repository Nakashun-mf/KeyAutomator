using System.Text;
using System.Text.Json;
using KeyAutomator.Models;

namespace KeyAutomator.Services;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ConfigPath => AppPaths.ConfigPath;

    public static List<MacroItem> Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var empty = new List<MacroItem>();
            Save(empty);
            return empty;
        }

        var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<MacroItem>>(json, JsonOptions) ?? [];
    }

    public static void Save(IEnumerable<MacroItem> macros)
    {
        var json = JsonSerializer.Serialize(macros.ToList(), JsonOptions);
        File.WriteAllText(ConfigPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static MacroItem? FindById(IEnumerable<MacroItem> macros, int id) =>
        macros.FirstOrDefault(m => m.Id == id);

    public static MacroItem? FindByName(IEnumerable<MacroItem> macros, string name) =>
        macros.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    public static MacroItem? FindByAlias(IEnumerable<MacroItem> macros, string alias) =>
        macros.FirstOrDefault(m =>
            !string.IsNullOrWhiteSpace(m.Alias) &&
            string.Equals(m.Alias, alias, StringComparison.OrdinalIgnoreCase));

    public static int NextId(IEnumerable<MacroItem> macros) =>
        macros.Any() ? macros.Max(m => m.Id) + 1 : 1;
}
