using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CoolSign.API;
using CoolSign.API.Version2;
using CoolSign.API.Version2.DataAccess;

namespace ListContent
{
    public static class Program
    {
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

                    // read all Content objects from server
                    Console.Write("Reading content objects...");
                    using (IValueResult<IModelCollection<IContent>> readResult = ncSession.DataAccess.Brokers.Content.Read(ncSession.ModelFactory.CreateAllSelector(), null))
                    {
                        if (readResult.IsSuccess)
                        {
                            Console.WriteLine("success");
                            Console.WriteLine("Found {0} content objects", readResult.Value.Count);
                            Console.WriteLine("[");
                            foreach (IContent content in readResult.Value.Items)
                            {
                                Console.WriteLine("  {{Name=\"{0}\", Id=\"{1}\"}}", content.Name, content.Id);
                            }
                            Console.WriteLine("]");
                        }
                        else
                        {
                            Console.WriteLine("failed: " + readResult.ToString());
                            return -2;
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
