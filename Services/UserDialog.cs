namespace KeyAutomator.Services;

/// <summary>
/// マクロ実行中の確認ダイアログ（OK 待ち）。
/// テスト時は <see cref="ShowOkHandler"/> を差し替えて実 UI を出さない。
/// </summary>
public static class UserDialog
{
    public const string DefaultMessage = "OKを押すと次の手順へ進みます。";
    public const string Caption = "KeyAutomator";

    /// <summary>単体テスト用。設定時は MessageBox の代わりに呼ばれる。</summary>
    public static Action<string>? ShowOkHandler { get; set; }

    /// <summary>メッセージを表示し、ユーザーが OK を押すまでブロックする。常に最前面。</summary>
    public static void ShowOk(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message.Trim();

        if (ShowOkHandler is not null)
        {
            ShowOkHandler(text);
            return;
        }

        MessageBoxTopmostHost.ShowOk(text, Caption);
    }
}
