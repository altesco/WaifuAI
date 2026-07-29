using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WaifuAI.Services;

namespace WaifuAI.Converters;

public class AffectionToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int affection)
        {
            return affection switch
            {
                <= 25 => Helper.GetThemeResource("BaseHigh", Brushes.Gray),
                <= 50 => Brush.Parse("#3a85ff"),
                <= 75 => Brush.Parse("#aa4eff"),
                _ => Brush.Parse("#ff3737")
            };
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

