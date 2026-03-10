using House.Of.Arbitration.Models;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class CategoryToNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if(value is CategoryModel category)
        {
            var weights =
    (category.WeightMin <= 0 && category.WeightMax > 0) ? $"-{category.WeightMax}" :
    ((category.WeightMin > 0 && category.WeightMax <= 0) ? $"+{category.WeightMin}" : String.Empty);

            switch (category.Type)
            {
                case CategoryType.None:
                    return String.Empty;
                case CategoryType.Sanda:
                    return $"Sanda {category.AgeRange} {category.Genre} {weights}";
                case CategoryType.SandaLight:
                    return $"Sanda Light {category.AgeRange} {category.Genre} {weights}";
                case CategoryType.Taolu:
                    return $"Taolu {category.AgeRange} {category.Genre}";
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
