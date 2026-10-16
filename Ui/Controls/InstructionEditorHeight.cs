using System;
using System.Globalization;
using System.Windows.Data;

namespace _1RM.Controls;

public sealed class InstructionEditorHeight : IValueConverter
{
    public object Convert(object value, Type type, object parameter, CultureInfo culture) => Math.Clamp(24 + (value is int lines ? lines : 1) * 18, 64, 160);
    public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
