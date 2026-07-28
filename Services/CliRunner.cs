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

        // -1 / -login_ok（スペースなし）または「- 1」「- login_ok」（スペースあり）
        if (TryResolveShortForm(macros, args, out var shortHit))
            return shortHit;

        var first = args[0];
        if (!first.StartsWith('-'))
        {
            // 素の引数: login_ok
            var byAlias = ConfigStore.FindByAlias(macros, first);
            if (byAlias is not null)
                return byAlias;
        }

        return null;
    }

    private static bool TryResolveShortForm(List<MacroItem> macros, string[] args, out MacroItem? macro)
    {
        macro = null;
        if (args is not { Length: > 0 })
            return false;

        string? body = null;
        var first = args[0];
        if (first.StartsWith('-') && first.Length > 1)
        {
            body = first[1..];
        }
        else if (first == "-" && args.Length >= 2)
        {
            body = args[1];
        }

        if (string.IsNullOrWhiteSpace(body))
            return false;

        if (int.TryParse(body, out var shortId))
        {
            macro = ConfigStore.FindById(macros, shortId);
            return true;
        }

        if (MacroItem.IsValidAlias(body, out _))
        {
            macro = ConfigStore.FindByAlias(macros, body);
            return macro is not null;
        }

        return false;
    }
}
