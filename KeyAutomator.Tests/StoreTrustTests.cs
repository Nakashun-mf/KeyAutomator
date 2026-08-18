using System.Text.RegularExpressions;
using System.Xml.Linq;
using KeyAutomator.Services;

namespace KeyAutomator.Tests;

/// <summary>
/// Microsoft Store 提出で他社より安心できる、と説明できる契約。
/// 権限の最小化・通信なし・ダミーサンプル・提出 Identity の同期を CI で固定する。
/// </summary>
[TestClass]
public class StoreTrustTests
{
    private static readonly string[] BannedNetworkTypes =
    [
        "HttpClient",
        "WebClient",
        "HttpWebRequest",
        "FtpWebRequest",
        "SmtpClient",
        "TelemetryClient",
        "ApplicationInsights",
        "SentrySdk"
    ];

    [TestMethod]
    public void Manifest_DeclaresOnlyRunFullTrustCapability()
    {
        var names = CapabilityNames();
        CollectionAssert.AreEqual(new[] { "runFullTrust" }, names);
    }

    [TestMethod]
    public void Manifest_IdentityMatchesProductIdentityDoc()
    {
        var identity = IdentityElement();
        var doc = RepoFiles.Read("docs", "microsoft-store", "product-identity.md");

        Assert.AreEqual("pryzo.KeyAutomator", (string?)identity.Attribute("Name"));
        Assert.AreEqual(
            "CN=4B1F058B-F39E-44DE-8373-4258152DED0F",
            (string?)identity.Attribute("Publisher"));
        Assert.AreEqual("pryzo", PublisherDisplayName());

        StringAssert.Contains(doc, "pryzo.KeyAutomator");
        StringAssert.Contains(doc, "CN=4B1F058B-F39E-44DE-8373-4258152DED0F");
        StringAssert.Contains(doc, "9P814VCBNVGF");
        StringAssert.Contains(doc, "pryzo.KeyAutomator_29frz59n2q2dp");
    }

    [TestMethod]
    public void Manifest_DeclaresCliAppExecutionAlias()
    {
        var aliases = Manifest()
            .Descendants()
            .Where(e => e.Name.LocalName == "ExecutionAlias")
            .Select(e => (string?)e.Attribute("Alias"))
            .ToList();

        CollectionAssert.AreEqual(new[] { "KeyAutomator.exe" }, aliases);
    }

    [TestMethod]
    public void ProductionCode_DoesNotReferenceNetworkOrTelemetryTypes()
    {
        foreach (var file in ProductionCsFiles())
        {
            var text = File.ReadAllText(file);
            foreach (var banned in BannedNetworkTypes)
            {
                Assert.IsFalse(
                    ContainsAsTypeName(text, banned),
                    $"{Path.GetRelativePath(RepoFiles.Root, file)} に {banned} があります");
            }
        }
    }

    [TestMethod]
    public void PrivacyPolicy_ExistsAndDeniesNetworkAndKeylogging()
    {
        var privacy = RepoFiles.Read("PRIVACY.md");
        StringAssert.Contains(privacy, "インターネットへ個人情報やマクロ内容を送信しません");
        StringAssert.Contains(privacy, "キーロガーではありません");
        StringAssert.Contains(privacy, "runFullTrust");
        StringAssert.Contains(privacy, "未使用の Capability は追加していません");
    }

    [TestMethod]
    public void RunFullTrustJustification_ExistsForPartnerCenter()
    {
        var text = RepoFiles.Read("docs", "microsoft-store", "runFullTrust.md");
        StringAssert.Contains(text, "runFullTrust");
        StringAssert.Contains(text, "does not transmit");
        StringAssert.Contains(text, "SendInput");
    }

