using System.Globalization;

namespace NvdaRemoteDroid.Converters;

/// <summary>Liga um RadioButton.IsChecked a um valor específico de enum (ConverterParameter = nome do valor).</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string target && value.ToString() == target;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string target && targetType.IsEnum)
            return Enum.Parse(targetType, target);

        return BindableProperty.UnsetValue;
    }
}
