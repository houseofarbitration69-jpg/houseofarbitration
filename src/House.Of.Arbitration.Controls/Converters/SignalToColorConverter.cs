using System.Globalization;

namespace House.Of.Arbitration.Controls.Converters;

public class SignalToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int rssi && parameter is string barIndexStr && int.TryParse(barIndexStr, out int barIndex))
        {
            // barIndex: 1, 2, 3, 4
            // Levels:
            // 4 bars: > -60
            // 3 bars: > -70
            // 2 bars: > -80
            // 1 bar:  > -90 (or anything)

            int activeBars = 1;
            if (rssi > -60) activeBars = 4;
            else if (rssi > -70) activeBars = 3;
            else if (rssi > -80) activeBars = 2;

            if (barIndex <= activeBars)
            {
                // Return color based on total strength
                if (activeBars == 1) return Colors.Red;
                if (activeBars == 2) return Colors.Orange;
                if (activeBars == 3) return Colors.Yellow;
                return Colors.LimeGreen;
            }

            return Color.FromArgb("#33FFFFFF"); // Inactive bar (translucent white)
        }

        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
