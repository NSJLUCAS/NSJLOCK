using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NSJLock.App;

public sealed class PercentToStrokeDashArrayConverter : IValueConverter
{
    private const double MeterDashUnits = 52.2;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var percent = value switch
        {
            int intValue => intValue,
            double doubleValue => doubleValue,
            _ => 0
        };

        percent = Math.Clamp(percent, 0, 100);
        var activeUnits = MeterDashUnits * percent / 100;
        return new DoubleCollection { activeUnits, MeterDashUnits - activeUnits };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
