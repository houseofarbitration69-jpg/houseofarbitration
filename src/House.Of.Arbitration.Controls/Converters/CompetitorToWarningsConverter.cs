using House.Of.Arbitration.Models;
using System.Globalization;
using System.Text;

namespace House.Of.Arbitration.Controls.Converters;

public class CompetitorToWarningsConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return string.Empty;

        var competitor = values[0] as CompetitorModel;
        var category = values[1] as CategoryModel;

        if (competitor == null || category == null) return string.Empty;

        // Find the registration link that contains the warnings
        var link = category.Competitors?.FirstOrDefault(cc => cc.CompetitorId == competitor.Id);
        
        if (link?.Warnings != null && link.Warnings.Any())
        {
            var sb = new StringBuilder();
            foreach (var warning in link.Warnings)
            {
                sb.AppendLine($"• {warning.Label}");
            }
            return sb.ToString().TrimEnd();
        }

        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
