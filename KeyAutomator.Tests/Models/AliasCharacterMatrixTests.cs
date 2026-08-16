using System.Text.RegularExpressions;
using KeyAutomator.Models;

namespace KeyAutomator.Tests.Models;

[TestClass]
public class AliasCharacterMatrixTests
{
    private static readonly Regex Allowed = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static IEnumerable<object[]> BmpBlock()
    {
        // 基本多言語面の先頭〜（空白・制御・記号・ラテン・拡張）。1 コードポイント 1 ケース。
        for (var code = 0; code <= 0x10FF; code++)
            yield return new object[] { code };
    }

    public static IEnumerable<object[]> CjkKanaBlock()
    {
        for (var code = 0x3040; code <= 0x30FF; code++)
            yield return new object[] { code };
    }

    [TestMethod]
    [DynamicData(nameof(BmpBlock), DynamicDataSourceType.Method)]
    public void IsValidAlias_BmpPrefix_MatchesGrammar(int codePoint)
    {
        AssertAliasCodePoint(codePoint);
    }

    [TestMethod]
    [DynamicData(nameof(CjkKanaBlock), DynamicDataSourceType.Method)]
    public void IsValidAlias_HiraganaKatakana_RejectedUnlessWhitespace(int codePoint)
    {
        AssertAliasCodePoint(codePoint);
    }

    private static void AssertAliasCodePoint(int codePoint)
    {
        var text = ((char)codePoint).ToString();
        var expected = string.IsNullOrWhiteSpace(text) || Allowed.IsMatch(text.Trim());
        var actual = MacroItem.IsValidAlias(text, out var error);
        Assert.AreEqual(expected, actual, $"U+{codePoint:X4} '{Escape(text)}'");
        if (actual)
            Assert.AreEqual(string.Empty, error);
        else
            StringAssert.Contains(error, "英数字");
    }

    private static string Escape(string text)
    {
        if (text.Length == 0) return "";
        var ch = text[0];
        return char.IsControl(ch) ? $"\\x{(int)ch:X2}" : text;
    }
}
