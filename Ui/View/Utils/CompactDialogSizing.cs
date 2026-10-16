using System;
using System.Windows;

namespace _1RM.View.Utils;

// Measure only the body: native caption controls must not force short prompts wide.
internal static class CompactDialogSizing
{
    internal static void Fit(Window window, FrameworkElement body)
    {
        double frame = window.Content is FrameworkElement root
            ? Math.Max(0, window.ActualWidth - root.ActualWidth) : 0;
        double maximum = Math.Min(640, Math.Min(window.MaxWidth, SystemParameters.WorkArea.Width - 32));
        double minimum = Math.Min(window.MinWidth, maximum);
        body.InvalidateMeasure();
        body.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = Math.Clamp(body.DesiredSize.Width + frame, minimum, maximum);
        double left = window.Left + (window.ActualWidth - width) / 2;
        window.Width = width;
        if (double.IsFinite(left)) window.Left = left;
        window.UpdateLayout();
    }
}
