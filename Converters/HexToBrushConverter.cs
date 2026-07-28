using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeyAutomator.Converters;

/// <summary>#RRGGBB / #AARRGGBB を SolidColorBrush に変換</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));

        hex = hex.TrimStart('#');
        byte a = 255, r, g, b;
        if (hex.Length == 6)
        {
            r = System.Convert.ToByte(hex[..2], 16);
            g = System.Convert.ToByte(hex[2..4], 16);
            b = System.Convert.ToByte(hex[4..6], 16);
        }
        else if (hex.Length == 8)
        {
            a = System.Convert.ToByte(hex[..2], 16);
            r = System.Convert.ToByte(hex[2..4], 16);
            g = System.Convert.ToByte(hex[4..6], 16);
            b = System.Convert.ToByte(hex[6..8], 16);
        }
        else
        {
            return new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
        }

        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
