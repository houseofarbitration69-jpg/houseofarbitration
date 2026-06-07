using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class GenreToFontConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Genre genre)
        {
            var result = genre != Genre.None;

            switch (genre)
            {
                case Genre.None:
                    return FontCustomIcons.MAN;
                case Genre.Men:
                    return FontCustomIcons.MAN;
                case Genre.Women:
                    return FontCustomIcons.WOMAN;
                case Genre.Mixte:
                    return FontCustomIcons.MAN;
                default:
                    return FontCustomIcons.MAN;
            }
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
