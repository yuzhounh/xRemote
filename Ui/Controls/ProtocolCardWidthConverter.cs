using System;
using System.Globalization;
using System.Windows.Data;

namespace _1RM.Controls;

public sealed class ProtocolCardWidthConverter : IValueConverter
{
    public static double Calculate(double width) => Math.Min(580, Math.Max(0, width - 30));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double width && width > 0 ? Calculate(width) : 580.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
