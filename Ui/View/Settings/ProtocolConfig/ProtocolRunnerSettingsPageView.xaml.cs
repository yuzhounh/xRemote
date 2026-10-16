using System.Windows;
using System.Windows.Controls;
using System;
using _1RM.Controls;
using _1RM.Service;
using _1RM.Service.DataSource;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf.Controls;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class ProtocolRunnerSettingsPageView : UserControl
    {
        public ProtocolRunnerSettingsPageView()
        {
            InitializeComponent();
            RunnersList.LayoutUpdated += (_, _) => AlignDefaultRunnerRow();
        }

        private void AlignDefaultRunnerRow()
        {
            // Use the actual item viewport, so the pinned row follows the form
            // even when a scrollbar appears or a narrower window is used.
            if (RunnersList.ItemContainerGenerator.ContainerFromIndex(0) is not FrameworkElement { ActualWidth: > 0 } item) return;
            double width = ProtocolCardWidthConverter.Calculate(item.ActualWidth);
            double left = item.TranslatePoint(new Point(), RunnerArea).X + (item.ActualWidth - width) / 2;
            if (Math.Abs(DefaultRunnerRow.Width - width) < .1 && Math.Abs(DefaultRunnerRow.Margin.Left - left) < .1) return;
            DefaultRunnerRow.Width = width;
            DefaultRunnerRow.Margin = new Thickness(left, 3, 0, 3);
        }
    }
}
