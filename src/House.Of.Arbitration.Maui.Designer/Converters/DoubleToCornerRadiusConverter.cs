using System.Globalization;

namespace House.Of.Arbitration.Maui.Designer.Converters;

public class DoubleToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return new CornerRadius(d);
        
        return new CornerRadius(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
