using System.Globalization;
using System.Windows;
using _1RM.View.Host;

var converter = new SessionTabContentConverter();
object Value(double width, bool selected, string name, bool enabled = true, bool close = true, bool hovered = false) =>
    converter.Convert(new object[] { width, selected, hovered, enabled, close }, typeof(object), name, CultureInfo.InvariantCulture);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
foreach (var width in new double[] { 16, 24, 32, 48, 60, 84, 200 })
{
    Check((Visibility)Value(width, true, "Close") == Visibility.Visible, $"active {width}: close hidden");
    var padding = (Thickness)Value(width, true, "Margin");
    var closeWidth = (double)Value(width, true, "CloseSize");
    Check(padding.Left + padding.Right + closeWidth <= width, $"active {width}: close target escapes tab");
}
Check((Visibility)Value(60, false, "Close", hovered: true) == Visibility.Collapsed, "narrow inactive hover exposes close");
Check((Visibility)Value(84, false, "Close") == Visibility.Visible, "wide inactive close hidden");
Check((Visibility)Value(24, true, "Icon") == Visibility.Collapsed, "icon overlaps close-only tab");
Check((Visibility)Value(60, true, "Icon") == Visibility.Visible, "icon hidden when both controls fit");
Check((Visibility)Value(60, true, "Close", enabled: false) == Visibility.Collapsed, "close preference ignored");
Check((Visibility)Value(48, true, "Icon", close: false) == Visibility.Visible, "hidden close still consumes icon space");
var margin = (Thickness)Value(200, true, "Margin");
Check(margin.Left == 8 && margin.Right == 4, "content does not use Chrome body-edge spacing");
Console.WriteLine("PASS: close priority, inactive threshold, icon collision and content spacing");
