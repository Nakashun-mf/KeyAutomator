using KeyAutomator.Models;
using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class RepeatBlockTests
{
    [TestMethod]
    public void TryParseCount_ValidInteger_ReturnsTrue()
    {
        Assert.IsTrue(RepeatBlock.TryParseCount("3", out var count));
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void TryParseCount_OutOfRangeOrNonInteger_ReturnsFalse()
    {
        Assert.IsFalse(RepeatBlock.TryParseCount("0", out _));
        Assert.IsFalse(RepeatBlock.TryParseCount("10000", out _));
        Assert.IsFalse(RepeatBlock.TryParseCount("1.5", out _));
        Assert.IsFalse(RepeatBlock.TryParseCount("abc", out _));
        Assert.IsFalse(RepeatBlock.TryParseCount("", out _));
    }

    [TestMethod]
    public void FindMatchingEnd_NestedBlocks_ReturnsOuterEnd()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "2" },
            new() { Type = "text", Value = "a" },
            new() { Type = "repeat", Value = "3" },
            new() { Type = "text", Value = "b" },
            new() { Type = "end_repeat", Value = "" },
            new() { Type = "text", Value = "c" },
            new() { Type = "end_repeat", Value = "" },
        };

        Assert.AreEqual(6, RepeatBlock.FindMatchingEnd(actions, 0));
        Assert.AreEqual(4, RepeatBlock.FindMatchingEnd(actions, 2));
    }

    [TestMethod]
    public void FindMatchingEnd_MissingEnd_ReturnsMinusOne()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "2" },
            new() { Type = "text", Value = "a" },
        };

        Assert.AreEqual(-1, RepeatBlock.FindMatchingEnd(actions, 0));
    }

    [TestMethod]
    public void ComputeDepths_Nested_AssignsStartAndEndSameDepth()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "text", Value = "x" },
            new() { Type = "repeat", Value = "2" },
            new() { Type = "text", Value = "a" },
            new() { Type = "repeat", Value = "3" },
            new() { Type = "text", Value = "b" },
            new() { Type = "end_repeat", Value = "" },
            new() { Type = "end_repeat", Value = "" },
        };

        var depths = RepeatBlock.ComputeDepths(actions);
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 2, 1, 0 }, depths);
    }

    [TestMethod]
    public void TryValidate_Balanced_ReturnsTrue()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "2" },
            new() { Type = "text", Value = "a" },
            new() { Type = "end_repeat", Value = "" },
        };

        Assert.IsTrue(RepeatBlock.TryValidate(actions, out var error));
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_Unclosed_ReturnsFalse()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "2" },
            new() { Type = "text", Value = "a" },
        };

        Assert.IsFalse(RepeatBlock.TryValidate(actions, out var error));
        Assert.IsTrue(error.Contains("ここまで"));
    }

    [TestMethod]
    public void TryValidate_OrphanEnd_ReturnsFalse()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "end_repeat", Value = "" },
        };

        Assert.IsFalse(RepeatBlock.TryValidate(actions, out var error));
        Assert.IsTrue(error.Contains("繰り返し"));
    }

    [TestMethod]
    public void TryValidate_InvalidCount_ReturnsFalse()
    {
        var actions = new List<ActionItem>
        {
            new() { Type = "repeat", Value = "0" },
            new() { Type = "end_repeat", Value = "" },
        };

        Assert.IsFalse(RepeatBlock.TryValidate(actions, out var error));
        Assert.IsTrue(error.Contains("回数"));
    }
}
