using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WaifuAI.Services;

namespace WaifuAI.Converters;

public class MoodToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int mood)
        {
            return mood switch
            {
                <= 25 => Brush.Parse("#ff3a3a"),
                <= 65 => Helper.GetThemeResource("BaseHigh", Brushes.Gray),
                _ => Brush.Parse("#2dc863")
            };
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

