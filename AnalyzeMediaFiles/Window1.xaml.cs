using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using CoolSign.API.Media;

namespace AnalyzeMediaFiles
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        private AssetInfo m_asset;

        public Window1()
        {
            InitializeComponent();

            m_browseButton.Click += m_browseButton_Click;
        }

        private void m_browseButton_Click(object sender, RoutedEventArgs e)
        {
            if (null != m_asset)
            {
                var asset = m_asset;
                m_asset = null;
                DataContext = null;
                m_img.Source = null;
            }

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = false;
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            bool? result = dlg.ShowDialog(this);
            if (result.HasValue && result.Value)
            {
                m_fileTextBox.Text = dlg.FileName;
                m_asset = MediaAnalyzer.AnalyzeAssetFile(m_fileTextBox.Text);
                DataContext = m_asset;
                using (MemoryStream ms = new MemoryStream(m_asset.ThumbnailImageBytes))
                {
                    BitmapImage img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = ms;
                    img.EndInit();
                    m_img.Source = img;
                }
            }
        }
    }
}
