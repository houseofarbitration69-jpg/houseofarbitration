using House.Of.Arbitration.Models;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class GenreToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Genre genre)
        {
            var result = genre != Genre.None;

            if (parameter as string == "Invert")
            {
                return !result;
            }
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
