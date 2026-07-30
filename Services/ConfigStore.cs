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
               ?? throw new InvalidDataException("config.json の内容を解釈できませんでした");
    }

    /// <summary>
    /// exe 横 / データフォルダの config.sample.json、無ければ内蔵サンプル。
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
                    return list;
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
        yield return Path.Combine(AppPaths.ExeDirectory, "config.sample.json");
        yield return Path.Combine(AppPaths.DataDirectory, "config.sample.json");
    }

    public static void Save(IEnumerable<MacroItem> macros)
    {
        var list = macros.ToList();
        var json = JsonSerializer.Serialize(list, JsonOptions);
        AtomicFile.WriteAllText(ConfigPath, json);

        // 書き込み後に読み返して失敗を検知する
        if (!File.Exists(ConfigPath))
            throw new IOException($"保存先にファイルが作成されませんでした: {ConfigPath}");

        var written = File.ReadAllText(ConfigPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(written))
            throw new IOException($"保存後の config.json が空です: {ConfigPath}");
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
