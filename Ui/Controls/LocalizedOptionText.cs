using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace _1RM.Controls;

// Translate display text while keeping the saved preset/runner identifiers unchanged.
public sealed class LocalizedOptionText : TextBlock
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(LocalizedOptionText), new PropertyMetadata("", Update));
    public static readonly DependencyProperty ResourcePrefixProperty = DependencyProperty.Register(
        nameof(ResourcePrefix), typeof(string), typeof(LocalizedOptionText), new PropertyMetadata("", Update));
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string ResourcePrefix { get => (string)GetValue(ResourcePrefixProperty); set => SetValue(ResourcePrefixProperty, value); }
    private static void Update(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var label = (LocalizedOptionText)obj;
        label.ClearValue(TextProperty);
        label.Inlines.Clear();
        if (label.ResourcePrefix is "ThemePreset." or "Settings.FontSize.")
            label.SetResourceReference(TextProperty, label.ResourcePrefix + label.Value);
        else if (label.ResourcePrefix == "DataSourceName." && label.Value == "Local")
            label.SetResourceReference(TextProperty, "Settings.Local");
        else if (label.ResourcePrefix == "RunnerName." && label.Value.StartsWith("Built-in ", StringComparison.Ordinal))
        {
            var prefix = new Run();
            prefix.SetResourceReference(Run.TextProperty, "Settings.BuiltIn");
            label.Inlines.Add(prefix);
            label.Inlines.Add(new Run(" " + label.Value.Substring("Built-in ".Length)));
        }
        else label.Text = label.Value;
    }
}
