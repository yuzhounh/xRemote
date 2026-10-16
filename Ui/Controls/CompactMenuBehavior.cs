using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace _1RM.Controls;

// These first-level entries live in a Popup/StackPanel, not a MenuBase.
// Explicit opening keeps submenu behavior independent of the default Menu template.
public static class CompactMenuBehavior
{
    // Measure the real localized templates, including checkmarks and shortcuts.
    // Both popup shells have the same padding, so equal row widths give equal cards.
    public static void SynchronizeWidths(Panel panel)
    {
        var roots = panel.Children.OfType<MenuItem>().ToArray();
        var entries = roots.Concat(roots.SelectMany(root => root.Items.OfType<MenuItem>())).ToArray();
        foreach (var entry in entries) entry.ClearValue(FrameworkElement.MinWidthProperty);
        double width = 0;
        foreach (var entry in entries)
        {
            entry.ApplyTemplate();
            entry.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width = System.Math.Max(width, entry.DesiredSize.Width);
        }
        foreach (var entry in entries) entry.MinWidth = System.Math.Ceiling(width);
    }

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CompactMenuBehavior),
        new PropertyMetadata(false, (d, e) =>
        {
            if (d is not MenuItem item || !(bool)e.NewValue) return;
            item.MouseEnter += (_, _) => Open(item);
            item.PreviewMouseLeftButtonDown += (_, args) => { if (item.HasItems && BelongsTo(item, args.OriginalSource)) { item.Focus(); Open(item); args.Handled = true; } };
            item.PreviewKeyDown += (_, args) =>
            {
                if (item.HasItems && BelongsTo(item, args.OriginalSource) && args.Key is Key.Right or Key.Enter or Key.Space) { Open(item); args.Handled = true; }
            };
        }));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    private static bool BelongsTo(MenuItem item, object source)
    {
        for (var current = source as DependencyObject; current != null; current = current is System.Windows.Media.Visual
                 ? System.Windows.Media.VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is MenuItem nearest) return nearest == item;
        return false;
    }
    private static void Open(MenuItem item)
    {
        if (item.Parent is Panel panel)
            foreach (var sibling in panel.Children.OfType<MenuItem>().Where(s => s != item)) sibling.IsSubmenuOpen = false;
        if (item.HasItems) item.IsSubmenuOpen = true;
    }
}
