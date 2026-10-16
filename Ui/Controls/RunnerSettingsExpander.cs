using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace _1RM.Controls;

/// <summary>View-only expansion state; it never changes the configured runner.</summary>
public sealed class RunnerSettingsExpander : Expander
{
    public static readonly DependencyProperty RunnerProperty = DependencyProperty.Register(
        nameof(Runner), typeof(object), typeof(RunnerSettingsExpander), new PropertyMetadata(null, SelectionChanged));
    public static readonly DependencyProperty SelectedRunnerProperty = DependencyProperty.Register(
        nameof(SelectedRunner), typeof(object), typeof(RunnerSettingsExpander), new PropertyMetadata(null, SelectionChanged));

    public object? Runner { get => GetValue(RunnerProperty); set => SetValue(RunnerProperty, value); }
    public object? SelectedRunner { get => GetValue(SelectedRunnerProperty); set => SetValue(SelectedRunnerProperty, value); }

    public RunnerSettingsExpander() => Loaded += (_, _) => ApplySelection();

    private static void SelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((RunnerSettingsExpander)sender).ApplySelection();

    private void ApplySelection()
    {
        SetCurrentValue(IsExpandedProperty, Runner != null && ReferenceEquals(Runner, SelectedRunner));
        if (!IsLoaded || !IsExpanded) return;
        // Keep the newly selected program's heading in view, including long external forms.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (IsLoaded && IsExpanded && ReferenceEquals(Runner, SelectedRunner))
                BringIntoView(new Rect(0, 0, ActualWidth, Math.Min(40, ActualHeight)));
        }));
    }
}
