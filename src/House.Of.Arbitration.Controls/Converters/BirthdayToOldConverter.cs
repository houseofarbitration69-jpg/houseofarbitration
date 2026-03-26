#region Imports
using System.Globalization;
#endregion

namespace House.Of.Arbitration.Controls.Converters;

public class BirthdayToOldConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null) return -1;

        if (values[0] is DateTime birthday)
        {
            var today = DateTime.Today;

            if (values[1] is DateTime date)
            {
                today = date;
            }

            int age = today.Year - birthday.Year;

            if(birthday.Date > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }

        return -1;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
