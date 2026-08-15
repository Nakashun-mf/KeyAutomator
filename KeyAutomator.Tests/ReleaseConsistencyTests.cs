using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests;

[TestClass]
public class ReleaseConsistencyTests
{
    [TestMethod]
    public void Version_CsprojManifestAndReadme_Match()
    {
        var root = FindRepoRoot();
        var csproj = File.ReadAllText(Path.Combine(root, "KeyAutomator.csproj"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var developer = File.ReadAllText(Path.Combine(root, "README_DEVELOPER.md"));
        var manifest = XDocument.Load(Path.Combine(root, "Package.appxmanifest"));

        var version = Regex.Match(csproj, @"<Version>([^<]+)</Version>").Groups[1].Value;
        var assembly = Regex.Match(csproj, @"<AssemblyVersion>([^<]+)</AssemblyVersion>").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrWhiteSpace(version));
        Assert.AreEqual(version + ".0", assembly);

        var identity = manifest.Root?
            .Elements()
            .First(e => e.Name.LocalName == "Identity")
            .Attribute("Version")?.Value;
        Assert.AreEqual(assembly, identity);

        StringAssert.Contains(readme, version);
        StringAssert.Contains(developer, version);
    }

    [TestMethod]
    public void BuiltInSamples_MatchConfigSampleJson()
    {
        var root = FindRepoRoot();
        var json = File.ReadAllText(Path.Combine(root, "config.sample.json"));
        var fromFile = JsonSerializer.Deserialize<List<MacroItem>>(json);
        var fromCode = BuiltInSamples.Create();

        Assert.IsNotNull(fromFile);
        Assert.AreEqual(fromFile.Count, fromCode.Count);
        for (var i = 0; i < fromFile.Count; i++)
        {
            Assert.AreEqual(fromFile[i].Id, fromCode[i].Id);
            Assert.AreEqual(fromFile[i].Name, fromCode[i].Name);
            Assert.AreEqual(fromFile[i].Alias, fromCode[i].Alias);
            Assert.AreEqual(fromFile[i].DelaySec, fromCode[i].DelaySec);
            Assert.AreEqual(fromFile[i].Actions.Count, fromCode[i].Actions.Count);
            for (var a = 0; a < fromFile[i].Actions.Count; a++)
            {
                Assert.AreEqual(fromFile[i].Actions[a].Type, fromCode[i].Actions[a].Type);
                Assert.AreEqual(fromFile[i].Actions[a].Value, fromCode[i].Actions[a].Value);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KeyAutomator.csproj")) &&
                File.Exists(Path.Combine(dir.FullName, "config.sample.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        Assert.Fail("リポジトリルート（KeyAutomator.csproj）が見つかりません");
        return string.Empty;
    }
}
