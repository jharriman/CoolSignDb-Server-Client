using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;
using CoolSign.API;
using CoolSign.API.Version1;
using CoolSign.API.Version1.DataAccess;

namespace DataTableEditor
{ 
    /// <summary>
    /// Interaction logic for WatcherSetupDialog.xaml
    /// </summary>
    public partial class WatcherSetupDialog : Window
    {
        private string pathToDb;
        private string tbl_OID;
        private IDataTable tableToEdit;
        private List<nsConf> ns_to_write;
        private char DELIM = ';';
        public bool allOneRecord = true;
        public WatcherSetupDialog(string db_path, string table_oid, IDataTable table_to_edit, bool editing)
        {
            pathToDb = db_path;
            tbl_OID = table_oid;
            tableToEdit = table_to_edit;
            InitializeComponent();

            /* Functionality buttons */
            m_saveButton.Click += m_saveButton_Click;
            m_goToNamespaces.Click += m_goToNamespaces_Click;
            m_recordBox.Unchecked += m_recordBox_Unchecked;
            m_recordBox.Checked += m_recordBox_Checked;
            m_cancelButton.Click += m_cancelButton_Click;
            m_clearButton.Click += m_clearButton_Click;

            /* Interface smoothing actions */
            m_sourcePathBox.TextChanged += m_sourcePathBox_TextChanged;

            /* Help dialog buttons */
            h_sourceHelp.Click += h_sourceHelp_Click;
            h_recordPath.Click += h_recordPath_Click;
            h_advanceMode.Click += h_advanceMode_Click;
            h_namespaces.Click += h_namespaces_Click;
            form_Init(editing, pathToDb);
        }
        private List<string> pos_xml_paths = new List<string>();
        void m_sourcePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string check_url = m_sourcePathBox.Text;

            /* Try to grab XML document from URL */
            XmlDocument xd = new XmlDocument();
            try
            { 
                xd.Load(check_url);
            }
            catch(Exception) // TODO: Make sure this is working properly
            {
                m_recordCombo.IsEnabled = false;
                return;
            }
            m_recordCombo.IsEnabled = true;
            XmlNode inXmlNode = xd.DocumentElement;
            /* BOBEX: Here is the example of tree traversal, in fact, since it already finds all the nodes everytime you change the URL, you just have to remove the 
             * non-relevant ones. The bigger problem will be appending a drop down box to a DataTable */
            traverseNodes(xd.ChildNodes, "");
            pos_xml_paths = pos_xml_paths.Distinct().ToList();
            pos_xml_paths.RemoveAll(ContainsHash);
            pos_xml_paths.Sort();
            m_recordCombo.ItemsSource = pos_xml_paths;
        }
        private bool ContainsHash(string s)
        { 
            return s.ToLower().Contains('#');
        }
        void traverseNodes(XmlNodeList nodes, string so_far)
        {
            foreach (XmlNode node in nodes)
            {
                string current = so_far + "/" + node.Name;
                pos_xml_paths.Add(current);
                traverseNodes(node.ChildNodes, current);
            }
        }

