using House.Of.Arbitration.Models;
using House.Of.Arbitration.Localization;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class RoundTypeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RoundType roundType)
        {
            var localizer = LocalizationResourceManager.Instance;
            
            return roundType switch
            {
                RoundType.Knockouts => localizer.GetValue("ENUM_ROUND_KNOCKOUTS"),
                RoundType.Pools => localizer.GetValue("ENUM_ROUND_POOLS"),
                RoundType.Order => localizer.GetValue("ENUM_ROUND_ORDER"),
                _ => localizer.GetValue("ENUM_ROUND_NONE")
            };
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
