using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using _1RM.Service.Locality;

namespace _1RM.View.ServerView;

public partial class ServerListPageView
{
    private Point columnPress;
    private string? movingColumn;
    private bool draggingColumn;
    private void ColumnHeaders_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Thumb || Shawn.Utils.Wpf.MyVisualTreeHelper.VisualUpwardSearch<Thumb>(e.OriginalSource as DependencyObject) != null) return;
        columnPress = e.GetPosition(ColumnHeaders);
        movingColumn = ServerColumnLayout.KeyAt(ColumnHeaders, columnPress.X, out _);
    }
    private void ColumnHeaders_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (movingColumn == null || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(ColumnHeaders);
        if (!draggingColumn && Math.Abs(point.X - columnPress.X) < SystemParameters.MinimumHorizontalDragDistance) return;
        if (!draggingColumn)
        {
            draggingColumn = true;
            Mouse.Capture(ColumnHeaders); ColumnHeaders.Cursor = Cursors.SizeAll;
        }
        var target = ServerColumnLayout.KeyAt(ColumnHeaders, point.X, out bool after);
        if (target != null) ServerColumnLayout.Move(movingColumn, target, after);
        e.Handled = true;
    }
    private void ColumnHeaders_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        movingColumn = null;
        if (!draggingColumn) return;
        draggingColumn = false; ColumnHeaders.ReleaseMouseCapture(); ColumnHeaders.Cursor = Cursors.Hand;
        LocalityListViewService.Save(); e.Handled = true;
    }
    private void ColumnHeaders_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!draggingColumn || ColumnHeaders.IsMouseCaptured) return;
        movingColumn = null; draggingColumn = false; ColumnHeaders.Cursor = Cursors.Hand;
        LocalityListViewService.Save();
    }
}
