#region Imports
using System.Globalization;
#endregion

namespace House.Of.Arbitration.Controls.Converters;

public class DoubleToCornerRadiusConverter : IValueConverter
{
    #region Implement IValueConverter
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
            return new CornerRadius(d);
        
        return new CornerRadius(0);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    #endregion
}