    [TestMethod]
    public void ReleaseWorkflow_PublishesStoreUploadWithProductIdAndRepoSecrets()
    {
        var yml = RepoFiles.Read(".github", "workflows", "release.yml");
        StringAssert.Contains(yml, "microsoft-store-apppublisher@");
        StringAssert.Contains(yml, "Build-MsixStore.ps1");
        StringAssert.Contains(yml, "9P814VCBNVGF");
        StringAssert.Contains(yml, "msstore publish");
        StringAssert.Contains(yml, "secrets.AZURE_AD_TENANT_ID");
        StringAssert.Contains(yml, "secrets.AZURE_AD_APPLICATION_CLIENT_ID");
        StringAssert.Contains(yml, "secrets.AZURE_AD_APPLICATION_SECRET");
        StringAssert.Contains(yml, "secrets.SELLER_ID");
        StringAssert.Contains(yml, "--noCommit は付けない");
        StringAssert.Contains(yml, "msstore publish -i \"$env:STORE_PACKAGE\"");
        Assert.IsFalse(
            Regex.IsMatch(yml, @"msstore publish[^\n]*MSIX_PATH"),
            "サイドロード成果物 MSIX_PATH を Store に提出してはいけない");
        Assert.IsFalse(
            yml.Contains("AZURE_AD_APPLICATION_SECRET: \"", StringComparison.Ordinal),
            "Client secret をワークフローに直書きしてはいけない");
        Assert.IsFalse(
            yml.Contains("--clientSecret ${{ secrets", StringComparison.Ordinal),
            "Client secret をコマンドライン引数に展開してはいけない");
    }

    [TestMethod]
    public void Gitignore_ExcludesSigningSecrets()
    {
        var gitignore = RepoFiles.Read(".gitignore");
        StringAssert.Contains(gitignore, "*.pfx");
        StringAssert.Contains(gitignore, "KeyAutomator_CI.cer");
    }

    [TestMethod]
    public void MainWindow_ExposesStableAutomationIdsForE2E()
    {
        var xaml = RepoFiles.Read("MainWindow.xaml");
        foreach (var id in new[]
                 {
                     "NewMacroButton",
                     "SaveMacroButton",
                     "MacroNameBox",
                     "MacroListView"
                 })
        {
            StringAssert.Contains(xaml, $"AutomationProperties.AutomationId=\"{id}\"");
        }
    }

    [TestMethod]
    public void BuiltInSamples_PasswordLikeTextIsClearlyDummy()
    {
        var secrets = BuiltInSamples.Create()
            .SelectMany(m => m.Actions)
            .Where(a => string.Equals(a.Type, "text", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Value ?? string.Empty)
            .Where(v => v.Length >= 4)
            .ToList();

        Assert.IsTrue(secrets.Count > 0);
        foreach (var value in secrets)
        {
            var ok = value.Contains("dummy", StringComparison.OrdinalIgnoreCase) ||
                     value.Contains("example", StringComparison.OrdinalIgnoreCase) ||
                     value.Contains("sample", StringComparison.OrdinalIgnoreCase) ||
                     value.Equals("user_admin", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(ok, $"サンプルのテキストが実在しそう: '{value}'");
        }
    }

    private static XDocument Manifest() =>
        XDocument.Load(RepoFiles.Combine("Package.appxmanifest"));

    private static XElement IdentityElement() =>
        Manifest().Root!.Elements().First(e => e.Name.LocalName == "Identity");

    private static string PublisherDisplayName() =>
        Manifest().Descendants().First(e => e.Name.LocalName == "PublisherDisplayName").Value;

    private static string[] CapabilityNames() =>
        Manifest()
            .Descendants()
            .Where(e => e.Name.LocalName == "Capability")
            .Select(e => (string?)e.Attribute("Name"))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .ToArray();

    private static IEnumerable<string> ProductionCsFiles()
    {
        var tests = Path.DirectorySeparatorChar + "KeyAutomator.Tests" + Path.DirectorySeparatorChar;
        var obj = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;
        var bin = Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar;
        return Directory.GetFiles(RepoFiles.Root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(tests, StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains(obj, StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains(bin, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAsTypeName(string source, string typeName) =>
        Regex.IsMatch(source, $@"\b{Regex.Escape(typeName)}\b");
}
