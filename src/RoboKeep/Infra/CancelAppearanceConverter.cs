using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace RoboKeep.Infra;

/// <summary>
/// true → <see cref="ControlAppearance.Danger"/> (pulsante in modalità "Annulla");
/// false → l'aspetto normale passato come ConverterParameter (es. Primary/Secondary).
/// </summary>
public sealed class CancelAppearanceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return ControlAppearance.Danger;
        return Enum.TryParse<ControlAppearance>(parameter as string ?? "Secondary", out var a)
            ? a
            : ControlAppearance.Secondary;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
