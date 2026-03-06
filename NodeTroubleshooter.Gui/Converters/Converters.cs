using System.Globalization;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using NodeTroubleshooter.Model;

namespace NodeTroubleshooter.Gui.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value switch
        {
            bool b => b,
            int i => i > 0,
            _ => false
        };
        bool invert = parameter is string s && s == "Invert";
        if (invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class SymptomDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SymptomSpec s)
        {
            var tag = s.DomainConfidence.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                ? "  [Stage 0]" : "";
            return $"{s.Title} ({s.Code}){tag}";
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ActionDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ActionSpec a)
            return a.Description;
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NodeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NodeSpec n)
            return $"{n.Gen} — {n.Platform} ({n.Label})";
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns Visible when the integer value is 0 (empty text), Collapsed otherwise.
/// Used for watermark/placeholder text overlays on TextBox controls.
/// </summary>
public class InverseLengthToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int length)
            return length == 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
