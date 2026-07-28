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
            if (string.Equals(args[i], "-name", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return ConfigStore.FindByName(macros, args[i + 1]);
        }

        var first = args[0];
        if (first.StartsWith('-') && first.Length > 1 && int.TryParse(first[1..], out var shortId))
            return ConfigStore.FindById(macros, shortId);

        return null;
    }
}
