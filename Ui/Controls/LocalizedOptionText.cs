using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using _1RM.Service;

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
        else if (label.ResourcePrefix == "ArgumentType.")
            label.SetResourceReference(TextProperty, label.ResourcePrefix + label.Value);
        else if (label.ResourcePrefix == "TerminalTheme." && label.Value == "Default")
            label.SetResourceReference(TextProperty, "TerminalTheme.Default");
        else if (label.ResourcePrefix == "Serial." && label.Value is "NONE" or "ODD" or "EVEN" or "MARK" or "SPACE")
            label.SetResourceReference(TextProperty, "Serial." + label.Value);
        else if (label.ResourcePrefix == "SerialFlow." && label.Value is "NONE" or "XON/XOFF" or "RTS/CTS" or "DSR/DTR")
            label.SetResourceReference(TextProperty, "SerialFlow." + label.Value);
        else if (label.ResourcePrefix == "Encoding.")
        {
            var name = label.Value == "Use font encoding" ? "Font" : label.Value.Split(' ', ':')[0];
            var key = "Encoding." + name;
            if (label.TryFindResource(key) != null) label.SetResourceReference(TextProperty, key);
            else label.Text = label.Value;
        }
        else if (label.ResourcePrefix == "DatabaseType.")
            label.Text = label.Value switch { "Sqlite" => "SQLite", "MySql" => "MySQL", "SqlServer" => "SQL Server", _ => label.Value };
        else if (label.ResourcePrefix == "DataSourceName." && label.Value == "Local")
            label.SetResourceReference(TextProperty, "Settings.Local");
        else if (label.ResourcePrefix == "RunnerName." && label.Value == "Follow the global settings")
            label.SetResourceReference(TextProperty, "Editor.FollowGlobal");
        else if (label.ResourcePrefix == "RunnerName." && label.Value.StartsWith("Built-in ", StringComparison.Ordinal))
        {
            var prefix = new Run();
            prefix.SetResourceReference(Run.TextProperty, "Settings.BuiltIn");
            label.Inlines.Add(prefix);
            label.Inlines.Add(new Run(" " + label.Value.Substring("Built-in ".Length)));
        }
        else if (label.ResourcePrefix == "RunnerName.")
            label.Text = label.Value switch { "Putty" => "PuTTY", "Kitty" => "KiTTY", _ => label.Value };
        else label.Text = label.Value;
        if (label.ResourcePrefix is "Serial." or "SerialFlow." or "Encoding." or "ArgumentType.")
            UiText.BindComputed(label, ToolTipProperty, () => UiText.Get("Ui.OptionParameterHint", label.Text, label.Value));
    }
}
