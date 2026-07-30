using System.Text.Json;
using KeyAutomator.Models;

namespace KeyAutomator.Services;

/// <summary>
/// zip 同梱の config.sample.json と同じ内容の内蔵サンプル。
/// exe だけコピーされた場合でも初回体験が空にならないようにする。
/// </summary>
public static class BuiltInSamples
{
    // config.sample.json と同期すること
    private const string SampleJson =
        """
        [
          {
            "id": 1,
            "name": "ログイン&定型データ入力",
            "alias": "login_ok",
            "delay_sec": 3.0,
            "actions": [
              { "type": "text", "value": "user_admin" },
              { "type": "key", "value": "TAB" },
              { "type": "text", "value": "dummy_secret_do_not_use" },
              { "type": "key", "value": "ENTER" },
              { "type": "dialog", "value": "ログイン完了を確認したら OK を押してください" },
              { "type": "wait", "value": "1.0" },
              { "type": "hotkey", "value": "CTRL+S" }
            ]
          },
          {
            "id": 2,
            "name": "全選択＆コピー",
            "alias": "select_copy",
            "delay_sec": 2.0,
            "actions": [
              { "type": "hotkey", "value": "CTRL+A" },
              { "type": "hotkey", "value": "CTRL+C" }
            ]
          }
        ]
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<MacroItem> Create()
    {
        var list = JsonSerializer.Deserialize<List<MacroItem>>(SampleJson, JsonOptions);
        return list ?? [];
    }
}
