using System.Windows;
using System.Windows.Controls;

namespace _1RM.Controls;

/// <summary>The shared icon, typography and spacing for all application captions.</summary>
public sealed class WindowCaptionTitle : Grid
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(WindowCaptionTitle), new PropertyMetadata(""));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public WindowCaptionTitle()
    {
        VerticalAlignment = VerticalAlignment.Center;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Children.Add(new AppLogo { Width = 24, Height = 24, Margin = new Thickness(5) });
        var title = new TextBlock { FontSize = 16, FontWeight = FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Left, MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis };
        title.SetResourceReference(TextBlock.FontFamilyProperty, "GlobalFontFamily");
        title.SetResourceReference(StyleProperty, "TextBlockBase");
        title.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Text)) { Source = this });
        SetColumn(title, 1); Children.Add(title);
    }
}
