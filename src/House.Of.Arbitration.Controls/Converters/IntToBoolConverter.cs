#region Imports
using System.Globalization;
#endregion

namespace House.Of.Arbitration.Controls.Converters;

public class IntToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = (value is Int32 && ((Int32)value) > 0);
        
        if (parameter as string == "Invert")
        {
            return !hasValue;
        }

        return hasValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
