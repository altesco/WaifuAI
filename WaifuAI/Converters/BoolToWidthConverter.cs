using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace WaifuAI.Converters;

public class BoolToWidthConverter : IValueConverter
{
    public GridLength TrueValue { get; set; } = new GridLength(256);
    
    public GridLength FalseValue { get; set; } = new GridLength(0);
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? TrueValue : FalseValue;
        }
        return FalseValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}