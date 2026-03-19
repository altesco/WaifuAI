using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace WaifuAI.Converters;

public class MathConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double val = (double)value;
        double param = double.Parse(parameter.ToString());
        return val * param;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) 
        => throw new NotImplementedException();
}
