using System.Globalization;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Controls.Converters;

public class IsoCodeToFlagPathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string input && !string.IsNullOrWhiteSpace(input))
        {
            string? isoCode = null;
            if (input.Length == 2)
            {
                isoCode = input.ToLowerInvariant();
            }
            else
            {
                // Try to find by name
                isoCode = CountryModel.DefaultCountries
                    .FirstOrDefault(c => string.Equals(c.Name, input, StringComparison.OrdinalIgnoreCase))
                    ?.IsoCode.ToLowerInvariant();
            }

            if (isoCode != null)
            {
                return $"{isoCode}.png";
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
