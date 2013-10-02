using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace DataTableEditor
{
    /// <summary>
    /// Interaction logic for NamespaceDialog.xaml
    /// </summary>
    public partial class NamespaceDialog : Window
    {

        public ObservableCollection<nsConf_inner> DataGridItemsSource { get; set; }

        public List<nsConf> config_to_return = new List<nsConf>();

        public class nsConf_inner
        {
            public string ns;
            public string source;
        }

        public NamespaceDialog(List<nsConf> pre_config)
        {
            InitializeComponent();
            DataGridItemsSource = new ObservableCollection<nsConf_inner>();
            initTable(pre_config);

            m_applyNSTable.Click += m_applyNSTable_Click;
            m_okAndExit.Click += m_okAndExit_Click;
        }

        void initTable(List<nsConf> pre_config)
        {
            //config_to_return = pre_config;
            if (pre_config != null)
            {
                //m_nstab.ItemsSource = pre_config;
            }
            else
            {
                /* List<nsConf> data_source = new List<nsConf>();
                data_source.Add(new_conf);
                m_nstab.ItemsSource = data_source; */
                nsConf_inner new_conf = new nsConf_inner()
                {
                    ns = "Hello",
                    source = "World!"
                };
                DataGridItemsSource.Add(new_conf);
                m_nstab.ItemsSource = DataGridItemsSource;
                m_nstab.Items.Refresh();
                DataGridItemsSource.Add(new_conf);
                DataGridTextColumn ns_col = new DataGridTextColumn()
                {
                    Binding = new Binding("ns") { Mode = BindingMode.TwoWay }
                };
                m_nstab.Columns.Add(ns_col);
                DataGridTextColumn ns_source_col = new DataGridTextColumn()
                {
                    Binding = new Binding("source") { Mode = BindingMode.TwoWay }
                };
                m_nstab.Columns.Add(ns_source_col);
                
            }
        }

        void m_okAndExit_Click(object sender, RoutedEventArgs e)
        {
            List<nsConf> new_config = new List<nsConf>();
            foreach (nsConf this_config in m_nstab.ItemsSource)
            {
                new_config.Add(this_config);
            }
            config_to_return = new_config;
            Close();
        }

        void m_applyNSTable_Click(object sender, RoutedEventArgs e)
        {
            List<nsConf> new_config = new List<nsConf>();
            foreach (nsConf this_config in m_nstab.ItemsSource)
            {
                new_config.Add(this_config);
            }
            config_to_return = new_config;
        }
    }
}
