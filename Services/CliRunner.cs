using KeyAutomator.Models;

namespace KeyAutomator.Services;

public static class CliRunner
{
    public static bool IsCliMode(string[] args) => args is { Length: > 0 };

    public static bool IsHelpRequest(string[] args) =>
        args.Any(a =>
            string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "/?", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-?", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "help", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 起動パスに引数を足した、貼り付け用のコマンド。
    /// GUI の「起動パスをコピー」がクリップボードへ入れる文字列。
    /// 引数名があれば <c>-alias</c>、マクロ ID があれば <c>-id</c>、どちらも無ければパスのみ。
    /// </summary>
    public static string FormatLaunchCommand(string exePath, string? alias = null, int? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        var quoted = AppPaths.QuoteCliPath(exePath);
        var trimmed = alias?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            return quoted + " -alias " + trimmed;

        if (id is >= 1)
            return quoted + " -id " + id.Value;

        return quoted;
    }

    public static string GetHelpText() => Loc.Get("Cli_Help").Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    public static int Run(string[] args)
    {
        try
        {
            if (IsHelpRequest(args))
            {
                TryWriteToConsole(GetHelpText());
                return 0;
            }

            var macro = ResolveMacro(args);
            if (macro is null)
            {
                ErrorLogger.Write($"指定マクロが見つかりません: {string.Join(' ', args)}");
                TryWriteToConsole(Loc.Get("Cli_MacroNotFound"));
                return 1;
            }

            KeySender.ExecuteMacro(macro);
            return 0;
        }
        catch (OperationCanceledException)
        {
            ErrorLogger.Write("CLI実行がキャンセルされました");
            return 1;
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

        return ResolveMacro(args, macros);
    }

    /// <summary>
    /// 読み込み済みマクロ一覧から CLI 引数で対象を解決する（ディスク I/O なし）。
    /// </summary>
    public static MacroItem? ResolveMacro(string[] args, IReadOnlyList<MacroItem> macros)
    {
        ArgumentNullException.ThrowIfNull(macros);

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

        if (TryResolveShortForm(macros, args, out var shortHit))
            return shortHit;

        var first = args[0];
        if (!first.StartsWith('-'))
        {
            var byAlias = ConfigStore.FindByAlias(macros, first);
            if (byAlias is not null)
                return byAlias;
        }

        return null;
    }

    private static bool TryResolveShortForm(IReadOnlyList<MacroItem> macros, string[] args, out MacroItem? macro)
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

    private static void TryWriteToConsole(string text)
    {
        try
        {
            Console.Out.WriteLine(text);
            Console.Out.Flush();
            return;
        }
        catch
        {
            // ignore and fall back
        }

        try
        {
            NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
            Console.WriteLine(text);
        }
        catch
        {
            try
            {
                ErrorLogger.Write(text);
            }
            catch
            {
                // ヘルプ表示失敗でプロセス全体を落とさない
            }
        }
    }
}
