using System.Globalization;
using System.Xml.Linq;

namespace KeyAutomator.Services;

/// <summary>
/// UI / CLI 文字列。Windows の表示言語、または設定の上書きに従う。
/// XAML の <c>x:Uid</c> と同じ .resw を埋め込みフォールバックとしても読む。
/// </summary>
public static class Loc
{
    public const string English = "en-US";
    public const string Japanese = "ja-JP";
    public const string SystemPreference = "";

    private static readonly XName DataName = XName.Get("data");
    private static readonly XName ValueName = XName.Get("value");

    private static Dictionary<string, string> _table = [];
    private static string _resolvedLanguage = English;
    private static string _preference = SystemPreference;
    private static bool _initialized;

    /// <summary>実際に読み込んでいる言語（en-US または ja-JP）。</summary>
    public static string CurrentLanguage => _resolvedLanguage;

    /// <summary>settings.json の値。空なら OS に合わせる。</summary>
    public static string Preference => _preference;

    /// <summary>settings.json の <c>ui_language</c> を読んで初期化する。</summary>
    public static void InitializeFromSettings()
    {
        var preference = SystemPreference;
        try
        {
            preference = SettingsStore.Load().UiLanguage ?? SystemPreference;
        }
        catch
        {
            // 設定が読めなくても起動は続ける
        }

        Initialize(preference);
    }

    /// <summary>指定した言語設定で文字列テーブルを読み込む。空なら OS の UI 言語。</summary>
    public static void Initialize(string? preference)
    {
        _preference = NormalizePreference(preference);
        _resolvedLanguage = ResolveLanguage(_preference);
        _table = LoadTable(_resolvedLanguage);
        ApplyProcessCulture(_preference, _resolvedLanguage);
        _initialized = true;
    }

    /// <summary>リソース識別子に対応する文字列を返す。</summary>
    public static string Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureInitialized();
        return _table.TryGetValue(name, out var value) && value.Length > 0
            ? value
            : name;
    }

    /// <summary>複合書式のリソース文字列を現在のカルチャで整形する。</summary>
    public static string Format(string name, params object[] args)
    {
        var template = Get(name);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    internal static IReadOnlyDictionary<string, string> LoadTableFor(string language) =>
        LoadTable(ResolveLanguage(language));

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;
        Initialize(SystemPreference);
    }

    private static string NormalizePreference(string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference))
            return SystemPreference;

        var trimmed = preference.Trim();
        if (trimmed.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            return Japanese;
        if (trimmed.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return English;
        return SystemPreference;
    }

    private static string ResolveLanguage(string preference)
    {
        if (preference == Japanese)
            return Japanese;
        if (preference == English)
            return English;

        var ui = CultureInfo.CurrentUICulture.Name;
        return ui.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? Japanese : English;
    }

    private static void ApplyProcessCulture(string preference, string resolved)
    {
        var culture = CultureInfo.GetCultureInfo(resolved);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride =
                preference.Length == 0 ? string.Empty : resolved;
        }
        catch
        {
            // テストホストなど WinRT 初期化前は C# テーブル側で補う
        }
    }

    private static Dictionary<string, string> LoadTable(string language)
    {
        var xml = ReadReswXml(language);
        if (string.IsNullOrEmpty(xml) && language != English)
            xml = ReadReswXml(English);
        if (string.IsNullOrEmpty(xml))
            return [];

        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var doc = XDocument.Parse(xml);
        foreach (var data in doc.Descendants(DataName))
        {
            var name = (string?)data.Attribute("name");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            table[name] = data.Element(ValueName)?.Value ?? string.Empty;
        }

        return table;
    }

    private static string? ReadReswXml(string language)
    {
        var resourceName = $"KeyAutomator.Loc.{language}.resw";
        var assembly = typeof(Loc).Assembly;
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }

        foreach (var candidate in EnumerateFileCandidates(language))
        {
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }

        return null;
    }

    private static IEnumerable<string> EnumerateFileCandidates(string language)
    {
        var relative = Path.Combine("Strings", language, "Resources.resw");
        yield return Path.Combine(AppContext.BaseDirectory, relative);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, relative);
            dir = dir.Parent;
        }
    }
}
