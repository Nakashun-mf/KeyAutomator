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
            // 初回: 空ではなくサンプルを入れて「壊れてる？」を防ぐ
            var samples = LoadSampleMacros();
            Save(samples);
            return samples;
        }

        var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<MacroItem>>(json, JsonOptions)
               ?? throw new InvalidDataException(Loc.Get("Ex_ConfigUnreadable"));
    }

    /// <summary>
    /// exe 横 / データフォルダの <c>config.sample.json</c> または <c>config.sample.en.json</c>。
    /// 無ければ内蔵サンプル。表示名は UI 言語で上書きする。
    /// </summary>
    public static List<MacroItem> LoadSampleMacros()
    {
        foreach (var path in SampleCandidatePaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var list = JsonSerializer.Deserialize<List<MacroItem>>(json, JsonOptions);
                if (list is { Count: > 0 })
                {
                    BuiltInSamples.ApplyCurrentLanguage(list);
                    return list;
                }
            }
            catch
            {
                // 次の候補へ
            }
        }

        return BuiltInSamples.Create();
    }

    private static IEnumerable<string> SampleCandidatePaths()
    {
        var names = Loc.CurrentLanguage == Loc.English
            ? new[] { "config.sample.en.json", "config.sample.json" }
            : new[] { "config.sample.json", "config.sample.en.json" };

        foreach (var name in names)
        {
            yield return Path.Combine(AppPaths.ExeDirectory, name);
            yield return Path.Combine(AppPaths.DataDirectory, name);
        }
    }

    public static void Save(IEnumerable<MacroItem> macros)
    {
        var list = macros.ToList();
        var json = JsonSerializer.Serialize(list, JsonOptions);
        AtomicFile.WriteAllText(ConfigPath, json);

        // 書き込み後に読み返して失敗を検知する
        if (!File.Exists(ConfigPath))
            throw new IOException(Loc.Format("Ex_ConfigNotCreated", ConfigPath));

        var written = File.ReadAllText(ConfigPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(written))
            throw new IOException(Loc.Format("Ex_ConfigEmptyAfterSave", ConfigPath));
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
