using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WaifuAI.Converters;

public class EngagementToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int engagement)
        {
            return engagement switch
            {
                <= 30 => Brush.Parse("#aaa1ff"),
                <= 70 => Brush.Parse("#2f9e44"),
                _ => Brush.Parse("#ff9e37")
            };
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

