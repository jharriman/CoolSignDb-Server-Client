using System;
using System.Collections.Generic;
using System.Diagnostics;
using CoolSign.API;
using CoolSign.API.Version1;
using CoolSign.API.Version1.DataAccess;

namespace RetrieveNodeAttributesFromALocalNode
{
    public static class Program
    {
        private const string ATTRIBUTE_NAME = "MyAttribute";

        public static int Main(string[] args)
        {
            // hook API logger
            Logger.SetLogSink(HandleLogEntry);

            try
            {
                // in most cases this would just be localhost, but I'm actually connecting to a remote node
                string hostname = "JoshVM-XP";
                int port = 80;

                // we use INodeConnection here because this node does not have to be (and probably is not) the NC
                Console.WriteLine("Connecting to node...");
                using (INodeConnection conn = CSAPI.Create().CreateNodeConnection(hostname, port))
                {
                    // pull the data local to this node (part of which are the node attributes)
                    Console.Write("Retrieving node data...");
                    using (var result = conn.LocalData.ReadNodeData())
                    {
                        if (result.IsSuccess)
                        {
                            Console.WriteLine("success, {0} node attributes found", result.Value.Attributes.Count);
                            foreach (string attrName in result.Value.Attributes.Keys)
                            {
                                Console.WriteLine("  NodeAttribute {{ Name = \"{0}\", Value = \"{1}\" }}", attrName, result.Value.Attributes[attrName]);
                            }
                        }
                        else
                        {
                            Console.WriteLine("failed: " + result.ToString());
                            return -1;
                        }
                    }
                }
                return 0;
            }
            finally
            {
                // unhook API logger
                Logger.SetLogSink(null);
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
