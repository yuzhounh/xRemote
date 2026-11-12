using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace _1RM.View.Host;

public sealed class SessionTabWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        int count = values.Length > 1 && values[1] is int items ? Math.Max(1, items) : 1;
        double available = values.Length > 0 && values[0] is double width && double.IsFinite(width)
            ? Math.Max(0, width) : count * 280;
        return parameter as string == "Strip" ? Math.Min(available, count * 280) : Math.Min(280, available / count);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SessionTabContentConverter : IMultiValueConverter
{
    private const double ContentPadding = 8;
    private const double InactiveCloseWidth = 84;
    private const double CloseSize = 24;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double width = values.Length > 0 && values[0] is double actual && double.IsFinite(actual) ? Math.Max(0, actual) : 0;
        bool selected = values.Length > 1 && values[1] is true;
        bool enabled = values.Length < 4 || values[3] is true;
        bool closeEnabled = values.Length < 5 || values[4] is true;
        bool close = width > 0 && (selected || width >= InactiveCloseWidth);
        bool hasClose = close && closeEnabled;
        bool icon = enabled && width > 0 && (!hasClose || width - 2 * ContentPadding >= 16 + 16 + 4);
        double iconSize = Math.Min(16, width);
        double leading = hasClose ? Math.Min(ContentPadding, Math.Max(0, (width - CloseSize) / 2))
            : width < 32 ? (width - iconSize) / 2 : ContentPadding;
        double trailing = hasClose ? Math.Min(4, Math.Max(0, (width - CloseSize) / 2))
            : Math.Min(ContentPadding, Math.Max(0, (width - iconSize) / 2));
        if (parameter as string == "Margin")
            return new Thickness(leading, 5, trailing, 0);
        if (parameter as string == "TitleMargin")
            return new Thickness(icon ? 8 : 0, 0, 0, 0);
        if (parameter as string == "CloseSize")
            return Math.Min(CloseSize, width);
        if (parameter as string == "IconSize")
            return iconSize;
        bool visible = (parameter as string) switch
        {
            "Title" => width - leading - trailing - (icon ? iconSize + 8 : 0)
                - (hasClose ? Math.Min(CloseSize, width) : 0) - (width >= 140 ? 26 : 0) >= 20,
            "Icon" => icon,
            "Reconnect" => width >= 140,
            "Close" => close,
            _ => true
        };
        return (parameter as string == "Title" || enabled) && visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
