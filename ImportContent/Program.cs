using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Net;
using System.Threading;
using System.Text;
using CoolSign.API;
using CoolSign.API.Media;
using CoolSign.API.Version2;
using CoolSign.API.Version2.DataAccess;
using DataTableEditor;

namespace ImportContent
{
    public static class Program
    {
        public static bool interrupted = false;
        [STAThread]
        public static int Main(string[] args)
        {
            int ret = 0;
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                Program.interrupted = true;
            };
            int res;
            string ncHostname = "10.50.149.13";
            int ncPort = 80;

            IServerSession session = CSAPI.Create().CreateServerSession(ncHostname, ncPort);
            //Authenticate(session);
            bool authenticated;
            do
            {
                authenticated = Authenticate(session);
            }
            while (!authenticated);
            while (!Program.interrupted)
            {
                Console.WriteLine("Updating internal database...");
                Console.WriteLine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Properties.Settings.Default.dbfilepath));
                WatchedSets sets = new WatchedSets((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Properties.Settings.Default.dbfilepath));
                foreach (w_set toLoad in sets.all_set)
                {
                    workingConf = new SetConfig(toLoad.TABLE_DB_PATH);
                    res = importItem(session, workingConf);
                    Console.WriteLine("Updating table: " + toLoad.TABLE_NAME);
                    if (res == 2) //Server Timeout
                    {
                        Console.WriteLine("Server timeout, disconnecting . . .");
                        bool reauthen = false;
                        do
                        {
                            Thread.Sleep(5000);
                            session = CSAPI.Create().CreateServerSession(ncHostname, ncPort);
                            reauthen = Authenticate(session);
                        }
                        while (!reauthen);
                        break;
                    }
                    else if (res == -1)
                    {
                        Console.WriteLine("Authentication error, retrying . . .");
                        bool reauthen = false;
                        do
                        {
                            Thread.Sleep(5000);
                            session = CSAPI.Create().CreateServerSession(ncHostname, ncPort);
                            reauthen = Authenticate(session);
                        }
                        while (!reauthen);
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Updated: " + toLoad.TABLE_NAME);
                    }
                }
                Console.WriteLine("Finished... Sleeping...");
                Thread.Sleep(900000);
            }
            return ret;
        }

        private static SetConfig workingConf;

        private static int importItem(IServerSession session, SetConfig workingConf)
        {
            // hook API logger
            Logger.SetLogSink(HandleLogEntry);
            try
            {
                /*string ncHostname = "10.50.149.13";
                int ncPort = 80;9(*/
                Oid targetTable = new Oid(workingConf.oidForWrite);

                //using (session /*IServerSession session = CSAPI.Create().CreateServerSession(ncHostname, ncPort)*/)
                //{
                //if (false/*!Authenticate(session)*/)
                //{
                //    session.Dispose();
                //    return -1;
                //}
                //    else
                //    {
                        /* Get access to read the table */
                try
                {
                    var result = session.DataAccess.Brokers.DataTable.ReadSingle(
                        session.ModelFactory.CreateSelectorById(targetTable),
                        new IRelationshipMetaData[]
                            {
                                MetaData.DataTableToDataTableDesign, 
                                MetaData.DataTableDesignToDataTableField, 
                                MetaData.DataTableToFileInDataTable, 
                                MetaData.DataTableToDataRow
                            });
                    IDataTable datatable = result.Value;
                    if (result.Value == null) { return 2; }
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Connection/authentication error. Reauthenticating before next iteration");
                    return -1;
                }

                /* The change set is a list of all changes to the database that will be sent after all edits are done */
                IChangeSet changes = session.DataAccess.CreateChangeSet();

                /* Delete all rows from the table */
                session.DataAccess.Brokers.DataTable.DeleteDataRows(changes, targetTable, session.ModelFactory.CreateAllSelector());

                /* TODO: Allow support for appending to tables instead of a fixed number of rows */

                int i = 1;
                if (workingConf.allOneRecord == false)
                {
                    List<List<string>> col_lists = new List<List<string>>();
                    List<IChangeSet> trigger_changes = new List<IChangeSet>();
                    /* TODO: Fork the process so that general datatable updates don't have to happen unless there is a change in the state of the triggered table */
                    foreach (colConf entry in workingConf.cols)
                    {
                        List<string> col_vals = new List<string>();
                        col_vals.Add(entry.name_of_col);
                        XmlDocument xd = new XmlDocument();
                        int retries = 3;
                        do //Solves transient network issues that occur when repeatedly pulling updated XML documents
                        {
                            try
                            {
                                xd.Load(entry.source);
                                break;
                            }
                            catch (WebException)
                            {
                                Console.WriteLine("Failed to get XML file from server, retrying . . .");
                                if (retries <= 0)
                                {
                                    Console.WriteLine("Retrying failed. Skipping document until next iteration.");
                                }
                                else
                                {
                                    Thread.Sleep(300);
                                }
                            }
                        }
                        while (retries-- > 0);
                        if (retries <= 0) { break; }
                        XmlNamespaceManager nsmgr = new XmlNamespaceManager(xd.NameTable);
                        /* (Experimental) Add user specified namespaces [TODO: Should add error handling so that program can continue if it fails] */
                        /* TODO: Namespace entries do not have to be unique or related to the source using them, could be done all at once much earlier */
                        foreach (nsConf ns_info in workingConf.ns_list)
                        {
                            nsmgr.AddNamespace(ns_info.ns, ns_info.ns_source);
                        }

                        XmlNodeList found_nodes = xd.SelectNodes(entry.description, nsmgr);
                        if (entry.firesTrigger)
                        {
                            bool triggered = false;

                            /* Create new trigger_change set if column is triggerable*/
                            IChangeSet trigger_change = session.DataAccess.CreateChangeSet();
                            var result2 = session.DataAccess.Brokers.Trigger.ReadSingle(session.ModelFactory.CreateSelectorById(new Oid(entry.triggerID)), null);
                            Oid m_triggerDataTableId = result2.Value.DataTable.Id;
                            foreach (XmlNode nodeInList in found_nodes)
                            {
                                if (!triggered)
                                {
                                    triggered = true; // Prevents multiple triggers when there is more than one node in the triggerable column

                                    /* Adding a row to the trigger payload table activates the trigger */
                                    IDataRow trigger_row = session.ModelFactory.CreateDataRow();
                                    trigger_row.ActivationDate = DateTime.Now;
                                    trigger_row.ExpirationDate = DateTime.Now.AddDays(5); //I really need to figure out how to write this in more clearly. Perhaps being able to clear triggers?
                                    trigger_row.SequenceNumber = 0;

                                    session.DataAccess.Brokers.DataTable.CreateDataRow(trigger_change, m_triggerDataTableId, trigger_row);
                                    trigger_changes.Add(trigger_change);
                                }
                                if (!String.IsNullOrEmpty(entry.attrib))
                                {
                                    col_vals.Add(nodeInList.Attributes[entry.attrib].Value);
                                }
                                else
                                {
                                    string valueToAdd = String.IsNullOrEmpty(nodeInList.Value) ? nodeInList.InnerText : nodeInList.Value;
                                    col_vals.Add(valueToAdd);
                                }
                            }
                            if (!triggered) // Clears the trigger if no data is added to the table upon update
                            {
                                session.DataAccess.Brokers.DataTable.DeleteDataRows(changes, m_triggerDataTableId, session.ModelFactory.CreateAllSelector());
                                trigger_changes.Add(trigger_change);
                            }
                        }
                        else
                        {
                            foreach (XmlNode nodeInList in found_nodes)
                            {
                                if (!String.IsNullOrEmpty(entry.attrib))
                                {
                                    col_vals.Add(nodeInList.Attributes[entry.attrib].Value);
                                }
                                else
                                {
                                    string valueToAdd = String.IsNullOrEmpty(nodeInList.Value) ? nodeInList.InnerText : nodeInList.Value;
                                    col_vals.Add(valueToAdd);
                                }
                            }
                        }
                        col_lists.Add(col_vals);

                    }
                    for (int j = 1; j <= workingConf.numRows; j++)
                    {
                        /* Add new rows to the table */
                        IDataRow row = session.ModelFactory.CreateDataRow();
                        row.SequenceNumber = i;
                        row.ActivationDate = new DateTime(1601, 1, 1);
                        row.ExpirationDate = new DateTime(3000, 1, 1);
                        foreach (List<string> col in col_lists)
                        {
                            try
                            {
                                row.Values[col[0]] = col[j];
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                break;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                break;
                            }
                        }
                        session.DataAccess.Brokers.DataTable.CreateDataRow(changes, targetTable, row);
                        i++;
                    }
                    changes.Save();
                    foreach (IChangeSet trig_cha in trigger_changes)
                    {
                        trig_cha.Save();
                    }
                }
                else
                {
                    /* TODO: Add a generic trigger to sources that are all on one record */
                    /* Access the Xml data source */
                    String URLString = workingConf.sourceUrl;
                    XmlDocument xd = new XmlDocument();
                    xd.Load(URLString);
                    XmlNamespaceManager nsmgr = new XmlNamespaceManager(xd.NameTable);

                    /* (Experimental) Add user specified namespaces [Should add error handling so that program can continue if it fails] */
                    foreach (nsConf ns_info in workingConf.ns_list)
                    {
                        nsmgr.AddNamespace(ns_info.ns, ns_info.ns_source);
                    }
                    XmlNodeList nodeOfRec = xd.SelectNodes(workingConf.itemOfRec, nsmgr);
                    foreach (XmlNode node in nodeOfRec)
                    {
                        //Add new rows to the table   
                        IDataRow row = session.ModelFactory.CreateDataRow();
                        row.SequenceNumber = i;
                        row.ActivationDate = new DateTime(1601, 1, 1);
                        row.ExpirationDate = new DateTime(3000, 1, 1);
                        if (node.HasChildNodes) /* Then the items we are looking for are not attributes of the node of record (but could be attributes of a child node) */
                        {
                            foreach (XmlNode cnode in node.ChildNodes)
                            {
                                foreach (colConf testcols in workingConf.cols)
                                {
                                    if (testcols.description == cnode.Name)
                                    {
                                        if (!String.IsNullOrEmpty(testcols.attrib))
                                        {
                                            row.Values[testcols.name_of_col] = cnode.Attributes[testcols.attrib].Value;
                                        }
                                        else
                                        {
                                            row.Values[testcols.name_of_col] = cnode.Value;
                                        }
                                    }
                                }
                            }
                        }
                        else /* We are looking at the node of record's attributes only */
                        {
                            XmlAttributeCollection xac = node.Attributes;
                            foreach (XmlAttribute xa in xac)
                            {
                                foreach (colConf testcols in workingConf.cols)
                                {
                                    if (testcols.attrib == xa.Name)
                                    {
                                        row.Values[testcols.name_of_col] = xa.Value;
                                        break;
                                    }
                                }
                            }
                            session.DataAccess.Brokers.DataTable.CreateDataRow(changes, targetTable, row);
                            i++;
                        }
                    }
                    changes.Save();
                }
                return 0;
            }
            finally
            {
                // unhook logger
                Logger.SetLogSink(null);
            }
        }
        private static string parseEndOfRecord(string fullrec)
        {
            var parts = fullrec.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts[parts.Length - 1];
        }
        private static bool Authenticate(IServerSession session)
        {
            string ncUser = "jharriman";
            string ncPassword = "5l0wn!c";

            Console.Write("Authenticating... ");
            using (var result = session.Authenticate(ncUser, ncPassword))
            {
                if (result.IsSuccess)
                {
                    Console.WriteLine("success");
                    return true;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    Thread.Sleep(2000);
                    return false;
                }
            }
        }

        private static bool ImportContentFile(IServerSession session, string assetFile, ContentAssetType type)
        {
            Console.Write("Analyzing {0} file \"{1}\"...", type, assetFile);
            AssetInfo imageInfo = MediaAnalyzer.AnalyzeAssetFile(assetFile);
            if (!string.IsNullOrEmpty(imageInfo.ErrorMessage))
            {
                Console.WriteLine("failed: {0}", imageInfo.ErrorMessage);
                return false;
            }
            else
            {
                Console.WriteLine("success");
            }

            string thumbnailFile = Path.Combine(Path.GetTempPath(), new Oid().ToString() + ".jpg");
            File.WriteAllBytes(thumbnailFile, imageInfo.ThumbnailImageBytes);
            try
            {
                IContent content = session.ModelFactory.CreateContent();
                content.DurationInMilliseconds = 5000;
                content.Format = ContentFormat.Landscape;
                content.Name = type.ToString() + " Content";
                content.ResolutionX = imageInfo.Width.Value;
                content.ResolutionY = imageInfo.Height.Value;

                Console.Write("Importing {0} content...", type);
                using (var result = session.DataAccess.Brokers.Content.ImportContentFromAssetFile(assetFile, type, content, thumbnailFile, new ContentImportOptions()))
                {
                    if (result.IsSuccess)
                    {
                        Console.WriteLine("success");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("failed: {0}", result.ToString());
                        return false;
                    }
                }
            }
            finally
            {
                File.Delete(thumbnailFile);
            }
        }

        private static void HandleLogEntry(Logger.LogEntry entry)
        {
            // we're just going to echo the logs to the debug output pane in the VS IDE, but you could append them to a file or re-route them into whatever log system you use
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(entry.ToString());
            }
        }
    }
}
/* Need info for each source, probs a foreach loop that loops over different source, add functionality later! */
/*while (reader.Read())
{
    switch (reader.NodeType)
    {
        case XmlNodeType.Element:
            if (workingConf.allOneRecord == false) //Why am I doing this one first, it is a much more difficult problem
            {
                //Add new rows to the table   
                IDataRow row = session.ModelFactory.CreateDataRow();
                row.SequenceNumber = i;
                row.ActivationDate = new DateTime(1601, 1, 1);
                row.ExpirationDate = new DateTime(3000, 1, 1);
                bool nodeUsed = false;
                foreach (colConf testcols in workingConf.cols)
                {
                    if (reader.Name == parseEndOfRecord(testcols.description))
                    {
                        if (!String.IsNullOrEmpty(testcols.attrib))
                        {
                            row.Values[testcols.name_of_col] = reader.GetAttribute(testcols.name_of_col);
                        }
                        else
                        {
                            row.Values[testcols.name_of_col] = reader.Value;
                        }
                        nodeUsed = true;
                    }
                }
                if (nodeUsed)
                {
                    session.DataAccess.Brokers.DataTable.CreateDataRow(changes, targetTable, row);
                    i++;
                }
                break;
            }
            else
            {
                if (reader.Name = testing)
                if () //Why am I doing this one first, it is a much more difficult problem
                {
                    // Add new rows to the table 
                    IDataRow row = session.ModelFactory.CreateDataRow();
                    row.SequenceNumber = i;
                    row.ActivationDate = new DateTime(1601, 1, 1);
                    row.ExpirationDate = new DateTime(3000, 1, 1);
                    bool nodeUsed = false;
                    foreach (colConf testcols in workingConf.cols)
                    {
                        if (reader.Name == parseEndOfRecord(testcols.description))
                        {
                            if (!String.IsNullOrEmpty(testcols.attrib))
                            {
                                row.Values[testcols.name_of_col] = reader.GetAttribute(testcols.name_of_col);
                            }
                            else
                            {
                                row.Values[testcols.name_of_col] = reader.Value;
                            }
                            nodeUsed = true;
                        }
                    }
                    if (nodeUsed)
                    {
                        session.DataAccess.Brokers.DataTable.CreateDataRow(changes, targetTable, row);
                        i++;
                    }
                    break;
                }
                ///*
                if (reader.Name == parseEndOfRecord(workingConf.cols[0].description))
                {
                    // Add new rows to the table
                    IDataRow row = session.ModelFactory.CreateDataRow();
                    row.SequenceNumber = i;
                    row.ActivationDate = new DateTime(1601, 1, 1);
                    row.ExpirationDate = new DateTime(3000, 1, 1);
                    foreach (IDataTableField field in datatable.DataTableDesigns.Items.FirstOrDefault().DataTableFields.Items)
                    {
                        row.Values[field.Name] = reader.GetAttribute(field.Name);
                    }
                    session.DataAccess.Brokers.DataTable.CreateDataRow(changes, targetTable, row);
                    i++;
                }
                break;
                //
            }
        default:
            break;

    }
}*/
