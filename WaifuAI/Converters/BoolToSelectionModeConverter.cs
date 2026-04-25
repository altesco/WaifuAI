using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace WaifuAI.Converters
{
    public class BoolToSelectionModeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isMultiSelect)
            {
                return isMultiSelect ? SelectionMode.Multiple | SelectionMode.Toggle : SelectionMode.Single;
            }
            return SelectionMode.Single;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}