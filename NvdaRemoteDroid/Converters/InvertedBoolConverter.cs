using System.Globalization;

namespace NvdaRemoteDroid.Converters;

/// <summary>Inverte um bool — usado para esconder os campos de host/porta quando "usar relay público" está ativo.</summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}
