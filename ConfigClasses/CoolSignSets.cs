using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Windows;
using CoolSign.API;
using CoolSign.API.Version1;

namespace ConfigClasses
{
    public class CoolSignSets
    {
        private CSAPI m_api;
        private string m_host;
        private int m_port;
        private IServerSession m_session;
        private string error;
        private string db_path = (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + "cs_dt.bin");
        public System.Collections.Generic.ICollection<CoolSign.API.Version1.DataAccess.IDataTable> available_tables;
        public CoolSignSets()
        {
            createSession();
            /* Setup backup thread */
            Thread backupRoutine = new Thread(refreshRoutine);
            backupRoutine.Start();
        }
        public void createSession()
        {
            m_api = CSAPI.Create();
            m_host = "10.50.149.13";
            m_port = 80;
            m_session = m_api.CreateServerSession(m_host, m_port);
            bool isAuthenticated = false;
            try
            {
                using (var result = m_session.Authenticate("jharriman", "5l0wn!c"))
                {
                    if (result.IsSuccess)
                    {
                        RefreshTableList();
                    }
                    else
                    {
                        if (result.Result == AuthenticateResultType.AuthFailed)
                        {
                            error = "Invalid username/password";
                        }
                        else if (result.Result == AuthenticateResultType.ServerRefused)
                        {
                            error = "Server rejected authentication, could be starting up";
                        }
                        else
                        {
                            error = "Failed login: " + result.ToString();
                        }
                        available_tables = safeRead<System.Collections.Generic.ICollection<CoolSign.API.Version1.DataAccess.IDataTable>>(db_path);
                    }
                }
            }
            finally
            {
                if (!isAuthenticated)
                {
                    m_session.Dispose();
                    m_session = null;
                }
            }
        }
        public void RefreshTableList()
        {
            using (var result = m_session.DataAccess.Brokers.DataTable.Read(m_session.ModelFactory.CreateAllSelector(), null))
            {
                if (result.IsSuccess)
                {
                    available_tables = result.Value.Items;
                    error = "";
                }
                else
                {
                    error = "Failed to read DataTable list: " + result.ToString();
                }
            }
        }
        public void refreshRoutine()
        {
            try
            {
                while (true)
                {
                    RefreshTableList();
                    backupTableList();
                    Thread.Sleep(60000);
                }
            }
            catch (Exception)
            {
                createSession();
            }
        }
        public void forceRefresh()
        {
            /* TODO: Might need a mutex for this */
            RefreshTableList();
            backupTableList();
        }
        public void backupTableList()
        {
            safeWrite<System.Collections.Generic.ICollection<CoolSign.API.Version1.DataAccess.IDataTable>>(db_path, available_tables);
        }

        public static T safeRead<T>(string file_path)
        {
            FileStream file = new FileStream(file_path, FileMode.Open, FileAccess.Read);
            byte[] bytes = new byte[file.Length];
            MemoryStream ms = new MemoryStream();
            file.Read(bytes, 0, (int)file.Length);
            ms.Write(bytes, 0, (int)file.Length);

            BinaryFormatter binForm = new BinaryFormatter();
            byte[] msgLen = new byte[4];
            ms.Read(msgLen, 0, 4);
            int dataLen = BitConverter.ToInt32(msgLen, 0);

            byte[] msgData = new byte[dataLen];
            int dataRead = 0;
            do
            {
                dataRead += ms.Read(msgData, dataRead, (dataLen - dataRead));

            } while (dataRead < dataLen);
            // Code above from: http://stackoverflow.com/questions/2316397/sending-and-receiving-custom-objects-using-tcpclient-class-in-c-sharp

            MemoryStream memStream = new MemoryStream(msgData);
            T objFromSend = (T)binForm.Deserialize(memStream);
            return objFromSend;
        }
        public static void safeWrite<T>(string file_path, T msg)
        {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter binForm = new BinaryFormatter();
            binForm.Serialize(ms, msg);
            byte[] bytesToSend = ms.ToArray();
            byte[] dataLen = BitConverter.GetBytes((Int32)bytesToSend.Length);
            ms.Write(dataLen, 0, 4);
            ms.Write(bytesToSend, 0, bytesToSend.Length);
            ms.Flush();

            FileStream file = new FileStream(file_path, FileMode.Open, FileAccess.Write);
            byte[] bytes = new byte[ms.Length];
            ms.Read(bytes, 0, (int)ms.Length);
            file.Write(bytes, 0, bytes.Length);

            ms.Close();
        }
    }

}
