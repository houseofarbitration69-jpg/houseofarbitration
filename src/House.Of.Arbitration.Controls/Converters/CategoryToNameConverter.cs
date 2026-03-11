using House.Of.Arbitration.Models;
using House.Of.Arbitration.Localization;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class CategoryToNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if(value is CategoryModel category)
        {
            var localizer = LocalizationResourceManager.Instance;

            var weights =
    (category.WeightMin <= 0 && category.WeightMax > 0) ? $"-{category.WeightMax}" :
    ((category.WeightMin > 0 && category.WeightMax <= 0) ? $"+{category.WeightMin}" : String.Empty);

            switch (category.Type)
            {
                case CategoryType.None:
                    return String.Empty;
                case CategoryType.Sanda:
                    return $"Sanda {localizer.GetValue($"ENUM_AGE_{category.AgeRange.ToString().ToUpper()}")} {localizer.GetValue($"ENUM_GENRE_{category.Genre.ToString().ToUpper()}")} {weights}{localizer.GetValue("WEIGHT_UNIT")}";
                case CategoryType.SandaLight:
                    return $"Sanda Light {localizer.GetValue($"ENUM_AGE_{category.AgeRange.ToString().ToUpper()}")} {localizer.GetValue($"ENUM_GENRE_{category.Genre.ToString().ToUpper()}")} {weights}{localizer.GetValue("WEIGHT_UNIT")}";
                case CategoryType.Taolu:
                    return $"Taolu {localizer.GetValue($"ENUM_AGE_{category.AgeRange.ToString().ToUpper()}")} {localizer.GetValue($"ENUM_GENRE_{category.Genre.ToString().ToUpper()}")}";
                default:
                    return String.Empty;
            }
        }

        return String.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
