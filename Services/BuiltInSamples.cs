using System.Text.Json;
using KeyAutomator.Models;

namespace KeyAutomator.Services;

/// <summary>
/// zip 同梱の config.sample.json と同じ手順の内蔵サンプル。
/// 表示名と確認ダイアログ文は現在の UI 言語に合わせる。
/// </summary>
public static class BuiltInSamples
{
    // config.sample.en.json と構造を同期すること（名前は Loc で上書き）
    private const string SampleJson =
        """
        [
          {
            "id": 1,
            "name": "Login and type fixed data",
            "alias": "login_ok",
            "delay_sec": 3.0,
            "actions": [
              { "type": "text", "value": "user_admin" },
              { "type": "key", "value": "TAB" },
              { "type": "text", "value": "dummy_secret_do_not_use" },
              { "type": "key", "value": "ENTER" },
              { "type": "dialog", "value": "Press OK after you confirm login succeeded" },
              { "type": "wait", "value": "1.0" },
              { "type": "hotkey", "value": "CTRL+S" }
            ]
          },
          {
            "id": 2,
            "name": "Select all and copy",
            "alias": "select_copy",
            "delay_sec": 2.0,
            "actions": [
              { "type": "hotkey", "value": "CTRL+A" },
              { "type": "hotkey", "value": "CTRL+C" }
            ]
          },
          {
            "id": 3,
            "name": "Enter 3 times",
            "alias": "enter_x3",
            "delay_sec": 2.0,
            "actions": [
              { "type": "repeat", "value": "3" },
              { "type": "key", "value": "ENTER" },
              { "type": "end_repeat", "value": "" }
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
        if (list is null)
            return [];

        ApplyCurrentLanguage(list);
        return list;
    }

    /// <summary>
    /// 既知のサンプル（alias）の表示名と確認ダイアログを UI 言語に合わせる。
    /// ユーザーが保存した独自マクロはそのまま。
    /// </summary>
    public static void ApplyCurrentLanguage(IList<MacroItem> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        foreach (var item in list)
        {
            item.Name = item.Alias switch
            {
                "login_ok" => Loc.Get("Sample_LoginName"),
                "select_copy" => Loc.Get("Sample_SelectCopyName"),
                "enter_x3" => Loc.Get("Sample_EnterX3Name"),
                _ => item.Name
            };

            if (!string.Equals(item.Alias, "login_ok", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var action in item.Actions)
            {
                if (string.Equals(action.Type, "dialog", StringComparison.OrdinalIgnoreCase))
                    action.Value = Loc.Get("Sample_LoginDialog");
            }
        }
    }
}
