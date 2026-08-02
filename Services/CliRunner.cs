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

    public static string GetHelpText() =>
        """
        KeyAutomator — キー入力自動化

        使い方:
          KeyAutomator.exe                 GUI を起動
          KeyAutomator.exe -1              ID=1 を実行
          KeyAutomator.exe -id 1           同上
          KeyAutomator.exe -alias NAME     引数名で実行
          KeyAutomator.exe -NAME           同上（短縮）
          KeyAutomator.exe -name "表示名"  表示名で実行
          KeyAutomator.exe -h              このヘルプ

        終了コード: 成功 0 / 失敗 1
        ログ: error.log（書き込み可能なデータフォルダ。保護フォルダ時は %LocalAppData%\\KeyAutomator）

        注意:
          - CLI では管理画面を出さずにマクロを実行します
          - 確認アクション(dialog) があるマクロは、CLI でもメッセージボックスが出ます
            （完全な無人実行ではありません）
        """;

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
                TryWriteToConsole("指定マクロが見つかりません。-h で使い方を表示します。");
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

    private static void TryWriteToConsole(string text)
    {
        try
        {
            // パイプ／リダイレクト時は標準出力へ直接書く（AttachConsole だと落ちる／見えない）
            if (Console.IsOutputRedirected)
            {
                Console.Out.WriteLine(text);
                Console.Out.Flush();
                return;
            }

            NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
            Console.WriteLine(text);
        }
        catch
        {
            ErrorLogger.Write(text);
        }
    }
}
