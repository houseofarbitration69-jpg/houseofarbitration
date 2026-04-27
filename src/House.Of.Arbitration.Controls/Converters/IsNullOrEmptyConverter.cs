using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class IsNullOrEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEmpty = false;

        if (value == null)
        {
            isEmpty = true;
        }
        else if (value is string s)
        {
            isEmpty = string.IsNullOrWhiteSpace(s);
        }
        else if (value is System.Collections.IEnumerable list)
        {
            var enumerator = list.GetEnumerator();
            isEmpty = !enumerator.MoveNext();
        }

        if (parameter as string == "Invert")
        {
            return !isEmpty;
        }

        return isEmpty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}