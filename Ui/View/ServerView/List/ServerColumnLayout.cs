using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using _1RM.Service.Locality;

namespace _1RM.View.ServerView;

// Keep the existing width/visibility bindings while moving a column and all its cells together.
public static class ServerColumnLayout
{
    private static readonly string[] PhysicalKeys = { "Selector", "Status", "RemoteUsage", "Icon", "Name", "Note", "Address", "Protocol", "Tags", "Actions" };
    public static readonly string[] DefaultOrder = { "Status", "RemoteUsage", "Name", "Note", "Address", "Protocol", "Tags" };
    private static event Action? Changed;
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ServerColumnLayout), new PropertyMetadata(false, Enable));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(LayoutState), typeof(ServerColumnLayout));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    private static void Enable(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Grid grid || args.NewValue is not true) return;
        grid.Loaded += (_, _) =>
        {
            if (grid.GetValue(StateProperty) is not LayoutState state)
            {
                state = new LayoutState(grid); grid.SetValue(StateProperty, state);
            }
            Changed -= state.Apply; Changed += state.Apply; state.Apply();
        };
        grid.Unloaded += (_, _) => { if (grid.GetValue(StateProperty) is LayoutState state) Changed -= state.Apply; };
    }
    public static List<string> Order() => (LocalityListViewService.Settings.ColumnOrder ?? new List<string>())
        .Where(DefaultOrder.Contains).Concat(DefaultOrder).Distinct().ToList();
    public static void Move(string source, string target, bool after)
    {
        if (source == target || !DefaultOrder.Contains(source) || !DefaultOrder.Contains(target)) return;
        var order = Order(); order.Remove(source); order.Insert(order.IndexOf(target) + (after ? 1 : 0), source);
        LocalityListViewService.Settings.ColumnOrder = order; Changed?.Invoke();
    }
    public static void Reset()
    {
        LocalityListViewService.Settings.ColumnOrder = DefaultOrder.ToList();
        Changed?.Invoke(); LocalityListViewService.Save();
    }
    public static string? KeyAt(Grid grid, double x, out bool after)
    {
        after = false;
        if (grid.GetValue(StateProperty) is not LayoutState state) return null;
        double left = 0;
        for (int index = 0; index < grid.ColumnDefinitions.Count; index++)
        {
            double width = grid.ColumnDefinitions[index].ActualWidth;
            if (width > 0 && x >= left && x < left + width)
            {
                after = x > left + width / 2;
                var key = state.Current[index];
                return key == "Icon" ? "Name" : DefaultOrder.Contains(key) ? key : null;
            }
            left += width;
        }
        return null;
    }
    private sealed class LayoutState
    {
        private readonly Grid grid;
        private readonly Dictionary<string, ColumnDefinition> columns;
        private readonly Dictionary<UIElement, string> cells;
        public string[] Current = PhysicalKeys;
        public LayoutState(Grid grid)
        {
            this.grid = grid;
            columns = grid.ColumnDefinitions.Cast<ColumnDefinition>().Select((column, index) => (column, key: PhysicalKeys[index]))
                .ToDictionary(item => item.key, item => item.column);
            cells = grid.Children.Cast<UIElement>().ToDictionary(child => child, child => PhysicalKeys[Grid.GetColumn(child)]);
        }
        public void Apply()
        {
            Current = new[] { "Selector" }.Concat(Order().SelectMany(key => key == "Name" ? new[] { "Icon", "Name" } : new[] { key }))
                .Concat(new[] { "Actions" }).ToArray();
            grid.ColumnDefinitions.Clear();
            foreach (var key in Current) grid.ColumnDefinitions.Add(columns[key]);
            foreach (var cell in cells) Grid.SetColumn(cell.Key, Array.IndexOf(Current, cell.Value));
        }
    }
}
