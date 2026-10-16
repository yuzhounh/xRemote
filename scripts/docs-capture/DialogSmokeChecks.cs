using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using _1RM.View.Utils;

internal static class DialogSmokeChecks
{
    internal static void Run()
    {
        var passed = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed.Add(name); }
        MessageBoxPageViewModel Model(MessageBoxButton buttons, MessageBoxImage icon,
            IDictionary<MessageBoxResult,string>? labels = null)
        {
            var model = new MessageBoxPageViewModel();
            model.Setup("说明", "提示", buttons, icon, buttonLabels: labels);
            return model;
        }
        var info = Model(MessageBoxButton.OK, MessageBoxImage.Information);
        Check(!info.ShowButtons && info.CancelButton?.Value == MessageBoxResult.OK, "information closes with OK result without duplicate action");
        Check(!Model(MessageBoxButton.OK, MessageBoxImage.None).ShowButtons, "plain explanation hides duplicate action");
        Check(Model(MessageBoxButton.OK, MessageBoxImage.Warning).ShowButtons, "warning keeps acknowledgement");
        Check(Model(MessageBoxButton.OK, MessageBoxImage.Error).ShowButtons, "error keeps acknowledgement");
        var choices = Model(MessageBoxButton.YesNoCancel, MessageBoxImage.None);
        Check(choices.ShowButtons && choices.ButtonList?.Count == 3 && choices.CancelButton?.Value == MessageBoxResult.Cancel,
            "three-way confirmation keeps actions and cancel result");
        Check(Model(MessageBoxButton.OK, MessageBoxImage.None, new Dictionary<MessageBoxResult,string>{{MessageBoxResult.OK,"自定义操作"}}).ShowButtons,
            "custom action is preserved");
        var text = new TextBlock { Text = "短提示", TextWrapping = TextWrapping.Wrap };
        var body = new Border { Padding = new Thickness(24), Child = text };
        var window = new Window { Width = 560, MinWidth = 360, MaxWidth = 800, Content = body };
        CompactDialogSizing.Fit(window, body);
        Check(window.Width == 360, "short prompt shrinks to minimum width");
        text.Text = new string('测', 200);
        CompactDialogSizing.Fit(window, body);
        Check(window.Width == 640, $"long prompt respects maximum width (actual={window.Width}, desired={body.DesiredSize.Width}, workarea={SystemParameters.WorkArea.Width})");
        body.Measure(new Size(640, double.PositiveInfinity));
        Check(text.DesiredSize.Height > 30, "long content wraps within maximum width");
        window.Close();
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "dialog-checks.json"), System.Text.Json.JsonSerializer.Serialize(passed));
    }
}
