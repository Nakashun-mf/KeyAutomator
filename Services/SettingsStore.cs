using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyAutomator.Services;

public sealed class AppSettings
{
    [JsonPropertyName("confirm_before_delete")]
    public bool ConfirmBeforeDelete { get; set; } = true;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string SettingsPath => AppPaths.SettingsPath;

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "settings.json 読み込み失敗");
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "settings.json 保存失敗");
        }
    }
}
