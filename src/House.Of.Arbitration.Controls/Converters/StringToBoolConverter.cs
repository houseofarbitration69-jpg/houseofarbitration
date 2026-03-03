#region Imports
using System.Globalization;
#endregion

namespace House.Of.Arbitration.Controls.Converters;

public class StringToBoolConverter : IValueConverter
{
    #region Implement IValueConverter
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
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
