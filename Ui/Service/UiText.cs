using System;
using System.Globalization;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace _1RM.Service;

internal static class UiText
{
    private static readonly List<WeakReference<LiveText>> Bindings = new();
    static UiText() => _1RM.Model.GlobalEventHelper.OnLanguageChanged += () =>
    {
        Bindings.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in Bindings.ToArray())
            if (reference.TryGetTarget(out var text)) text.Refresh();
    };
    public static string Get(string key, params object[] arguments)
    {
        var text = Application.Current?.TryFindResource(key) as string ?? key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, arguments);
    }

    // Weak sources refresh cached menu/window labels without retaining their controls.
    public static void Bind(FrameworkElement target, DependencyProperty property, string key, params object[] arguments)
    {
        BindComputed(target, property, () => Get(key, arguments));
    }

    public static void BindComputed(FrameworkElement target, DependencyProperty property, Func<string> text)
    {
        var source = new LiveText(text);
        Bindings.RemoveAll(reference => !reference.TryGetTarget(out _));
        Bindings.Add(new WeakReference<LiveText>(source));
        target.SetBinding(property, new Binding(nameof(LiveText.Text)) { Source = source, Mode = BindingMode.OneWay });
    }

    private sealed class LiveText(Func<string> text) : INotifyPropertyChanged
    {
        public string Text => text();
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
    }
}
