using System.Globalization;
using System.Windows.Data;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;

namespace ScreenApp.UI;

/// <summary>
/// Конвертер bool → кисть для индикаторов диагностики: true → зелёный, false → красный.
/// </summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    private static readonly Brush Ok = new SolidColorBrush(Color.FromRgb(0x4C, 0xD1, 0x37));
    private static readonly Brush Fail = new SolidColorBrush(Color.FromRgb(0xE0, 0x4B, 0x4B));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Ok : Fail;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
