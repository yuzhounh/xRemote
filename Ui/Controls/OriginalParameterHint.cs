using System.Windows;
using _1RM.Service;

namespace _1RM.Controls;

// Keep exact identifiers available, with a localized explanation rather than a bare token.
public static class OriginalParameterHint
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached(
        "Value", typeof(object), typeof(OriginalParameterHint), new PropertyMetadata(null, OnValueChanged));
    public static object? GetValue(DependencyObject target) => target.GetValue(ValueProperty);
    public static void SetValue(DependencyObject target, object? value) => target.SetValue(ValueProperty, value);

    private static void OnValueChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement element) return;
        if (args.NewValue == null) element.ClearValue(FrameworkElement.ToolTipProperty);
        else UiText.Bind(element, FrameworkElement.ToolTipProperty, "Ui.OriginalParameterHint", args.NewValue);
    }
}
