using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class CliArgumentMatrixTests
{
    private static readonly MacroItem[] Macros =
    [
        new() { Id = 1, Name = "ログイン処理", Alias = "login_ok" },
        new() { Id = 2, Name = "全選択＆コピー", Alias = "select_copy" },
        new() { Id = 3, Name = "Enterを3回", Alias = "enter_x3" },
    ];

    public static IEnumerable<object[]> IdForms()
    {
        foreach (var macro in Macros)
        {
            yield return new object[] { Pack("-id", macro.Id.ToString()), macro.Id };
            yield return new object[] { Pack("-ID", macro.Id.ToString()), macro.Id };
            yield return new object[] { Pack($"-{macro.Id}"), macro.Id };
            yield return new object[] { Pack("-", macro.Id.ToString()), macro.Id };
        }
    }

    public static IEnumerable<object[]> AliasForms()
    {
        foreach (var macro in Macros)
        {
            yield return new object[] { Pack("-alias", macro.Alias), macro.Id };
            yield return new object[] { Pack("-ALIAS", macro.Alias.ToUpperInvariant()), macro.Id };
            yield return new object[] { Pack("-a", macro.Alias), macro.Id };
            yield return new object[] { Pack($"-{macro.Alias}"), macro.Id };
            yield return new object[] { Pack("-", macro.Alias), macro.Id };
            yield return new object[] { Pack(macro.Alias), macro.Id };
        }
    }

    public static IEnumerable<object[]> NameForms()
    {
        foreach (var macro in Macros)
        {
            yield return new object[] { Pack("-name", macro.Name), macro.Id };
            yield return new object[] { Pack("-NAME", macro.Name), macro.Id };
        }
    }

    [TestMethod]
    [DynamicData(nameof(IdForms), DynamicDataSourceType.Method)]
    public void ResolveMacro_IdForms_ReturnExpected(string packed, int expectedId)
    {
        AssertResolved(packed, expectedId);
    }

    [TestMethod]
    [DynamicData(nameof(AliasForms), DynamicDataSourceType.Method)]
    public void ResolveMacro_AliasForms_ReturnExpected(string packed, int expectedId)
    {
        AssertResolved(packed, expectedId);
    }

    [TestMethod]
    [DynamicData(nameof(NameForms), DynamicDataSourceType.Method)]
    public void ResolveMacro_NameForms_ReturnExpected(string packed, int expectedId)
    {
        AssertResolved(packed, expectedId);
    }

    private static string Pack(params string[] args) => string.Join('\u001f', args);

    private static void AssertResolved(string packed, int expectedId)
    {
        var args = packed.Split('\u001f');
        var hit = CliRunner.ResolveMacro(args, Macros);
        Assert.IsNotNull(hit);
        Assert.AreEqual(expectedId, hit.Id);
    }
}
