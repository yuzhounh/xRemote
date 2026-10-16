using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using _1RM.View.Utils;

namespace _1RM.View.Settings;

public partial class SettingsWindowView : Window
{
    private readonly Func<bool> _tryClose;
    private readonly NativeMainTitleBar _nativeTitleBar;

    public SettingsWindowView(SettingsPageViewModel model)
        : this(new SettingsPageView { DataContext = model }, model.TryFinishEditing)
    {
        model.CloseRequested += RequestClose;
        Closed += (_, _) => model.CloseRequested -= RequestClose;
    }

    internal SettingsWindowView(FrameworkElement content, Func<bool> tryClose)
    {
        InitializeComponent();
        _tryClose = tryClose;
        SettingsBody.Content = content;
        SettingsViewport.SizeChanged += (_, _) => SettingsBody.Width = Math.Max(880, SettingsViewport.ActualWidth);
        _nativeTitleBar = new NativeMainTitleBar(this, CaptionRow, CaptionArea, CaptionReserve, RequestClose);
        _ = new WindowWorkArea(this);
        Closing += OnSettingsClosing;
        Closed += (_, _) =>
        {
            if (content is SettingsPageView page) page.ReleaseContent();
            SettingsBody.Content = null;
        };
    }

    private void OnSettingsClosing(object? sender, CancelEventArgs e)
    {
        if (App.ExitingFlag) return;
        Keyboard.ClearFocus();
        var invalid = FindInvalidFontSize(SettingsBody);
        if (invalid != null)
        {
            e.Cancel = true;
            _1RM.Controls.EditorValidation.RevealErrors(VisualTreeHelper.GetParent(invalid) ?? invalid);
            invalid.BringIntoView();
            invalid.Focus();
            return;
        }
        if (!_tryClose()) e.Cancel = true;
    }

    private static FrameworkElement? FindInvalidFontSize(DependencyObject node)
    {
        if (node is UIElement { Visibility: not Visibility.Visible }) return null;
        if (node is FrameworkElement field && Validation.GetHasError(node))
            foreach (var error in Validation.GetErrors(node))
                if (error.RuleInError is _1RM.Controls.PositiveFontSizeRule) return field;
        if (node is not Visual) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindInvalidFontSize(VisualTreeHelper.GetChild(node, i)) is { } invalid) return invalid;
        return null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Let editing controls handle Escape before closing the window.
        if (e.Key != Key.Escape || e.Handled || Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true }) return;
        if (Keyboard.FocusedElement is TextBoxBase { IsReadOnly: false }) return;
        e.Handled = true;
        RequestClose();
    }

    private void RequestClose() => Close();
}
