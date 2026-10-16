using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace _1RM.Controls
{
    public class GridWith4Columns : Grid
    {
        public static readonly DependencyProperty InputWidthProperty = DependencyProperty.Register(nameof(InputWidth), typeof(double), typeof(GridWith4Columns),
            new PropertyMetadata(300.0, (d, e) => ((GridWith4Columns)d).ColumnDefinitions[1].Width = new GridLength((double)e.NewValue)));
        public double InputWidth { get => (double)GetValue(InputWidthProperty); set => SetValue(InputWidthProperty, value); }
        public GridWith4Columns()
        {
            //ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            //ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            //ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            //ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto, SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(300, GridUnitType.Pixel), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto, SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star)});
        }
    }
}
