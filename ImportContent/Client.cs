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
using NetworkClasses;

namespace ImportContent
{
    public class Client
    {
        private LogClass logging;
        private WatchedSets sets;
        private CoolSignSets cs_sets;
        private string backup_path = (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Properties.Settings.Default.dbfilepath);
        public Client(TcpClient s, LogClass logger, WatchedSets w_sets, CoolSignSets cs_sets_in)
        {
            cs_sets = cs_sets_in;
            sets = w_sets;
            logging = logger;
            Thread clientThread = new Thread(handleClient);
            clientThread.Start(s);

        }
        public void handleClient(object data)
        {
            TcpClient s = (TcpClient)data;
            NetworkStream netStream = s.GetStream();
            IFormatter binForm = new BinaryFormatter();
            /* Continue serving client until disconnect */
            while (true)
            {
                try
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
                        case 4: // Client requests information on a Table (Generate init file with only column info if there is not config on file)
                            {
                                sendTableConfig(netStream);
                                break;
                            }
                        case 5: // Client requests list of all current CoolSign DataTables
                            {
                                sendCSTables(netStream);
                                break;
                            }
                        case 6: // Client requests log report (Authentication, possibly?)
                            {
                                break;
                            }
                        default:
                            {
                                logging.write("Invalid message from client", s.Client.RemoteEndPoint.ToString());
                                Console.WriteLine("Invalid communiction");
                                /* Send message back to the client */
                                break;
                            }
                    }
                }
                catch (SerializationException e)
                {
                    s.Close();
                    logging.write(e.Message, s.Client.RemoteEndPoint.ToString());
                    return;
                }
                catch (SocketException e)
                {
                    s.Close();
                    logging.write(e.Message, s.Client.RemoteEndPoint.ToString());
                    return;
                }
            }
        }

        public void retSig(NetworkStream netStream, int response)
        {
            safeWrite<int>(netStream, response);
        }

        public void sendCSTables(NetworkStream netStream)
        {
            safeWrite<List<avail_table>>(netStream, cs_sets.available_tables);
        }

        public void sendTableConfig(NetworkStream netStream)
        {
            string table_oid = safeRead<string>(netStream);
            SetConfig to_send = sets.isInWatched(table_oid);
            if (to_send != null)
            {
                safeWrite<int>(netStream, 7);
                safeWrite<setProps>(netStream, to_send.all_props);
            }
            else
            {
                safeWrite<int>(netStream, 8);
            }
        }
        public int removeSet(NetworkStream netStream)
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
        public int addNewSet(NetworkStream netStream)
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
        public int editExisting(NetworkStream netStream)
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

        public T safeRead<T>(NetworkStream netStream)
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
        public void safeWrite<T>(NetworkStream netStream, T msg)
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

        public List<colConf> in_set;
    }
}
