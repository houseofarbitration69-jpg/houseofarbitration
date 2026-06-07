using House.Of.Arbitration.Models;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class GenreToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CompetitorModel competitor)
        {
            if (!string.IsNullOrEmpty(competitor.Photo))
            {
                return ImageSource.FromFile(competitor.Photo);
            }

            string imageName = competitor.Genre switch
            {
                Genre.Women => "user_woman",
                _ => "user_man"
            };

            return imageName;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}