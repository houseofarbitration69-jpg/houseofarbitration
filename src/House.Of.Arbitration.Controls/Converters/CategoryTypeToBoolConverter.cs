using House.Of.Arbitration.Models;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class CategoryTypeToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RoundType type)
        {
            if (parameter is string paramStr)
            {
                if (paramStr == "Invert") return type == RoundType.None;
                
                bool isNot = paramStr.StartsWith("Not");
                string targetName = isNot ? paramStr.Substring(3) : paramStr;

                if (Enum.TryParse<RoundType>(targetName, out var target))
                {
                    bool isEqual = type == target;
                    return isNot ? !isEqual : isEqual;
                }
            }
            else if (parameter is RoundType target)
            {
                return type == target;
            }

            return type != RoundType.None;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
