using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class EmptyToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = !string.IsNullOrWhiteSpace(value as string) && !(value is null);
        
        if (parameter as string == "Invert")
        {
            return !hasValue;
        }

        return hasValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
