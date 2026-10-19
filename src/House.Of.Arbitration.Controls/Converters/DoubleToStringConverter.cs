#region Imports
using System.Globalization;
#endregion

namespace House.Of.Arbitration.Controls.Converters;

/// <summary>
/// Convertisseur bidirectionnel pour les valeurs numériques à virgule flottante (double),
/// supportant à la fois le point et la virgule comme séparateur décimal.
/// </summary>
public class DoubleToStringConverter : IValueConverter
{
    #region Implement IValueConverter
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return d.ToString(CultureInfo.CurrentCulture);
        }
        return string.Empty;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            var trimmed = str.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            // Essai avec la culture courante (ex: "65,5" en fr-FR)
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentCultureVal))
            {
                return currentCultureVal;
            }

            // Repli avec InvariantCulture en normalisant le séparateur (ex: "65.5")
            var invariantString = trimmed.Replace(',', '.');
            if (double.TryParse(invariantString, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantVal))
            {
                return invariantVal;
            }

            // Si l'utilisateur est en train de taper un séparateur seul (ex: "," ou ".")
            if (trimmed == "," || trimmed == ".")
            {
                return Binding.DoNothing;
            }

            return Binding.DoNothing;
        }

        return null;
    }
    #endregion
}
