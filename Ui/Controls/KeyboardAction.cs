using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace _1RM.Controls;

// Opt in only actions that need keyboard navigation; retain their existing style.
public static class KeyboardAction
{
    private static readonly Style FocusRing = CreateFocusRing();
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(KeyboardAction), new PropertyMetadata(false, (d, e) =>
        {
            if (d is Button button && e.NewValue is true) Enable(button);
        }));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);

    internal static void Enable(Button button)
    {
        button.Focusable = true;
        button.IsTabStop = true;
        button.FocusVisualStyle = FocusRing;
    }

    internal static void EnableButtons(DependencyObject root, bool defaultSave = false)
    {
        if (root is Button button)
        {
            Enable(button);
            if (defaultSave && button.GetBindingExpression(Button.CommandProperty)?.ParentBinding.Path?.Path == "CmdSave")
                button.IsDefault = true;
        }
        foreach (var child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject dependency) EnableButtons(dependency, defaultSave);
    }

    private static Style CreateFocusRing()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(FrameworkElement.MarginProperty, new Thickness(-2));
        border.SetResourceReference(Border.BorderBrushProperty, "PrimaryTextBrush");
        var template = new ControlTemplate(typeof(Control)) { VisualTree = border };
        var style = new Style(typeof(Control));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }
}
