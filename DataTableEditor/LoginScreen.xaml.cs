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
using NetworkClasses;
using ConfigClasses;

namespace DataTableEditor
{
    /// <summary>
    /// Interaction logic for LoginScreen.xaml
    /// </summary>
    public partial class LoginScreen : Window
    {
        public LoginScreen()
        {
            InitializeComponent();
            m_connect.Click += m_connect_Click;
        }

        void m_connect_Click(object sender, RoutedEventArgs e)
        {
            DataTableEditorWindow dtew = new DataTableEditorWindow(establishConnection());
            this.Hide();
            bool? result = dtew.ShowDialog();
        }
        
        netConnect establishConnection()
        {
            netConnect conn = new netConnect("128.135.167.97", 8001);
            conn.safeWrite<int>(5);
            List<avail_table> available_tables = conn.safeRead<List<avail_table>>();
            return conn;
        }
    }
}
