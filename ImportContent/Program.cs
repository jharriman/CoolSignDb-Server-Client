using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using CoolSign.API;
using CoolSign.API.Media;
using CoolSign.API.Version2;
using CoolSign.API.Version2.DataAccess;
using ConfigClasses;

namespace ImportContent
{
    public static class Program
    {
        public static bool interrupted = false;
        private static WatchedSets sets;
        private static CoolSignSets cs_sets;
        private static string backup_path = (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Properties.Settings.Default.dbfilepath);
        [STAThread]
        public static int Main(string[] args)
        {
            /* Read in database from file */
            Console.WriteLine("Reading Database ........");
            sets = new WatchedSets(backup_path);

            /* Initialize CoolSign Table List */
            cs_sets = new CoolSignSets();
            
            try
            {
                IPAddress ipAd = IPAddress.Parse("128.135.167.97");
                TcpClient tcpclnt = new TcpClient();
                TcpListener listen = new TcpListener(ipAd, 8001);
                listen.Start();
                Console.WriteLine("The server is running at port 8001...");
                Console.WriteLine("The local end point is: " +
                                  listen.LocalEndpoint);
                Console.WriteLine("Waiting for a connection.....");

                while (true)
                {
                    TcpClient s = listen.AcceptTcpClient();
                    Console.WriteLine("Connection accepted from " + s.Client.RemoteEndPoint);
                    Thread clientThread = new Thread(Program.handleClient);
                    clientThread.Start(s);
                }
                /* clean up */
                listen.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
            }    
            //return doWork(); // doWork should be an ongoing thread [Note: we will need some sort of mutex in order to synchronize structural edits from outside users.]
            return 1; //Remove after constructing server routine
        }

        public static void handleClient(object data)
        {
            TcpClient s = (TcpClient)data;
            NetworkStream netStream = s.GetStream();
            IFormatter binForm = new BinaryFormatter();
            /* Continue serving client until disconnect */
            while (true)
            {
                int command = safeRead<int>(netStream);
                netStream.Flush();
                switch (command)
                {
                    case 1: //edit existing set and ensure edit is valid
                        {
                            retSig(netStream, editExisting(netStream));
                            break;
                        }
                    case 2: // add a new set config to the watched sets
                        {
                            retSig(netStream, addNewSet(netStream));
                            break;
                        }
                    case 3: // Remove a configuration from the set
                        {
                            retSig(netStream, removeSet(netStream));
                            break;
                        }
                    case 4: // Client requests list of WatchedSets information
                        {
                            sendWatchedSets(netStream);
                            break;
                        }
                    case 5: // Client requests list of all current CoolSign DataTables
                        {
                            Console.Write("Received 5, sending . . . ");
                            sendCSTables(netStream);
                            break;
                        }
                    case 6: // Client requests log report (Authentication, possibly?)
                        {
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Invalid communiction");
                            /* Send message back to the client */
                            break;
                        }
                }
            }
        }

        public static void retSig(NetworkStream netStream, int response)
        {
           safeWrite<int>(netStream, response);
        }

        public static void sendCSTables(NetworkStream netStream)
        {
            safeWrite<System.Collections.Generic.ICollection<CoolSign.API.Version1.DataAccess.IDataTable>>(netStream, cs_sets.available_tables);
        }

