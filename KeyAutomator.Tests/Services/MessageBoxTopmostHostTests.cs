using KeyAutomator.Services;

namespace KeyAutomator.Tests.Services;

[TestClass]
public class MessageBoxTopmostHostTests
{
    [TestMethod]
    public void FindVisibleDialog_WhenNoDialog_ReturnsZero()
    {
        var hwnd = MessageBoxTopmostHost.FindVisibleDialog(
            (uint)Environment.ProcessId,
            "KeyAutomator-NoSuchDialog-" + Guid.NewGuid().ToString("N"));

        Assert.AreEqual(IntPtr.Zero, hwnd);
    }

    [TestMethod]
    public void PromoteToTopmost_WithZeroHandle_DoesNotThrow()
    {
        MessageBoxTopmostHost.PromoteToTopmost(IntPtr.Zero);
    }
}
