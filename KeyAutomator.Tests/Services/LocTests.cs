using System.Text.RegularExpressions;
using System.Xml.Linq;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class LocTests
{
    [TestMethod]
    public void ReswFiles_SameKeysAndPlaceholderIndexes()
    {
        var en = LoadNames("en-US");
        var ja = LoadNames("ja-JP");
        CollectionAssert.AreEquivalent(en.Keys.ToList(), ja.Keys.ToList());

        var placeholder = new Regex(@"\{(\d+)\}", RegexOptions.CultureInvariant);
        foreach (var key in en.Keys)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(en[key]), key);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ja[key]), key);
            CollectionAssert.AreEquivalent(
                placeholder.Matches(en[key]).Select(m => m.Value).Distinct().ToList(),
                placeholder.Matches(ja[key]).Select(m => m.Value).Distinct().ToList(),
                key);
        }
    }

    [TestMethod]
    public void Get_Japanese_ReturnsJapaneseReadyStatus()
    {
        Loc.Initialize(Loc.Japanese);
        Assert.AreEqual("準備完了", Loc.Get("Status_Ready"));
        StringAssert.Contains(Loc.Get("Cli_Help"), "終了コード");
        Assert.AreEqual("ログイン&定型データ入力", BuiltInSamples.Create()[0].Name);
    }

    [TestMethod]
    public void Get_English_ReturnsEnglishReadyStatus()
    {
        try
        {
            Loc.Initialize(Loc.English);
            Assert.AreEqual("Ready", Loc.Get("Status_Ready"));
            Assert.AreEqual("New macro", Loc.Get("Macro_DefaultName"));
            StringAssert.Contains(Loc.Get("Cli_Help"), "Exit codes");
            StringAssert.Contains(Loc.Format("Macro_ActionCount", 2), "2");
            StringAssert.Contains(Loc.Format("Macro_ActionCount", 2), "steps");
            Assert.AreEqual("Login and type fixed data", BuiltInSamples.Create()[0].Name);
            StringAssert.Contains(Loc.Format("Log_UnknownActionType", "speech"), "Unknown action type");
            StringAssert.Contains(Loc.Get("Ex_ConfigUnreadable"), "config.json");
        }
        finally
        {
            Loc.Initialize(Loc.Japanese);
        }
    }

    [TestMethod]
    public void Initialize_SystemPreferenceOnJapaneseUiCulture_ResolvesJapanese()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture =
                System.Globalization.CultureInfo.GetCultureInfo("ja-JP");
            Loc.Initialize(Loc.SystemPreference);
            Assert.AreEqual(Loc.Japanese, Loc.CurrentLanguage);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
            Loc.Initialize(Loc.Japanese);
        }
    }

    [TestMethod]
    public void Initialize_SystemPreferenceOnEnglishUiCulture_ResolvesEnglish()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture =
                System.Globalization.CultureInfo.GetCultureInfo("en-US");
            Loc.Initialize(Loc.SystemPreference);
            Assert.AreEqual(Loc.English, Loc.CurrentLanguage);
            Assert.AreEqual("Ready", Loc.Get("Status_Ready"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
            Loc.Initialize(Loc.Japanese);
        }
    }

    [TestMethod]
    public void MainWindowXaml_UidKeys_ExistInResw()
    {
        var xaml = RepoFiles.Read("MainWindow.xaml");
        var uids = Regex.Matches(xaml, @"x:Uid=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.IsTrue(uids.Count >= 20, $"x:Uid count was {uids.Count}");

        var ja = LoadNames("ja-JP");
        foreach (var uid in uids)
        {
            var found = ja.Keys.Any(k =>
                string.Equals(k, uid, StringComparison.Ordinal) ||
                k.StartsWith(uid + ".", StringComparison.Ordinal));
            Assert.IsTrue(found, $"Resources.resw に {uid} がありません");
        }
    }

    [TestMethod]
    public void LoadTableFor_EnglishAndJapanese_DifferOnUiKeys()
    {
        var en = Loc.LoadTableFor(Loc.English);
        var ja = Loc.LoadTableFor(Loc.Japanese);
        Assert.AreNotEqual(en["Status_Ready"], ja["Status_Ready"]);
        Assert.AreNotEqual(en["ActionType_dialog"], ja["ActionType_dialog"]);
        Assert.AreNotEqual(en["Log_SaveFailed"], ja["Log_SaveFailed"]);
        Assert.AreNotEqual(en["Ex_PathContainsQuote"], ja["Ex_PathContainsQuote"]);
    }

    [TestMethod]
    public void ProductionCs_QuotedStrings_ContainNoJapanese()
    {
        var japanese = new Regex(@"[\u3040-\u30ff\u4e00-\u9fff]");
        var quoted = new Regex("\"(?:\\\\.|[^\"\\\\])*\"");
        var tests = Path.DirectorySeparatorChar + "KeyAutomator.Tests" + Path.DirectorySeparatorChar;
        foreach (var file in Directory.GetFiles(RepoFiles.Root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(tests, StringComparison.OrdinalIgnoreCase) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            var inBlock = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (inBlock)
                {
                    var end = line.IndexOf("*/", StringComparison.Ordinal);
                    if (end < 0)
                        continue;
                    line = line[(end + 2)..];
                    inBlock = false;
                }

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;

                var blockStart = line.IndexOf("/*", StringComparison.Ordinal);
                if (blockStart >= 0)
                {
                    var blockEnd = line.IndexOf("*/", blockStart + 2, StringComparison.Ordinal);
                    if (blockEnd < 0)
                    {
                        inBlock = true;
                        line = line[..blockStart];
                    }
                    else
                    {
                        line = string.Concat(line.AsSpan(0, blockStart), line.AsSpan(blockEnd + 2));
                    }
                }

                var slash = line.IndexOf("//", StringComparison.Ordinal);
                if (slash >= 0)
                    line = line[..slash];

                foreach (Match match in quoted.Matches(line))
                {
                    Assert.IsFalse(
                        japanese.IsMatch(match.Value),
                        $"{Path.GetRelativePath(RepoFiles.Root, file)}:{i + 1} {match.Value}");
                }
            }
        }
    }

    private static Dictionary<string, string> LoadNames(string language)
    {
        var path = RepoFiles.Combine("Strings", language, "Resources.resw");
        var doc = XDocument.Load(path);
        return doc.Descendants("data")
            .Select(e => (
                Name: (string?)e.Attribute("name"),
                Value: e.Element("value")?.Value ?? string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToDictionary(x => x.Name!, x => x.Value, StringComparer.Ordinal);
    }
}
