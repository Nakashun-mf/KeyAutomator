using KeyAutomator.Models;

namespace KeyAutomator.Services;

public static class CliRunner
{
    public static bool IsCliMode(string[] args) => args is { Length: > 0 };

    public static int Run(string[] args)
    {
        try
        {
            var macro = ResolveMacro(args);
            if (macro is null)
            {
                ErrorLogger.Write($"指定マクロが見つかりません: {string.Join(' ', args)}");
                return 1;
            }

            KeySender.ExecuteMacro(macro);
            return 0;
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "CLI実行エラー");
            return 1;
        }
    }

    public static MacroItem? ResolveMacro(string[] args)
    {
        List<MacroItem> macros;
        try
        {
            macros = ConfigStore.Load();
        }
        catch (Exception ex)
        {
            ErrorLogger.Write(ex, "config.json 読み込み失敗");
            return null;
        }

        if (args is not { Length: > 0 })
            return null;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var id))
            {
                return ConfigStore.FindById(macros, id);
            }
        }

        for (var i = 0; i < args.Length; i++)
        {
            if ((string.Equals(args[i], "-alias", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[i], "-a", StringComparison.OrdinalIgnoreCase))
                && i + 1 < args.Length)
            {
                return ConfigStore.FindByAlias(macros, args[i + 1]);
            }
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "-name", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return ConfigStore.FindByName(macros, args[i + 1]);
        }

        var first = args[0];
        if (first.StartsWith('-') && first.Length > 1)
        {
            var body = first[1..];
            if (int.TryParse(body, out var shortId))
                return ConfigStore.FindById(macros, shortId);

            // -login_ok 形式（英数字と _）
            if (MacroItem.IsValidAlias(body, out _) && !string.IsNullOrWhiteSpace(body))
            {
                var byAlias = ConfigStore.FindByAlias(macros, body);
                if (byAlias is not null)
                    return byAlias;
            }
        }
        else if (!first.StartsWith('-'))
        {
            // 素の引数: login_ok
            var byAlias = ConfigStore.FindByAlias(macros, first);
            if (byAlias is not null)
                return byAlias;
        }

        return null;
    }
}
