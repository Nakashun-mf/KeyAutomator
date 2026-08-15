using KeyAutomator.Services;
using Windows.System;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class KeySenderResolveTests
{
    [TestMethod]
    [DataRow("ENTER", VirtualKey.Enter)]
    [DataRow("return", VirtualKey.Enter)]
    [DataRow("TAB", VirtualKey.Tab)]
    [DataRow("ESC", VirtualKey.Escape)]
    [DataRow("F12", VirtualKey.F12)]
    [DataRow("UP", VirtualKey.Up)]
    public void ResolveKey_NamedKeys_ReturnsMappedVirtualKey(string name, VirtualKey expected)
    {
        Assert.AreEqual(expected, KeySender.ResolveKey(name));
    }

    [TestMethod]
    public void ResolveKey_SingleLetter_ReturnsCharacterKey()
    {
        Assert.AreEqual(VirtualKey.A, KeySender.ResolveKey("a"));
        Assert.AreEqual(VirtualKey.S, KeySender.ResolveKey("S"));
        Assert.AreEqual(VirtualKey.Number5, KeySender.ResolveKey("5"));
    }

    [TestMethod]
    public void ResolveKey_UnknownOrEmpty_ReturnsNone()
    {
        Assert.AreEqual(VirtualKey.None, KeySender.ResolveKey(""));
        Assert.AreEqual(VirtualKey.None, KeySender.ResolveKey("   "));
        Assert.AreEqual(VirtualKey.None, KeySender.ResolveKey("NOT_A_KEY_ZZZ"));
    }
}
