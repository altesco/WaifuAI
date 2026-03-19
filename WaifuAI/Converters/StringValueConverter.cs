using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia.Data.Converters;

namespace WaifuAI.Converters;

public class StringValueConverter : IValueConverter
{
    private readonly Dictionary<string, string> _names = new()
    {
        { "ru", "Русский" },
        { "en", "English" },
        { "de", "Deutsch" },
        { "es", "Español" },
        { "fr", "Français" },
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && _names.TryGetValue(key, out var displayName))
        {
            return displayName;
        }
        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}