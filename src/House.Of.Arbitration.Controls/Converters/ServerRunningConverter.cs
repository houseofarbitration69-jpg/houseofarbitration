using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace House.Of.Arbitration.Controls.Converters;

public class ServerRunningConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values != null && values.Length == 3)
        {
            try 
            {
                var compId = System.Convert.ToInt32(values[0]);
                var serverCompId = System.Convert.ToInt32(values[1]);
                var isStarted = System.Convert.ToBoolean(values[2]);
                return isStarted && compId == serverCompId;
            }
            catch 
            {
                return false;
            }
        }
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
