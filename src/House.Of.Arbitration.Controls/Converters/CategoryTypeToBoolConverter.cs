using House.Of.Arbitration.Models;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class CategoryTypeToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        RoundType? roundType = null;
        if (value is RoundType rt)
        {
            roundType = rt;
        }
        else if (value is IDrawModel drawModel)
        {
            roundType = drawModel.Type;
        }

        if (roundType.HasValue)
        {
            var type = roundType.Value;
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
