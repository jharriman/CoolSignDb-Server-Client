using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CoolSign.API;
using CoolSign.API.Version2;
using CoolSign.API.Version2.DataAccess;

namespace FireTrigger
{
    public static class Program
    {
        private const string TRIGGER_ID = "7B6BA7B7-DA40-4859-8305-3E6D5F40640F";

        private static Oid g_triggerDataTableId;

        public static int Main(string[] args)
        {
            // hook API logger
            Logger.SetLogSink(HandleLogEntry);

            try
            {
                string ncHostname = "JoshVM-2003";
                int ncPort = 80;

                using (IServerSession ncSession = CSAPI.Create().CreateServerSession(ncHostname, ncPort))
                {
                    if (!Authenticate(ncSession))
                    {
                        return -1;
                    }

                    // find datatable id for trigger payload
                    if (!IdentifyTriggerPayloadDataTable(ncSession))
                    {
                        return -2;
                    }

                    // clear out old triggers -- you don't have to do this, payload rows will be automatically GC'd after their expiration date; this is just an example of how to synchronously deactivate triggers
                    if (!DeactivateTriggers(ncSession))
                    {
                        return -3;
                    }

                    // activate trigger
                    if (!ActivateTrigger(ncSession))
                    {
                        return -4;
                    }
                }

                return 0;
            }
            finally
            {
                Console.WriteLine("Press enter to continue...");
                Console.ReadLine();

                // unhook logger
                Logger.SetLogSink(null);
            }
        }

        private static bool Authenticate(IServerSession session)
        {
            string ncUser = "admin";
            string ncPassword = "password";

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
                    return false;
                }
            }
        }

        private static bool IdentifyTriggerPayloadDataTable(IServerSession session)
        {
            Console.Write("Looking up payload table for trigger...");
            using (var result = session.DataAccess.Brokers.Trigger.ReadSingle(session.ModelFactory.CreateSelectorById(new Oid(TRIGGER_ID)), null))
            {
                if (result.IsSuccess)
                {
                    Console.WriteLine("success: " + result.Value.DataTable.Id);
                    g_triggerDataTableId = result.Value.DataTable.Id;
                    return true;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    return false;
                }
            }
        }

        private static bool DeactivateTriggers(IServerSession session)
        {
            Console.Write("Deleting existing rows from trigger payload table...");

            IChangeSet changes = session.DataAccess.CreateChangeSet();

            session.DataAccess.Brokers.DataTable.DeleteDataRows(changes, g_triggerDataTableId, session.ModelFactory.CreateAllSelector());

            using (var result = changes.Save())
            {
                if (result.IsSuccess)
                {
                    Console.WriteLine("success");
                    return true;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    return false;
                }
            }
        }

        private static bool ActivateTrigger(IServerSession session)
        {
            Console.Write("Adding row to trigger payload table...");

            IChangeSet changes = session.DataAccess.CreateChangeSet();

            IDataRow row = session.ModelFactory.CreateDataRow();
            row.ActivationDate = DateTime.Now;
            row.ExpirationDate = DateTime.Now.AddMinutes(1);// short expirations are good on triggers, because they usually represent timely events and in the case of a player being offline you don't want it to get a wave of stale triggers
            row.SequenceNumber = 0;
            row.Values["foo"] = "bar";// the column names depend upon the actual columns in the trigger

            session.DataAccess.Brokers.DataTable.CreateDataRow(changes, g_triggerDataTableId, row);

            using (var result = changes.Save())
            {
                if (result.IsSuccess)
                {
                    Console.WriteLine("success");
                    return true;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    return false;
                }
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
