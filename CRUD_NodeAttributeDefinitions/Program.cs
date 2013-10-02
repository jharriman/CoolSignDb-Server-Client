using System;
using System.Collections.Generic;
using System.Diagnostics;
using CoolSign.API;
using CoolSign.API.Version1;
using CoolSign.API.Version1.DataAccess;

namespace CRUD_NodeAttributeDefinitions
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
                string ncHostname = "JoshVM-2003";
                int ncPort = 80;

                using (IServerSession ncSession = CSAPI.Create().CreateServerSession(ncHostname, ncPort))
                {
                    if (!Authenticate(ncSession))
                    {
                        return -1;
                    }

                    // read all node attribute definitions
                    var attrDefs = ReadAllAttributeDefs(ncSession);
                    if (null == attrDefs)
                    {
                        return -2;
                    }

                    // delete any defs with my name
                    if (!DeleteMyAttributes(ncSession))
                    {
                        return -3;
                    }

                    // read all node attribute definitions
                    attrDefs = ReadAllAttributeDefs(ncSession);
                    if (null == attrDefs)
                    {
                        return -2;
                    }

                    // create my custom node attribute defintion
                    if (!CreateMyAttribute(ncSession))
                    {
                        return -4;
                    }

                    // read all node attribute definitions
                    attrDefs = ReadAllAttributeDefs(ncSession);
                    if (null == attrDefs)
                    {
                        return -2;
                    }

                    // update the default value of my node attribute definition
                    if (!UpdateMyAttribute(ncSession))
                    {
                        return -5;
                    }

                    // read all node attribute definitions
                    attrDefs = ReadAllAttributeDefs(ncSession);
                    if (null == attrDefs)
                    {
                        return -2;
                    }

                    // find a node id (you'll probably already know the node id or name you're interested in, but here's how to do it)
                    Oid nodeId = FindANodeId(ncSession);
                    if (null == nodeId)
                    {
                        return -6;
                    }

                    // read the current values of the node's attributes
                    if (!ReadNodeAttributeValues(nodeId, ncSession))
                    {
                        return -7;
                    }

                    // set the value of my node attribute for the node
                    if (!SetNodeAttributeValue(nodeId, "new value", ncSession))
                    {
                        return -8;
                    }

                    // read the current values of the node's attributes
                    if (!ReadNodeAttributeValues(nodeId, ncSession))
                    {
                        return -7;
                    }
                }

                return 0;
            }
            finally
            {
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

        private static ICollection<INodeAttributeDefinition> ReadAllAttributeDefs(IServerSession session)
        {
            Console.Write("Reading NodeAttributeDefinitions... ");
            using (var result = session.DataAccess.Brokers.NodeAttributeDefinition.Read(null, null))
            {
                if (result.IsSuccess)
                {
                    Console.WriteLine("found {0}", result.Value.Count);
                    foreach (var attr in result.Value.Items)
                    {
                        Console.WriteLine("  NodeAttributeDef {{ Name = \"{0}\", DefaultValue = \"{1}\" }}", attr.Name, attr.DefaultValue);
                    }
                    return result.Value.Items;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    return null;
                }
            }
        }

        private static bool DeleteMyAttributes(IServerSession session)
        {
            var attrBroker = session.DataAccess.Brokers.NodeAttributeDefinition;
            var dacFactory = session.DataAccess.ModelFactory;
            IChangeSet changes = session.DataAccess.CreateChangeSet();
            attrBroker.DeleteNodeAttributeDefinitions(changes, dacFactory.CreateSelectorByFilter(dacFactory.CreateFilterExpression("Name LIKE \"" + ATTRIBUTE_NAME + "\"")));

            Console.Write("Deleting my NodeAttributeDefinitions... ");
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

        private static bool CreateMyAttribute(IServerSession session)
        {
            var attrBroker = session.DataAccess.Brokers.NodeAttributeDefinition;
            IChangeSet changes = session.DataAccess.CreateChangeSet();
            INodeAttributeDefinition attr = session.DataAccess.ModelFactory.CreateNodeAttributeDefinition();
            attr.Id = new Oid();
            attr.Name = ATTRIBUTE_NAME;
            attr.DefaultValue = "hi";
            attrBroker.CreateNodeAttributeDefinition(changes, attr);

            Console.Write("Creating my NodeAttributeDefinition... ");
            using (var result = changes.Save())
            {
                if (result.IsSuccess)
                {
                    // extract the actual id of the created attr def
                    Oid newId = result.IdMap[attr.Id];
                    Console.WriteLine("success, my attribute def has id {0}", newId);
                    return true;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    return false;
                }
            }
        }

        private static bool UpdateMyAttribute(IServerSession session)
        {
            var attrBroker = session.DataAccess.Brokers.NodeAttributeDefinition;
            var dacFactory = session.DataAccess.ModelFactory;
            IChangeSet changes = session.DataAccess.CreateChangeSet();
            INodeAttributeDefinition attr = dacFactory.CreateNodeAttributeDefinition();
            attr.DefaultValue = "hello";
            attrBroker.UpdateNodeAttributeDefinitions(changes, attr, dacFactory.CreateSelectorByFilter(dacFactory.CreateFilterExpression("Name LIKE \"" + ATTRIBUTE_NAME + "\"")));

            Console.Write("Updating my NodeAttributeDefinition... ");
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

        private static Oid FindANodeId(IServerSession session)
        {
            Console.Write("Finding a Node... ");
            using (var result = session.DataAccess.Brokers.Node.Read(null, null))
            {
                if (result.IsSuccess)
                {
                    foreach (var node in result.Value.Items)
                    {
                        Console.WriteLine("found Node with id {0}", node.Id);
                        return node.Id;
                    }
                    Console.WriteLine("there are no Nodes in the network!");
                    return null;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    return null;
                }
            }
        }

        private static bool ReadNodeAttributeValues(Oid nodeId, IServerSession session)
        {
            Console.Write("Reading attribute values for Node {0}... ", nodeId);
            using (var result = session.DataAccess.Brokers.Node.ReadSingle(session.DataAccess.ModelFactory.CreateSelectorById(nodeId), null))
            {
                if (result.IsSuccess)
                {
                    Console.WriteLine("found {0} attribute values", result.Value.Attributes.Count);
                    foreach (string attrName in result.Value.Attributes.Keys)
                    {
                        Console.WriteLine("  Attribute {{ Name = \"{0}\", Value = \"{1}\" }}", attrName, result.Value.Attributes[attrName]);
                    }
                    return true;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    return false;
                }
            }
        }

        private static bool SetNodeAttributeValue(Oid nodeId, string value, IServerSession session)
        {
            IModelFactory dacFactory = session.DataAccess.ModelFactory;
            INodeBroker nodeBroker = session.DataAccess.Brokers.Node;
            ISelector nodeIdSel = dacFactory.CreateSelectorById(nodeId);

            Console.WriteLine("Updating attribute values for Node {0}... ", nodeId);
            NodeAttributeValues attrValues;
            using (var result = nodeBroker.ReadSingle(nodeIdSel, null))
            {
                if (result.IsSuccess)
                {
                    attrValues = result.Value.Attributes;
                }
                else
                {
                    Console.WriteLine("failed: " + result.ToString());
                    return false;
                }
            }
            IChangeSet changes = session.DataAccess.CreateChangeSet();
            INode node = session.DataAccess.ModelFactory.CreateNode();
            node.Attributes = attrValues;
            node.Attributes[ATTRIBUTE_NAME] = value;
            nodeBroker.UpdateNodes(changes, node, nodeIdSel);
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
