using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WaifuAI.Converters;

public class EnergyToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int energy)
        {
            return energy switch
            {
                <= 20 => Brush.Parse("#ff3a3a"),
                <= 50 => Brush.Parse("#ffa11d"),
                _ => Brush.Parse("#fff04b")
            };
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

