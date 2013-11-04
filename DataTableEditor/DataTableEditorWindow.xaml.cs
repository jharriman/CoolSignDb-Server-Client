using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using System.IO;
using System.Threading;
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
using CoolSign.API;
using CoolSign.API.Version1;
using CoolSign.API.Version1.DataAccess;
using ConfigClasses;

namespace DataTableEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class DataTableEditorWindow : Window
    {
        private CSAPI m_api;
        private IServerSession m_session;
        private IDataTable m_table;
        private ObservableCollection<IDataRow> m_rows;
        private ObservableCollection<IFileInDataTable> m_files;
        private WatchedSets dbsets;
        private TcpClient client;

        public DataTableEditorWindow()
        {
            m_api = CSAPI.Create();
            
            InitializeComponent();

            OnLoad();

            m_connectButton.Click += m_connectButton_Click;
            m_tableComboBox.SelectionChanged += m_tableComboBox_SelectionChanged;
            m_addRowButton.Click += m_addRowButton_Click;
            m_saveRowsButton.Click += m_saveRowsButton_Click;
            m_addTableToWatch.Click += m_addTableToWatch_Click;
            m_editWatchSets.Click += m_editWatchSets_Click;
            m_establishConnection.Click += m_establishConnection_Click;
        }

        void m_establishConnection_Click(object sender, RoutedEventArgs e)
        {
            TcpClient tcpclnt = new TcpClient();
            Console.WriteLine("Connecting.....");

            tcpclnt.Connect("128.135.167.97", 8001);
            // use the ipaddress as in the server program

            Console.WriteLine("Connected");
            /* Console.Write("Enter the string to be transmitted : "); */

            client = tcpclnt;
            MessageBox.Show("Connected!");
        }

        public void OnLoad()
        {
            dbsets = new WatchedSets(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Properties.Settings.Default.dbfilepath);
            m_serverUrl.Text = Properties.Settings.Default.defaultserverip;
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (null != m_session)
            {
                m_session.Dispose();
                m_session = null;
            }

            base.OnClosing(e);
        }

        void m_editWatchSets_Click(object sender, RoutedEventArgs e)
        {
            /*NamespaceDialog2 nd = new NamespaceDialog2();
            bool? result = nd.ShowDialog();*/
            
            if (m_table != null)
            {
                /* Make sure the table we are adding is not in the table */
                if (dbsets.isInAllSet((string)m_table.Id))
                {
                    /* Start configuration/setup dialog */
                    WatcherSetupDialog wsd = new WatcherSetupDialog((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + m_table.Name + "-db.txt"), (string)m_table.Id, m_table, true, client);
                    bool? result = wsd.ShowDialog();

                }
                else
                {
                    MessageBox.Show("Error: Table is not being watched. Perhaps you meant 'Add to Watch'?");
                }
            }
        }

        private void m_addTableToWatch_Click(object sender, RoutedEventArgs e)
        {
            if (m_table != null)
            {
                /* Make sure the table we are adding is not in the table */
                if (!dbsets.isInAllSet((string)m_table.Id))
                {
                    /* Start configuration/setup dialog */
                    WatcherSetupDialog wsd = new WatcherSetupDialog((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + m_table.Name + "-db.txt"), (string)m_table.Id, m_table, false, client);
                    bool? result = wsd.ShowDialog();

                    /* Make sure the addition wasn't canceled */
                    if (result.Value == true)
                    {
                        /* Add to working database */
                        dbsets.addToWatchedSets((string)m_table.Id, m_table.Name, (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + m_table.Name + "-db.txt"));

                        /* Save database to file */
                        dbsets.saveToDbFile((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Properties.Settings.Default.dbfilepath));
                    }

                    /* Refresh working configuration (in case user wants to update the same table again without closing the window) */
                    dbsets = new WatchedSets(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Properties.Settings.Default.dbfilepath);

                }
                else
                {
                    MessageBox.Show("Table is already being watched.");
                }
                /* MessageBox.Show((string)m_table.Id); 
                using (FileStream fs = File.Open(Properties.Settings.Default.dbfilepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    StreamReader sr = new StreamReader(fs);
                    StreamWriter sw = new StreamWriter(fs);
                    MessageBox.Show("Worked!");
                    bool inTable = false;
                    while(!sr.EndOfStream)
                    {
                        var parts = sr.ReadLine().Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts[0] == (string)m_table.Id)
                        {
                            inTable = true;
                            break; 
                        }
                    }
                    if (inTable == false)
                    {
                        MessageBox.Show("HERE!");
                        sw.WriteLine((string)m_table.Id + DELIM + m_table.Name); //Insert configuration file for ImportContent here
                        sw.Flush();
                    }
                    fs.Close(); 
                }*/
            }
        }

        private void m_addRowButton_Click(object sender, RoutedEventArgs e)
        {
            IDataRow row = m_session.ModelFactory.CreateDataRow();
            if (m_rows.Count == 0) { row.SequenceNumber = 0; }
            else { row.SequenceNumber = m_rows.Max((r) => r.SequenceNumber) + 1; }
            row.ActivationDate = new DateTime(1601, 1, 1);
            row.ExpirationDate = new DateTime(3000, 1, 1);
            foreach (IDataTableField field in m_table.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
            {
                row.Values[field.Name] = field.DefaultValue;
            }
            m_rows.Add(row);
        }

        private void m_saveRowsButton_Click(object sender, RoutedEventArgs e)
        {
            IChangeSet changes = m_session.DataAccess.CreateChangeSet();

            // delete all rows first
            m_session.DataAccess.Brokers.DataTable.DeleteDataRows(changes, m_table.Id, m_session.ModelFactory.CreateAllSelector());

            // delete all files first
            m_session.DataAccess.Brokers.DataTable.DeleteFileInDataTables(changes, m_table.Id, m_session.ModelFactory.CreateAllSelector());

            List<IDataTableField> mediaFields = new List<IDataTableField>();
            foreach (IDataTableField field in m_table.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
            {
                if (field.Type == DataTableFieldType.Media)
                {
                    mediaFields.Add(field);
                }
            }

            List<string> mediaFileNames = new List<string>();
            foreach (IDataRow row in m_rows)
            {
                foreach (IDataTableField mediaField in mediaFields)
                {
                    if (row.Values.ContainsKey(mediaField.Name) &&
                        !mediaFileNames.Contains(row.Values[mediaField.Name]))
                    {
                        mediaFileNames.Add(row.Values[mediaField.Name]);
                    }
                }
                m_session.DataAccess.Brokers.DataTable.CreateDataRow(changes, m_table.Id, row);
            }

            // delete orphaned files
            foreach (IFileInDataTable fidt in new List<IFileInDataTable>(m_files))
            {
                if (!mediaFileNames.Contains(fidt.Name))
                {
                    m_files.Remove(fidt);
                }
            }

            foreach (IFileInDataTable fidt in m_files)
            {
                m_session.DataAccess.Brokers.DataTable.CreateFileInDataTable(changes, m_table.Id, fidt);
            }

            using (var result = changes.Save())
            {
                if (result.IsSuccess)
                {
                    m_errors.Text = "";
                }
                else
                {
                    m_errors.Text = "Failed to update data table: " + result.ToString();
                }
            }
        }

        private void m_tableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_rowGrid.ItemsSource = null;
            m_rowGrid.Columns.Clear();

            IDataTable table = m_tableComboBox.SelectedItem as IDataTable;
            if (null != table)
            {
                using (var result = m_session.DataAccess.Brokers.DataTable.ReadSingle(
                    m_session.ModelFactory.CreateSelectorById(table.Id),
                    new IRelationshipMetaData[]
                {
                    MetaData.DataTableToDataTableDesign, 
                    MetaData.DataTableDesignToDataTableField, 
                    MetaData.DataTableToFileInDataTable, 
                    MetaData.DataTableToDataRow
                }))
                {
                    if (result.IsSuccess)
                    {
                        m_errors.Text = "";

                        m_table = result.Value;

                        IDataTableDesign design = m_table.DataTableDesigns.Items.FirstOrDefault();

                        //{
                        //    DataGridColumn col = new DataGridTextColumn()
                        //    {
                        //        Binding = new Binding("SequenceNumber") { Mode = BindingMode.TwoWay },
                        //        CanUserReorder = true,
                        //        CanUserResize = true,
                        //        CanUserSort = true,
                        //        IsReadOnly = false,
                        //        Header = "SequenceNumber"
                        //    };
                        //    m_rowGrid.Columns.Add(col);
                        //}

                        //{
                        //    DataGridColumn col = new DataGridTextColumn()
                        //    {
                        //        Binding = new Binding("ActivationDate") { Mode = BindingMode.TwoWay },
                        //        CanUserReorder = true,
                        //        CanUserResize = true,
                        //        CanUserSort = true,
                        //        IsReadOnly = false,
                        //        Header = "ActivationDate"
                        //    };
                        //    m_rowGrid.Columns.Add(col);
                        //}

                        //{
                        //    DataGridColumn col = new DataGridTextColumn()
                        //    {
                        //        Binding = new Binding("ExpirationDate") { Mode = BindingMode.TwoWay },
                        //        CanUserReorder = true,
                        //        CanUserResize = true,
                        //        CanUserSort = true,
                        //        IsReadOnly = false,
                        //        Header = "ExpirationDate"
                        //    };
                        //    m_rowGrid.Columns.Add(col);
                        //}

                        if (null != design)
                        {
                            foreach (IDataTableField field in design.DataTableFields.Items)
                            {
                                DataGridColumn col;
                                if (field.Type == DataTableFieldType.Media)
                                {
                                    DataTemplate editTemplate;
                                    {
                                        editTemplate = new DataTemplate();
                                        FrameworkElementFactory fac = new FrameworkElementFactory(typeof(Button));
                                        fac.SetValue(Button.ContentProperty, "...");
                                        fac.SetValue(Button.WidthProperty, 25d);
                                        fac.AddHandler(Button.ClickEvent, new RoutedEventHandler(MediaFieldEditClick));
                                        editTemplate.VisualTree = fac;
                                    }
                                    DataTemplate cellTemplate;
                                    {
                                        cellTemplate = new DataTemplate();
                                        FrameworkElementFactory fac = new FrameworkElementFactory(typeof(TextBlock));
                                        fac.SetBinding(TextBlock.TextProperty, new Binding("Values[" + field.Name + "]") { Mode = BindingMode.OneWay });
                                        cellTemplate.VisualTree = fac;
                                    }

                                    col = new DataGridTemplateColumn()
                                    {
                                        CellEditingTemplate = editTemplate,
                                        CellTemplate = cellTemplate
                                    };
                                }
                                else
                                {
                                    col = new DataGridTextColumn()
                                    {
                                        Binding = new Binding("Values[" + field.Name + "]") { Mode = BindingMode.TwoWay },
                                        CanUserReorder = true,
                                        CanUserResize = true,
                                        CanUserSort = true,
                                        IsReadOnly = false,
                                    };
                                }

                                col.Header = field.Name;
                                col.Width = 75;

                                m_rowGrid.Columns.Add(col);
                            }
                        }

                        {
                            DataTemplate template = new DataTemplate();
                            FrameworkElementFactory fac = new FrameworkElementFactory(typeof(Button));
                            fac.SetValue(Button.ContentProperty, "X");
                            fac.SetValue(Button.WidthProperty, 15d);
                            fac.AddHandler(Button.ClickEvent, new RoutedEventHandler(RowDeleteButtonClick));
                            template.VisualTree = fac;

                            DataGridColumn col = new DataGridTemplateColumn()
                            {
                                IsReadOnly = true,
                                CellTemplate = template,
                                Header = "Delete row",
                                Width = 45,
                            };
                            m_rowGrid.Columns.Add(col);
                        }

                        m_rows = new ObservableCollection<IDataRow>(m_table.DataRows.Items.OrderBy((row) => row.SequenceNumber));
                        m_files = new ObservableCollection<IFileInDataTable>(m_table.FileInDataTables.Items);
                        m_rowGrid.ItemsSource = m_rows;
                    }
                    else
                    {
                        m_errors.Text = "Failed to read DataTable rows: " + result.ToString();
                    }
                }
            }
        }

        private void RowDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            m_errors.Text = "";

            Button b = (Button)sender;
            IDataRow row = (IDataRow)b.DataContext;

            m_rows.Remove(row);
        }

        private void MediaFieldEditClick(object sender, RoutedEventArgs e)
        {
            m_errors.Text = "";

            Button b = (Button)sender;
            IDataRow row = (IDataRow)b.DataContext;
            DataGridCell cell = GetVisualParent<DataGridCell>(b);
            DataGridColumn col = cell.Column;

            OpenFileDialog dlg = new OpenFileDialog();
            bool? result = dlg.ShowDialog();
            if (result.HasValue && result.Value)
            {
                using (System.IO.Stream fs = dlg.OpenFile())
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                        using (var putFileResult = m_session.Files.PutFile(fs))
                        {
                            if (putFileResult.IsSuccess)
                            {
                                string name = System.IO.Path.GetFileName(dlg.FileName);
                                IFileInDataTable fidt = m_files.FirstOrDefault((f) => 0 == string.Compare(name, f.Name, StringComparison.InvariantCultureIgnoreCase) && putFileResult.Value == f.FileHash);
                                if (null == fidt)
                                {
                                    fidt = m_session.ModelFactory.CreateFileInDataTable();
                                    fidt.FileHash = putFileResult.Value;
                                    fidt.Name = System.IO.Path.GetFileName(dlg.FileName);
                                    fidt.DataTable.Id = m_table.Id;
                                    m_files.Add(fidt);
                                }

                                row.Values[(string)col.Header] = fidt.Name;
                            }
                            else
                            {
                                m_errors.Text = "Failed to upload file to server: " + putFileResult.ToString();
                            }
                        }
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                }
            }
        }

        private static T GetVisualParent<T>(Visual child) where T : Visual
        {
            T parent = default(T);
            Visual v = (Visual)VisualTreeHelper.GetParent(child);
            if (v == null)
            {
                return null;
            }
            parent = v as T;
            if (parent == null)
            {
                return GetVisualParent<T>(v);
            }
            return parent;
        }

        private void m_connectButton_Click(object sender, RoutedEventArgs e)
        {
            string host = m_serverUrl.Text;
            Properties.Settings.Default.defaultserverip = m_serverUrl.Text; /* Changes the default if the user changes the server IP */
            int port = 80;
            if (!string.IsNullOrEmpty(host) && host.Contains(':'))
            {
                string[] bits = host.Split(':');
                host = bits[0];
                int.TryParse(bits[1], out port);
            }

            LoginDialog dlg = new LoginDialog(m_api, host, port);
            bool? result = dlg.ShowDialog();

            if (result.HasValue && result.Value)
            {
                m_session = dlg.Session;

                RefreshTableList();
            }
        }

        private void RefreshTableList()
        {
            using (var result = m_session.DataAccess.Brokers.DataTable.Read(m_session.ModelFactory.CreateAllSelector(), null))
            {
                if (result.IsSuccess)
                {
                    m_tableComboBox.ItemsSource = result.Value.Items;
                    m_errors.Text = "";
                }
                else
                {
                    m_errors.Text = "Failed to read DataTable list: " + result.ToString();
                }
            }
        }
    }
}
