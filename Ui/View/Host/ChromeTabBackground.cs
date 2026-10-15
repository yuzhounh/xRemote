using System;
using System.Windows;
using System.Windows.Media;

namespace _1RM.View.Host
{
    /// <summary>Fixed-radius tab corners anchored to the original rectangular header edges.</summary>
    public sealed class ChromeTabBackground : FrameworkElement
    {
        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(ChromeTabBackground),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush? Fill
        {
            get => (Brush?)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public static readonly DependencyProperty ShowSeparatorProperty = DependencyProperty.RegisterAttached(
            "ShowSeparator", typeof(bool), typeof(ChromeTabBackground), new PropertyMetadata(true));

        public static bool GetShowSeparator(DependencyObject item) => (bool)item.GetValue(ShowSeparatorProperty);
        public static void SetShowSeparator(DependencyObject item, bool value) => item.SetValue(ShowSeparatorProperty, value);

        protected override void OnRender(DrawingContext drawingContext)
        {
            var width = RenderSize.Width;
            var height = RenderSize.Height;
            if (width <= 0 || height <= 0 || Fill == null) return;
            var top = Math.Min(10, Math.Min(width / 2, height / 2));
            var bottom = Math.Min(8, height / 2);
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                // The vertical segments are exactly x=0 and x=width. Only the feet extend outside.
                path.BeginFigure(new Point(-bottom, height), true, true);
                path.ArcTo(new Point(0, height - bottom), new Size(bottom, bottom), 0, false, SweepDirection.Counterclockwise, true, false);
                path.LineTo(new Point(0, top), true, false);
                path.ArcTo(new Point(top, 0), new Size(top, top), 0, false, SweepDirection.Clockwise, true, false);
                path.LineTo(new Point(width - top, 0), true, false);
                path.ArcTo(new Point(width, top), new Size(top, top), 0, false, SweepDirection.Clockwise, true, false);
                path.LineTo(new Point(width, height - bottom), true, false);
                path.ArcTo(new Point(width + bottom, height), new Size(bottom, bottom), 0, false, SweepDirection.Counterclockwise, true, false);
            }
            geometry.Freeze();
            drawingContext.DrawGeometry(Fill, null, geometry);
        }
    }
}
