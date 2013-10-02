using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FileTransferStatusWidget
{
    public class AlternatingRowBGConverter : IValueConverter
    {
        private static readonly Brush EVEN_ROW_BRUSH = new SolidColorBrush(Color.FromRgb(0xFF, 0xFC, 0xE6));
        private static readonly Brush ODD_ROW_BRUSH = Brushes.White;

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            ListViewItem item = (ListViewItem)value;
            ListView view = ItemsControl.ItemsControlFromItemContainer(item) as ListView;
            int index = view.ItemContainerGenerator.IndexFromContainer(item);
            if (index % 2 == 0)
            {
                return EVEN_ROW_BRUSH;
            }
            else
            {
                return ODD_ROW_BRUSH;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
