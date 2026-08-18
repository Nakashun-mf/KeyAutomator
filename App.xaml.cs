using KeyAutomator.Services;
using Microsoft.UI.Xaml;

namespace KeyAutomator;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride =
                Loc.Preference.Length == 0 ? string.Empty : Loc.CurrentLanguage;
        }
        catch
        {
            // unpackaged / テストでは C# テーブル側で補う
        }

        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>言語切替後にメインウィンドウを作り直す。</summary>
    public void RestartMainWindow()
    {
        var old = _window;
        _window = new MainWindow();
        _window.Activate();
        old?.Close();
    }
}
