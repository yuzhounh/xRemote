using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace _1RM.Controls;

// Keep header dividers one physical pixel wide, including on scaled displays.
public sealed class PixelHeaderBorder : Border
{
    public PixelHeaderBorder()
    {
        BorderBrush = new SolidColorBrush(Color.FromRgb(208, 208, 208));
        SnapsToDevicePixels = true;
        BorderThickness = new Thickness(0, 0, 0, 1);
        Loaded += (_, _) => UpdateThickness(VisualTreeHelper.GetDpi(this));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateThickness(newDpi);
    }

    private void UpdateThickness(DpiScale dpi)
        => BorderThickness = new Thickness(0, 0, 0, 1 / dpi.DpiScaleY);
}
