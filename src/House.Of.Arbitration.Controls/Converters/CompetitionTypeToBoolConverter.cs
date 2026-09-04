using House.Of.Arbitration.Models;
using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class CompetitionTypeToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CompetitionType ct)
        {
            if (parameter is string paramStr)
            {
                if (paramStr == "Invert") return ct == CompetitionType.None;

                bool isNot = paramStr.StartsWith("Not");
                string targetName = isNot ? paramStr.Substring(3) : paramStr;

                if (Enum.TryParse<CompetitionType>(targetName, out var target))
                {
                    bool isEqual = ct == target;
                    return isNot ? !isEqual : isEqual;
                }
            }
            else if (parameter is CompetitionType target)
            {
                return ct == target;
            }

            return ct != CompetitionType.None;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