        public static void sendWatchedSets(NetworkStream netStream)
        {
            safeWrite<List<w_set>>(netStream, sets.all_set);
            safeWrite<List<SetConfig>>(netStream, sets.configs);
        }
        public static int removeSet(NetworkStream netStream)
        {
            try
            {
                string oid_from_client = safeRead<string>(netStream);
                w_set inSet;
                if ((inSet = sets.w_setInAllSet(oid_from_client)) != null)
                {
                    SetConfig toRemove = sets.isInWatched(oid_from_client);
                    sets.all_set.Remove(inSet);
                    sets.configs.Remove(toRemove);
                    /* TODO : Set up server-side logging */

                    /* Force immediate backup */
                    sets.backupDb(backup_path);
                    return 0;
                }
                else
                {
                    return 1;
                    /* TODO : Send Error to client */
                }
            }
            catch (Exception e)
            {
                return 2;
            }

        }
        public static int addNewSet(NetworkStream netStream)
        {
            try
            {
                w_set setToAdd = safeRead<w_set>(netStream);
                setProps propsToSend = safeRead<setProps>(netStream);
                SetConfig newConfig = new SetConfig("", true);
                SetConfig isInConfigs;
                if ((isInConfigs = sets.isInWatched(propsToSend.oidForWrite)) == null)
                {
                    /* TODO: Test configuration to make sure it runs without errors before adding it to the server */

                    newConfig.all_props = propsToSend;
                    sets.all_set.Add(setToAdd);
                    sets.configs.Add(newConfig);

                    // DEBUG: Print set in List
                    newConfig.printConfig();

                    /* Force immediate backup */
                    sets.backupDb(backup_path);

                    return 0;
                }
                else
                {
                    return 1;
                    /* TODO: Notify client that configuration is already in the set */
                }
            }
            catch (Exception e)
            {
                // Notify client, and do nothing
                return 2;
            }
        }
        public static int editExisting(NetworkStream netStream)
        {
            try
            {
                setProps propsFromSend = safeRead<setProps>(netStream);
                SetConfig setInList;
                if ((setInList = sets.isInWatched(propsFromSend.oidForWrite)) != null)
                {
                    /* TODO: Test configuration to make sure it runs without errors before adding it to the server */

                    /* Make sure the set isn't being edited by someone else */
                    setInList.set_mutex.WaitOne();

                    /* Put the edited setings into the configuation class */
                    setInList.all_props = propsFromSend;

                    // DEBUG: Print set in List
                    setInList.printConfig();

                    /* Force a backup immediately after change has been accepted */
                    sets.backupDb(backup_path);

                    /* TODO: Add a record to the log */

                    setInList.set_mutex.ReleaseMutex();
                    return 0;
                }
                else
                {
                    return 1;
                    /* TODO: Warn client that this set is not in the list */
                }
            }
            catch (Exception e)
            {
                // Notify client, and add event to log
                return 2;
                Console.ReadLine();
            }
        }

        public static T safeRead<T>(NetworkStream netStream)
        {
            BinaryFormatter binForm = new BinaryFormatter();
            byte[] msgLen = new byte[4];
            netStream.Read(msgLen, 0, 4);
            int dataLen = BitConverter.ToInt32(msgLen, 0);

            byte[] msgData = new byte[dataLen];
            int dataRead = 0;
            do
            {
                dataRead += netStream.Read(msgData, dataRead, (dataLen - dataRead));

            } while (dataRead < dataLen);
            // Code above from: http://stackoverflow.com/questions/2316397/sending-and-receiving-custom-objects-using-tcpclient-class-in-c-sharp
            
            MemoryStream memStream = new MemoryStream(msgData);
            T objFromSend = (T)binForm.Deserialize(memStream);
            return objFromSend;
        }
        public static void safeWrite<T>(NetworkStream netStream, T msg)
        {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter binForm = new BinaryFormatter();
            binForm.Serialize(ms, msg);
            byte[] bytesToSend = ms.ToArray();
            byte[] dataLen = BitConverter.GetBytes((Int32)bytesToSend.Length);
            netStream.Write(dataLen, 0, 4);
            netStream.Write(bytesToSend, 0, bytesToSend.Length);
            netStream.Flush();
        }

        public static List<colConf> in_set;

        public static int doWork()
        {
            /* TODO: Divide this work into proper units */
            int ret = 0;
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                Program.interrupted = true;
                e.Cancel = true;
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
                foreach (w_set toLoad in sets.all_set)
                {
                    workingConf = new SetConfig(toLoad.TABLE_DB_PATH, false);
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
                Oid targetTable = new Oid(workingConf.all_props.oidForWrite);

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
                if (workingConf.all_props.allOneRecord == false)
                {
                    List<List<string>> col_lists = new List<List<string>>();
                    List<IChangeSet> trigger_changes = new List<IChangeSet>();
                    /* TODO: Fork the process so that general datatable updates don't have to happen unless there is a change in the state of the triggered table */
                    foreach (colConf entry in workingConf.all_props.cols)
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
                        foreach (nsConf ns_info in workingConf.all_props.ns_list)
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
                    for (int j = 1; j <= workingConf.all_props.numRows; j++)
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
                    String URLString = workingConf.all_props.sourceUrl;
                    XmlDocument xd = new XmlDocument();
                    xd.Load(URLString);
                    XmlNamespaceManager nsmgr = new XmlNamespaceManager(xd.NameTable);

                    /* (Experimental) Add user specified namespaces [Should add error handling so that program can continue if it fails] */
                    foreach (nsConf ns_info in workingConf.all_props.ns_list)
                    {
                        nsmgr.AddNamespace(ns_info.ns, ns_info.ns_source);
                    }
                    XmlNodeList nodeOfRec = xd.SelectNodes(workingConf.all_props.itemOfRec, nsmgr);
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
                                foreach (colConf testcols in workingConf.all_props.cols)
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
                                foreach (colConf testcols in workingConf.all_props.cols)
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
