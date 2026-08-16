using System.Text.RegularExpressions;
using KeyAutomator.Models;

namespace KeyAutomator.Tests.Models;

[TestClass]
public class AliasCharacterMatrixTests
{
    private static readonly Regex Allowed = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    [TestMethod]
    [DataRow("login_ok", true)]
    [DataRow("A", true)]
    [DataRow("n9", true)]
    [DataRow("_", true)]
    [DataRow("", true)]
    [DataRow("   ", true)]
    [DataRow("ログイン", false)]
    [DataRow("has space", false)]
    [DataRow("dash-name", false)]
    [DataRow("a.b", false)]
    [DataRow("あ", false)]
    public void IsValidAlias_RepresentativeValues_MatchGrammar(string alias, bool expected)
    {
        Assert.AreEqual(expected, MacroItem.IsValidAlias(alias, out var error));
        if (expected)
            Assert.AreEqual(string.Empty, error);
        else
            StringAssert.Contains(error, "英数字");
    }

    [TestMethod]
    public void IsValidAlias_SampledUnicodeBlocks_MatchGrammar()
    {
        // 件数を膨らませず、代表ブロックを 1 テスト内でなめる
        foreach (var code in SampleCodePoints())
        {
            var text = ((char)code).ToString();
            var expected = string.IsNullOrWhiteSpace(text) || Allowed.IsMatch(text.Trim());
            var actual = MacroItem.IsValidAlias(text, out _);
            Assert.AreEqual(expected, actual, $"U+{code:X4}");
        }
    }

    private static IEnumerable<int> SampleCodePoints()
    {
        for (var code = 0; code <= 0x007F; code++)
            yield return code;
        foreach (var code in new[] { 0x00A0, 0x00E9, 0x3042, 0x30A2, 0x4E00, 0xFF21, 0xFF0D })
            yield return code;
    }
}
