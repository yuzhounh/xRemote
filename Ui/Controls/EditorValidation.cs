using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace _1RM.Controls;

/// <summary>Editor-only validation: one thin outline and a real layout row below each edited field.</summary>
public static class EditorValidation
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(EditorValidation),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static readonly DependencyProperty TouchedProperty = DependencyProperty.RegisterAttached("Touched", typeof(bool), typeof(EditorValidation), new PropertyMetadata(false));
    public static void SetTouched(DependencyObject target, bool value) => target.SetValue(TouchedProperty, value);
    public static bool GetTouched(DependencyObject target) => (bool)target.GetValue(TouchedProperty);
    private static readonly ConditionalWeakTable<FrameworkElement, object> Prepared = new();
    static EditorValidation() => EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
        new RoutedEventHandler((sender, eventArgs) =>
        {
            if (sender is not FrameworkElement field || !GetEnabled(field) || Prepared.TryGetValue(field, out _)) return;
            if (field is not (System.Windows.Controls.Primitives.TextBoxBase or ComboBox or PasswordInput or AutoCompleteComboBox or BindableAvalonEditor)
                || field is TextBox { IsReadOnly: true }) return;
            for (DependencyObject? ancestor = VisualTreeHelper.GetParent(field); ancestor != null; ancestor = VisualTreeHelper.GetParent(ancestor))
                if (ancestor is ComboBox or PasswordInput or AutoCompleteComboBox or BindableAvalonEditor) return;
            var values = field.GetLocalValueEnumerator(); bool validated = false;
            while (values.MoveNext())
            {
                var binding = BindingOperations.GetBinding(field, values.Current.Property);
                if (values.Current.Property.Name is "Text" or "SelectedItem" or "SelectedValue" or "Password" && binding != null
                    && (binding.ValidatesOnDataErrors || binding.ValidatesOnNotifyDataErrors || binding.ValidatesOnExceptions))
                { validated = true; break; }
            }
            if (!validated) return;
            // Hide the legacy overlay immediately, including untouched required fields.
            Validation.SetErrorTemplate(field, null);
            field.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => Wrap(field)));
        }), true);
    private static void Wrap(FrameworkElement field)
    {
        if (Prepared.TryGetValue(field, out _) || field.Parent is not Panel parent) return;
        Prepared.Add(field, new object());
        var caption = FindCaption(field);
        var host = new ValidationField(field, caption) { Margin = field.Margin,
            HorizontalAlignment = field.HorizontalAlignment, VerticalAlignment = field.VerticalAlignment };
        field.Margin = new Thickness(0);
        field.HorizontalAlignment = HorizontalAlignment.Stretch; field.VerticalAlignment = VerticalAlignment.Top;
        foreach (var property in new[] { Grid.RowProperty, Grid.ColumnProperty, Grid.RowSpanProperty, Grid.ColumnSpanProperty, DockPanel.DockProperty })
        {
            host.SetValue(property, field.GetValue(property)); field.ClearValue(property);
        }
        int index = parent.Children.IndexOf(field);
        parent.Children.RemoveAt(index); host.Attach(); parent.Children.Insert(index, host);
    }
    private static TextBlock? FindCaption(FrameworkElement field)
    {
        FrameworkElement current = field;
        for (int i = 0; i < 5 && current.Parent is FrameworkElement parent; i++, current = parent)
        {
            if (parent is not Panel panel) continue;
            var label = panel.Children.Cast<UIElement>().TakeWhile(c => c != current).OfType<TextBlock>()
                .LastOrDefault(t => !string.IsNullOrWhiteSpace(t.Text));
            if (label != null) return label;
        }
        return null;
    }
    public static void RevealErrors(DependencyObject root)
    {
        if (root is ValidationField field) field.Reveal();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) RevealErrors(VisualTreeHelper.GetChild(root, i));
    }
    private sealed class ValidationField : Grid
    {
        private readonly FrameworkElement field;
        private readonly TextBlock error;
        private readonly TextBlock? caption;
        private readonly Thickness captionMargin;
        private readonly VerticalAlignment captionAlignment;
        private bool touched;
        private static readonly ControlTemplate ThinOutline = MakeOutline();
        private static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register("HasError", typeof(bool), typeof(ValidationField),
            new PropertyMetadata(false, (d, _) => ((ValidationField)d).Refresh()));
        public ValidationField(FrameworkElement field, TextBlock? caption)
        {
            this.field = field;
            this.caption = caption;
            if (caption != null) { captionMargin = caption.Margin; captionAlignment = caption.VerticalAlignment; }
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
                FontSize = 12, Visibility = Visibility.Collapsed, IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top, TextAlignment = TextAlignment.Left };
            error.SetResourceReference(TextBlock.ForegroundProperty, "ValidationErrorBrush");
            error.SetBinding(TextBlock.TextProperty, new Binding("(Validation.Errors)[0].ErrorContent") { Source = field,
                Converter = new ErrorText(caption?.Text.Trim() ?? (Application.Current?.TryFindResource("Editor.Field") as string ?? "value")), FallbackValue = "", TargetNullValue = "" });
            SetRow(error, 1);
            field.LostKeyboardFocus += (_, _) => field.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() => { if (!field.IsKeyboardFocusWithin) Reveal(); }));
        }
        public void Attach()
        {
            Children.Add(field); Children.Add(error);
            SetBinding(HasErrorProperty, new Binding("(Validation.HasError)") { Source = field });
        }
        public void Reveal() { touched = true; SetTouched(field, true); Refresh(); }
        private void Refresh()
        {
            bool show = touched && (bool)GetValue(HasErrorProperty);
            error.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (caption != null)
            {
                caption.VerticalAlignment = show ? VerticalAlignment.Top : captionAlignment;
                caption.Margin = show ? new Thickness(captionMargin.Left,
                    Math.Max(0, ((field.ActualHeight > 0 ? field.ActualHeight : 30) - Math.Max(16, caption.ActualHeight)) / 2), captionMargin.Right, captionMargin.Bottom) : captionMargin;
            }
            Validation.SetErrorTemplate(field, show ? ThinOutline : null);
            InvalidateMeasure();
        }
        private static ControlTemplate MakeOutline()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetResourceReference(Border.BorderBrushProperty, "ValidationErrorBrush");
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            border.AppendChild(new FrameworkElementFactory(typeof(AdornedElementPlaceholder)));
            return new ControlTemplate { VisualTree = border };
        }
    }
    private sealed class ErrorText(string caption) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value?.ToString() ?? "";
            if (text.Contains("不能为空") || text.Contains("empty", StringComparison.OrdinalIgnoreCase))
                return string.Format(Application.Current?.TryFindResource("Editor.EnterField") as string ?? "Enter {0}", caption);
            return text;
        }
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
