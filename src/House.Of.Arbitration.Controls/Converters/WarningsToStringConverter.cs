using House.Of.Arbitration.Models;
using System.Globalization;
using System.Text;

namespace House.Of.Arbitration.Controls.Converters;

public class WarningsToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is List<WarningModel> warnings && warnings.Any())
        {
            var sb = new StringBuilder();
            foreach (var warning in warnings)
            {
                sb.AppendLine($"• {warning.Label}");
            }
            return sb.ToString().TrimEnd();
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
