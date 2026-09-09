using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = !string.IsNullOrWhiteSpace(value as string);
        bool hasParameter = !string.IsNullOrWhiteSpace(parameter as string);

        if (hasValue && hasParameter)
        {
            return String.Compare(value as string, parameter as string, true) == 0;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
