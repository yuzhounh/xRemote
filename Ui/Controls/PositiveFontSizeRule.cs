using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using _1RM.Service;

namespace _1RM.Controls;

public sealed class PositiveFontSizeRule : ValidationRule
{
    private static bool IsSupported(int value) => value > 0 && TextElement.FontSizeProperty.IsValidValue((double)value);
    internal static int Normalize(int value) => IsSupported(value) ? value : 14;

    public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
        int.TryParse(value?.ToString(), NumberStyles.Integer, cultureInfo, out var size) && IsSupported(size)
            ? ValidationResult.ValidResult
            : new ValidationResult(false, UiText.Get("Settings.PositiveFontSize"));
}
