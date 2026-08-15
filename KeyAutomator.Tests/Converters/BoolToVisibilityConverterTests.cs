using Microsoft.UI.Xaml;
using KeyAutomator.Converters;

namespace KeyAutomator.Tests.Converters;

[TestClass]
public class BoolToVisibilityConverterTests
{
    [TestMethod]
    public void Convert_True_ReturnsVisible()
    {
        var converter = new BoolToVisibilityConverter();
        Assert.AreEqual(Visibility.Visible, converter.Convert(true, typeof(Visibility), null!, "ja"));
        Assert.AreEqual(Visibility.Collapsed, converter.Convert(false, typeof(Visibility), null!, "ja"));
    }

    [TestMethod]
    public void Convert_Invert_FlipsResult()
    {
        var converter = new BoolToVisibilityConverter { Invert = true };
        Assert.AreEqual(Visibility.Collapsed, converter.Convert(true, typeof(Visibility), null!, "ja"));
        Assert.AreEqual(Visibility.Visible, converter.Convert(false, typeof(Visibility), "Invert", "ja"));
    }

    [TestMethod]
    public void Convert_ParameterInvert_FlipsResult()
    {
        var converter = new BoolToVisibilityConverter();
        Assert.AreEqual(Visibility.Collapsed, converter.Convert(true, typeof(Visibility), "Invert", "en"));
    }

    [TestMethod]
    public void ConvertBack_Visible_ReturnsTrue()
    {
        var converter = new BoolToVisibilityConverter();
        Assert.AreEqual(true, converter.ConvertBack(Visibility.Visible, typeof(bool), null!, "ja"));
        Assert.AreEqual(false, converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, "ja"));
    }
}
