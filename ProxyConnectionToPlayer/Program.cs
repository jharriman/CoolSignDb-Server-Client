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
        // the id of a player node somewhere in the network
        private const string PLAYER_NODE_ID = "16F298ED-9C6B-4691-9935-6C69A00D2CF7";

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

                    Console.Write("Creating proxied connection...");
                    INodeConnection playerConn;
                    using (var result = ncSession.CreateProxiedConnectionToNode((Oid)PLAYER_NODE_ID))
                    {
                        if (result.IsSuccess)
                        {
                            Console.WriteLine("success");
                            playerConn = result.Value;
                        }
                        else
                        {
                            Console.WriteLine("failed: " + result.ToString());
                            return -2;
                        }
                    }

                    Console.Write("Querying proxied node info...");
                    playerConn.SocketTimeout = 30;
                    using (var result = playerConn.ExecuteCommand("Coolsign.System.GetInfo", null))
                    {
                        if (result.IsSuccess)
                        {
                            Console.WriteLine("success" + Console.Out.NewLine + result.ReadBody());
                        }
                        else
                        {
                            Console.WriteLine("failed: " + result.ToString());
                            return -3;
                        }
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
