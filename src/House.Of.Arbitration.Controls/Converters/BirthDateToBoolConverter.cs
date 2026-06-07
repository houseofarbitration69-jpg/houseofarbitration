using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class BirthDateToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime date)
        {
            var result = date < DateTime.Now.AddYears(-4);

            if (parameter as string == "Invert")
            {
                return !result;
            }

            return result;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
