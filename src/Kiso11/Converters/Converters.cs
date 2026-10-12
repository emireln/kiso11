using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Kiso11.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            if (Inverse) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// Shows an element when the current step is past the step given as the parameter. Set Inverse to show it when the step is not yet past.
public class StepPastConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var current = value is int step ? step : 1;
        var target = int.TryParse(parameter?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        var isPast = current > target;
        if (Inverse) isPast = !isPast;
        return isPast ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