        void h_sourceHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("The source url is the URL address where the data feed is stored. This is typically the text in the address bar of your web browser when you navigate to a feed.", "Source URL Help");
        }

        void h_recordPath_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("The record path is the XML path to the data you want to appear in your datatable. For example, you might want to use the 'item' branch of an XML tree that is wrapped in an RSS structure. By specifying 'rss/channel/item' in this box, the program will search each XML feed for 'item' tags and then add a row to the datatable for each instance of the 'item' tag. Each row is then populated using the elements underneath the 'item' tag. You may specify which elements to use in the table below this control.", "Record Path Help");
        }

        void h_advanceMode_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Unchecking this box enables Advanced Mode, which allows you to access multiple XML feeds, to set up triggerable content based on XML feed information, and to specify a different 'record' for each column in your table. Recommended for advanced users only.", "Advanced Mode Help");
        }

        void h_namespaces_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Namespaces are custom XML classes that must be referenced (by URL) in order to use XML paths that contain colons. These namespaces and their associated URL can usually be found through internet searches.", "Edit Namespaces Help");
        }

        void m_clearButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear all values?", "Clear All Entries?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                
                if (allOneRecord)
                {
                    m_recordCombo.ItemsSource = null;
                    m_recordCombo.IsEnabled = false; ;
                    m_sourcePathBox.Text = "";
                    string[] column_names = { "Column Name", "80", "XML Path", "150" };
                    List<checkedData> addToGrid = new List<checkedData>();
                    foreach (IDataTableField field in tableToEdit.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
                    {
                        checkedData add_new = new checkedData() { Column_Name = field.Name };
                        addToGrid.Add(add_new);
                    }
                    m_dataGridChamleon.ItemsSource = addToGrid;
                    m_dataGridChamleon.ItemsSource = addToGrid;
                }
                else
                {
                    string[] column_names = { "Column Name", "80", "Source URL", "150", "XML Path", "150", "Triggerable?", "75", "Trigger ID", "150" };
                    List<uncheckedData> addToGrid = new List<uncheckedData>();
                    foreach (IDataTableField field in tableToEdit.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
                    {
                        uncheckedData add_new = new uncheckedData() { column_name = field.Name };
                        addToGrid.Add(add_new);
                    }
                    m_dataGridChamleon.ItemsSource = addToGrid;
                    m_dataGridChamleon.ItemsSource = addToGrid;
                }
                
            }
        }

        void m_cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }

        public class checkedData
        {
            public string Column_Name { set; get; }
            public string Xml_Path { set; get; }
        }

        public class uncheckedData
        {
            public string column_name { set; get; }
            public string source { set; get; }
            public string xml_path { set; get; }
            public bool is_triggerable { set; get; }
            public string trigger_source { set; get; }
        }

        void m_recordBox_Checked(object sender, RoutedEventArgs e)
        {
            allOneRecord = true;
            m_sourcePathBox.IsEnabled = true;
            m_recordCombo.IsEnabled = true;
            m_rowNumber.Visibility = System.Windows.Visibility.Hidden;
            m_numRowsTextBlock.Visibility = System.Windows.Visibility.Hidden;
            string[] column_names = { "Column Name", "80", "XML Path", "150" };
            List<checkedData> addToGrid = new List<checkedData>();
            foreach (IDataTableField field in tableToEdit.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
            {
                checkedData add_new = new checkedData() { Column_Name = field.Name };
                addToGrid.Add(add_new);
            }
            m_dataGridChamleon.ItemsSource = addToGrid;
            int i = 0;
            foreach (DataGridColumn col in m_dataGridChamleon.Columns)
            {
                if (i > (column_names.Length - 1)) { break; }
                col.Header = column_names[i];
                if (i == 0) { col.IsReadOnly = true; }
                col.Width = Convert.ToInt32(column_names[i + 1]);
                i += 2;
            }
        }

        void m_recordBox_Unchecked(object sender, RoutedEventArgs e)
        {
            /* Clean up from previous state */
            allOneRecord = false;
            m_sourcePathBox.IsEnabled = false;
            m_recordCombo.IsEnabled = false;
            m_rowNumber.Visibility = System.Windows.Visibility.Visible;
            m_numRowsTextBlock.Visibility = System.Windows.Visibility.Visible;

            /* Add names of data sources */
            string[] column_names = { "Column Name", "80", "Source URL", "150", "XML Path", "150", "Triggerable?", "75", "Trigger ID", "150" };
            List<uncheckedData> addToGrid = new List<uncheckedData>();
            foreach (IDataTableField field in tableToEdit.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
            {
                uncheckedData add_new = new uncheckedData() { column_name = field.Name };
                addToGrid.Add(add_new);
            }
            m_dataGridChamleon.ItemsSource = addToGrid;

            /* Update column names */
            int i = 0;
            foreach (DataGridColumn col in m_dataGridChamleon.Columns)
            {
                if (i > (column_names.Length - 1)) { break; }
                col.Header = column_names[i];
                if (i == 0) { col.IsReadOnly = true; } // So that the column_name (which is determined by the table) cannot be altered
                col.Width = Convert.ToInt32(column_names[i + 1]);
                if (column_names[i] == "Triggerable") { col.IsReadOnly = true; }
                i += 2;
            }

        }

        void form_Init(bool editing, string db_path)
        {
            m_recordCombo.ItemsSource = pos_xml_paths;
            /* Edit Init */
            if (editing)
            {
                SetConfig init_config = new SetConfig(db_path);
                ns_to_write = init_config.ns_list;
                allOneRecord = init_config.allOneRecord;
                if (allOneRecord)
                {
                    m_sourcePathBox.IsEnabled = true;
                    m_recordCombo.IsEnabled = false;
                    m_sourcePathBox.Text = init_config.sourceUrl;
                    m_recordCombo.Text = init_config.itemOfRec;
                    m_rowNumber.Visibility = System.Windows.Visibility.Hidden;
                    m_numRowsTextBlock.Visibility = System.Windows.Visibility.Hidden;
                    string[] column_names = { "Column Name", "80", "XML Path", "150" };
                    List<checkedData> addToGrid = new List<checkedData>();
                    int k = 0;
                    /* BOBNI: Here is an example of data being added to a DataTable, though in this configuration, it is being added from file */
                    foreach (IDataTableField field in tableToEdit.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
                    {
                        colConf this_config = init_config.cols[k];
                        /* BOBNI: We create a string that represents the XML path to the specified object: concat_path
                         * Concat_path is comprised of the item of record (a generic path for simplifying paths for end users)
                         * the 'description' which is the rest of the full XML path
                         * and possibly an attribute (attributes are not encoded into XML's text data field, instead the values are stored like an 
                         * attribute of an HTML tag) */
                        string concat_path = init_config.itemOfRec + this_config.description + (String.IsNullOrEmpty(this_config.attrib) ? "" : ("@" + this_config.attrib));
                        
                        /* BOBNI: Once we have the concatenated path, we add it the checkedData, data structure */
                        checkedData add_new = new checkedData() { Column_Name = field.Name, Xml_Path = concat_path };

                        /* BOBNI: Your mission, should you choose to accept it, is to modify the CheckedData structure and this little segment of code so that, instead of
                         * a plain text path, the data table only displays a drop-down box that has been populated by following the URL, getting a list of all paths, 
                         * and then eliminating all paths that do not start with the "item of record". 
                         * An example of the drop-down box can be found at the comment labeled BOBEX */
                        addToGrid.Add(add_new);
                        k++;
                    }
                    m_dataGridChamleon.ItemsSource = addToGrid;
                    m_dataGridChamleon.ItemsSource = addToGrid;
                    int i = 0;
                    foreach (DataGridColumn col in m_dataGridChamleon.Columns)
                    {
                        if (i > (column_names.Length - 1)) { break; }
                        col.Header = column_names[i];
                        if (i == 0) { col.IsReadOnly = true; }
                        col.Width = Convert.ToInt32(column_names[i + 1]);
                        i += 2;
                    }
                    List<int> row_num_options = new List<int>();
                    for (int j = 1; j < 100; j++)
                    {
                        row_num_options.Add(j);
                    }
                    m_rowNumber.ItemsSource = row_num_options;
                }
                else
                {
                    /* Clean up from previous state */
                    m_sourcePathBox.IsEnabled = false;
                    m_recordCombo.IsEnabled = false;
                    m_recordBox.IsChecked = false;
                    m_rowNumber.Visibility = System.Windows.Visibility.Visible;
                    m_numRowsTextBlock.Visibility = System.Windows.Visibility.Visible;

                    /* Add names of data sources */
                    string[] column_names = { "Column Name", "80", "Source URL", "150", "XML Path", "150", "Triggerable?", "75", "Trigger ID", "150" };
                    List<uncheckedData> addToGrid = new List<uncheckedData>();
                    int k = 0;
                    foreach (IDataTableField field in tableToEdit.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
                    {
                        colConf this_config = init_config.cols[k];
                        string concat_path = this_config.description + (String.IsNullOrEmpty(this_config.attrib) ? "" : ("@" + this_config.attrib));
                        uncheckedData add_new = new uncheckedData() 
                        { 
                            column_name = field.Name, 
                            source = this_config.source, 
                            xml_path = concat_path,
                            is_triggerable = this_config.firesTrigger,
                            trigger_source = this_config.triggerID
                        };
                        k++;
                        addToGrid.Add(add_new);
                    }
                    m_dataGridChamleon.ItemsSource = addToGrid;
                    m_dataGridChamleon.ItemsSource = addToGrid;

                    /* Update column names */
                    int i = 0;
                    foreach (DataGridColumn col in m_dataGridChamleon.Columns)
                    {
                        if (i > (column_names.Length - 1)) { break; }
                        col.Header = column_names[i];
                        if (i == 0) { col.IsReadOnly = true; } // So that the column_name (which is determined by the table) cannot be altered
                        col.Width = Convert.ToInt32(column_names[i + 1]);
                        if (column_names[i] == "Triggerable") { col.IsReadOnly = true; }
                        i += 2;
                    }
                    List<int> row_num_options = new List<int>();
                    for (int j = 1; j < 100; j++)
                    {
                        row_num_options.Add(j);
                    }
                    m_rowNumber.ItemsSource = row_num_options;
                    m_rowNumber.SelectedIndex = init_config.numRows - 1;
                }
            }
            else /* Add Init */
            {
                allOneRecord = true;
                m_sourcePathBox.IsEnabled = true;
                m_recordCombo.IsEnabled = false;
                m_rowNumber.Visibility = System.Windows.Visibility.Hidden;
                m_numRowsTextBlock.Visibility = System.Windows.Visibility.Hidden;
                string[] column_names = { "Column Name", "80", "XML Path", "150" };
                List<checkedData> addToGrid = new List<checkedData>();
                foreach (IDataTableField field in tableToEdit.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
                {
                    checkedData add_new = new checkedData() { Column_Name = field.Name };
                    addToGrid.Add(add_new);
                }
                m_dataGridChamleon.ItemsSource = addToGrid;
                m_dataGridChamleon.ItemsSource = addToGrid;
                int i = 0;
                foreach (DataGridColumn col in m_dataGridChamleon.Columns)
                {
                    if (i > (column_names.Length - 1)) { break; }
                    col.Header = column_names[i];
                    if (i == 0) { col.IsReadOnly = true; }
                    col.Width = Convert.ToInt32(column_names[i + 1]);
                    i += 2;
                }
                List<int> row_num_options = new List<int>();
                for (int j = 1; j < 100; j++)
                {
                    row_num_options.Add(j);
                }
                m_rowNumber.ItemsSource = row_num_options;
            }
        }
        
        void m_goToNamespaces_Click(object sender, RoutedEventArgs e)
        {
            NamespaceDialog2 nd = new NamespaceDialog2(ns_to_write);
            bool? result = nd.ShowDialog();
            if (result.Value == true) { ns_to_write = nd.ns_to_write; }
        }

        void m_saveButton_Click(object sender, RoutedEventArgs e)
        {
            string source = m_sourcePathBox.Text;
            int retries = 0;
            do
            {
                try
                {
                    /* Check if namespace ought to be specified */
                    if (allOneRecord)
                    {
                        foreach (checkedData data in m_dataGridChamleon.ItemsSource)
                        {
                            if (data.Xml_Path != null)
                            {
                                if (data.Xml_Path.Contains(':'))
                                {
                                    if (ns_to_write == null)
                                    {
                                        if (MessageBox.Show("The XML Path: " + data.Xml_Path + " contains a colon, but you have not specified a namespace. Would you like to cancel saving and specify a namespace?", "Colon Detected", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                                        {
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (uncheckedData data in m_dataGridChamleon.ItemsSource)
                        {
                            if (data.xml_path != null)
                            {
                                if (data.xml_path.Contains(':'))
                                {
                                    if (ns_to_write == null)
                                    {
                                        if (MessageBox.Show("The XML Path: " + data.xml_path + " contains a colon, but you have not specified a namespace. Would you like to cancel saving and specify a namespace?", "Colon Detected", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                                        {
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    /* TODO: More elegant way of stating the above such that we don't write a file, but we don't have to iterate twice over the same data */

                    FileStream fs = File.Open(pathToDb, FileMode.Create, FileAccess.Write, FileShare.None); /* Need to look into how this will be shared with the other process */
                    StreamWriter sw = new StreamWriter(fs);
                    string toWrite = tbl_OID + DELIM + (allOneRecord ? m_sourcePathBox.Text : "") + DELIM + (allOneRecord ? "1" : "0") + DELIM + (allOneRecord ? m_recordCombo.Text : m_rowNumber.Text);
                    sw.WriteLine(toWrite);
                    if (allOneRecord)
                    {
                        foreach (checkedData data in m_dataGridChamleon.ItemsSource)
                        {
                            /* Parsing data.xml_path for attributes */
                            string attrib = "";
                            string xml_path = "";
                            if (data.Xml_Path != null)
                            {
                                if (data.Xml_Path.Contains('@'))
                                {
                                    var parts = data.Xml_Path.Split(new char[] { '@' }, StringSplitOptions.None);
                                    xml_path = parts[0];
                                    attrib = parts[1];
                                }
                                else { xml_path = data.Xml_Path; }
                            }
                            else { xml_path = data.Xml_Path; }
                            toWrite = "." + DELIM + data.Column_Name + DELIM + xml_path + DELIM + attrib + DELIM + DELIM + "0" + DELIM + " " + DELIM;
                            sw.WriteLine(toWrite);
                        }
                    }
                    else
                    {
                        foreach (uncheckedData data in m_dataGridChamleon.ItemsSource)
                        {
                            /* Parsing data.xml_path for attributes */
                            string attrib = "";
                            string xml_path = "";
                            if (data.xml_path != null)
                            {
                                if (data.xml_path.Contains('@'))
                                {
                                    var parts = data.xml_path.Split(new char[] { '@' }, StringSplitOptions.None);
                                    xml_path = parts[0];
                                    attrib = parts[1];
                                }
                                else { xml_path = data.xml_path; }
                            }
                            else { xml_path = data.xml_path; }

                            /* Write entry to output */
                            toWrite = "." + DELIM + data.column_name + DELIM + xml_path + DELIM + attrib + DELIM + data.source + DELIM + (data.is_triggerable ? "1" : "0") + DELIM + data.trigger_source + DELIM;
                            sw.WriteLine(toWrite);
                        }
                    }
                    /* Write Namespace Manager Data */
                    if (ns_to_write != null)
                    {
                        foreach (nsConf this_conf in ns_to_write)
                        {
                            toWrite = ".." + DELIM + this_conf.ns + DELIM + this_conf.ns_source + DELIM;
                            sw.WriteLine(toWrite);
                        }
                    }
                    sw.WriteLine("...");
                    sw.Close();
                    fs.Close();
                    retries = 11;
                    this.DialogResult = true;
                    Close();
                }
                catch (IOException)
                {
                    if (retries >= 10) { throw; }
                    else
                    {
                        Thread.Sleep(250);
                        retries++;
                    }
                }
            } while (retries <= 9);
            
        }
    }
}
