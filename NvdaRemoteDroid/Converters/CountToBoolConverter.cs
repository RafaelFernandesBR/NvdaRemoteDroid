using System.Globalization;

namespace NvdaRemoteDroid.Converters;

/// <summary>Converte uma contagem (int) em bool: true se > 0. ConverterParameter="Invert" inverte o resultado.</summary>
public sealed class CountToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasItems = value is int count && count > 0;
        return string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase) ? !hasItems : hasItems;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
