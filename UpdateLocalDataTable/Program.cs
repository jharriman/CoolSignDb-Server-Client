using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CoolSign.API;
using CoolSign.API.Version2;
using CoolSign.API.Version2.DataAccess;

namespace UpdateLocalDataTable
{
    public static class Program
    {
        private const string HOST = "JoshVM-XP";
        private const int PORT = 80;

        // this sample assumes you already know the id of the data table you want to modify
        private static readonly Oid TABLE_ID = new Oid("DADF87E4-4463-43CD-93C6-A77512E800E1");

        // this depends on the schema of your data table
        private const string FIELD_1 = "Text1";

        public static int Main(string[] args)
        {
            Logger.SetLogSink(HandleLogEntry);

            try
            {
                CSAPI api = CSAPI.Create();

                // first create a node connection directly to the node whose local data store we want to modify
                using (INodeConnection tnConn = api.CreateNodeConnection(HOST, PORT))
                {
                    // we need a local session context to use the local data table apis
                    Console.Write("Establishing local session with node...");
                    using (var result = tnConn.CreateLocalSession())
                    {
                        if (result.IsSuccess)
                        {
                            Console.WriteLine("success");
                        }
                        else
                        {
                            Console.WriteLine("failed: " + result.ToString());
                            return 1001;
                        }
                    }

                    // we need to figure out if the table already exists in the local store or not, so we'll try to read it from there first
                    bool isAlreadyInLocalStore;
                    Console.Write("Reading table from local data store...");
                    using (var result = tnConn.LocalData.ReadLocalDataTable(TABLE_ID))
                    {
                        if (result.IsSuccess)
                        {
                            Console.WriteLine("found it");
                            isAlreadyInLocalStore = true;
                        }
                        else if (result.ErrorType == CommandErrorType.ServerError && result.ServerErrorCode == ServerErrorType.ObjectNotFound)
                        {
                            Console.WriteLine("doesn't exist");
                            isAlreadyInLocalStore = false;
                        }
                        else
                        {
                            Console.WriteLine("failed: " + result.ToString());
                            return 1002;
                        }
                    }

                    // this is the object which will contain all the changes we're going to make to the table
                    IDataTableChangeSet changes = tnConn.LocalData.CreateDataTableChangeSet();

                    // if the table does not yet exist in the local store, then we must save the schema with the update; if it does already exist then we can just update rows
                    if (!isAlreadyInLocalStore)
                    {
                        // what is the schema?  we'll pull it from the network cached version of the table (the one downloaded from the parent node)
                        //  for this to work, the table would need to be associated with the node in some way.  E.g. if it were bound to a content in the schedule of this node, or one if this node's descendants.
                        Console.Write("Reading table from network cache...");
                        using (var result = tnConn.LocalData.ReadCachedDataTable(TABLE_ID))
                        {
                            if (result.IsSuccess)
                            {
                                Console.WriteLine("found it");
                                IDataTable networkCopy = result.Value;

                                // add the table & fields to our local store changes
                                changes.UpdateTable(networkCopy);
                                foreach (IDataTableField field in networkCopy.DataTableDesigns.Items.First().DataTableFields.Items)
                                {
                                    changes.UpdateField(field);
                                }
                            }
                            else if (result.ErrorType == CommandErrorType.ServerError && result.ServerErrorCode == ServerErrorType.ObjectNotFound)
                            {
                                Console.WriteLine("doesn't exist, aborting");
                                return 1003;
                            }
                            else
                            {
                                Console.WriteLine("failed: " + result.ToString());
                                return 1004;
                            }
                        }
                    }

                    // first delete all existing rows, you may not want to do this if you're appending a row
                    changes.DeleteAllRows();

                    // now add the new row
                    IDataRow row = tnConn.ModelFactory.CreateDataRow();
                    row.ActivationDate = new DateTime(1600, 1, 1);
                    row.ExpirationDate = new DateTime(3000, 1, 1);
                    row.SequenceNumber = 0;
                    row.Values[FIELD_1] = "some value";
                    changes.UpdateRow(row);

                    // save changes into local store
                    Console.Write("Updating table in local store...");
                    using (var result = tnConn.LocalData.UpdateLocalDataTable(TABLE_ID, changes, false))
                    {
                        if (result.IsSuccess)
                        {
                            Console.WriteLine("success");
                        }
                        else
                        {
                            Console.WriteLine("failed: " + result.ToString());
                            return 1005;
                        }
                    }
                }
            }
            finally
            {
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();

                Logger.SetLogSink(null);
            }

            return 0;
        }

        private static void HandleLogEntry(Logger.LogEntry entry)
        {
            // write log message to debug output window in VS
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(entry.ToString());
            }
        }
    }
}
