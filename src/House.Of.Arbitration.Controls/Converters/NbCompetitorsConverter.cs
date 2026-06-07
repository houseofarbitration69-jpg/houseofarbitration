#region Imports
using House.Of.Arbitration.Models;
using System.Globalization;
#endregion

namespace House.Of.Arbitration.Controls.Converters;

public class NbCompetitorsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int nbCompetitor)
        {
            var result = String.Empty;

            if (nbCompetitor > 1)
            {
                result = $"({nbCompetitor} compétiteurs)";
            }
            else if(nbCompetitor == 1)
            {
                result = $"({nbCompetitor} compétiteur)";
            }
            else
            {
                result = "Pas de compétiteur inscrit";
            }

            return result;
        }

        return "Pas de compétiteur inscrit";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
