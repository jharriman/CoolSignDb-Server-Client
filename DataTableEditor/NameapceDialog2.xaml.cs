using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading;
using CoolSign.API;
using CoolSign.API.Version1;
using CoolSign.API.Version1.DataAccess;
using ConfigClasses;

namespace DataTableEditor
{
    /// <summary>
    /// Interaction logic for WatcherSetupDialog.xaml
    /// </summary>
    public partial class NamespaceDialog2 : Window
    {
        private int column_num = 0;
        private string[] column_names = { "Namespace", "250", "Namespace URL", "250" };
        private List<checkedData> working_set = new List<checkedData>();
        public List<nsConf> ns_to_write = new List<nsConf>();
        public class checkedData
        {
            public string Namespace { set; get; }
            public string NamespaceURL { set; get; }
        }

        public NamespaceDialog2(List<nsConf> pre_config)
        {
            InitializeComponent();
            m_saveButton.Click += m_saveButton_Click;
            m_clearButton.Click += m_clearButton_Click;
            m_nsAddRow.Click += m_nsAddRow_Click;
            m_dataGridChamleon.AutoGeneratingColumn += m_dataGridChamleon_AutoGeneratingColumn;
            form_Init(pre_config);
        }

        void m_nsAddRow_Click(object sender, RoutedEventArgs e)
        {
            working_set.Add(new checkedData());
        }

        void m_dataGridChamleon_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (column_num > (column_names.Length - 1)) { column_num = 0; }
            e.Column.Header = column_names[column_num];
            e.Column.Width = new DataGridLength(Convert.ToDouble(column_names[column_num + 1]), DataGridLengthUnitType.Pixel);
            column_num += 2;
        }

        void m_clearButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to values?", "Clear All Entries?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                List<checkedData> addToGrid = new List<checkedData>();
                checkedData add_new = new checkedData();
                addToGrid.Add(add_new);
                m_dataGridChamleon.ItemsSource = addToGrid;
            }
        }

        void form_Init(List<nsConf> pre_config)
        {
            if (pre_config != null)
            {
                /* Convert to checkedData and add to working_set*/
                foreach (nsConf conf in pre_config)
                {
                    working_set.Add(new checkedData() { Namespace = conf.ns, NamespaceURL = conf.ns_source });
                } 
            }
            m_dataGridChamleon.ItemsSource = working_set;
            
        }

        void m_saveButton_Click(object sender, RoutedEventArgs e)
        {
            /* Convert checkedData to nsConf */
            foreach (checkedData check in working_set)
            {
                ns_to_write.Add(new nsConf() { ns = check.Namespace, ns_source = check.NamespaceURL });
            }
            this.DialogResult = true;
            Close();
        }
    }
}
