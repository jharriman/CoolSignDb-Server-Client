using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace FileTransferStatusWidget
{
    public class ByteSizeConverter : IValueConverter
    {
        private enum Units
        {
            B = 0,
            KB = 1,
            MB = 2,
            GB = 3,
            TB = 4
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isRate = false;
            if (parameter is bool)
            {
                isRate = (bool)parameter;
            }

            double bytes = (double)(long)value;

            Units unit = Units.B;

            while (bytes >= 1024 && unit < Units.TB)
            {
                ++unit;
                bytes /= 1024.0d;
            }

            return string.Format("{0:F2}{1}{2}", bytes, unit, isRate ? "/s" : "");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
