using System;
using Avalonia;
using Avalonia.Media;

namespace WaifuAI.Services;

public static class Helper
{
    public static int GetAge(DateOnly birthday)
    {
        var today = DateTime.Today;
        var age = today.Year - birthday.Year;
        if (birthday.Month > today.Month ||
            (birthday.Month == today.Month && birthday.Day > today.Day))
            age--;
        return age;
    }

    public static IBrush GetThemeResource(string key, IBrush fallback)
    {
        if (Application.Current != null && Application.Current.TryGetResource(key, null, out var resource))
        {
            if (resource is IBrush brush)
                return brush;

            if (resource is Color color)
                return new SolidColorBrush(color);
        }

        return fallback;
    }
}