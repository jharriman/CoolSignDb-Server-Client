using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using ConfigClasses;

namespace ServerBackendTester
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                WatchedSets sets = new WatchedSets((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + Properties.Settings.Default.dbfilepath));
                TcpClient tcpclnt = new TcpClient();
                Console.WriteLine("Connecting.....");

                tcpclnt.Connect("128.135.167.97", 8001);
                // use the ipaddress as in the server program

                Console.WriteLine("Connected");
                /* Console.Write("Enter the string to be transmitted : "); */

                String str = Console.ReadLine();
                Stream stm = tcpclnt.GetStream();
                BinaryFormatter binForm = new BinaryFormatter();
                foreach (w_set set in sets.all_set)
                {
                    Console.Write(set.OID_STR + "\n\t" + set.TABLE_NAME + "\n\t" + set.TABLE_DB_PATH + "\n");
                }
                try
                {
                    
                    binForm.Serialize(stm, sets.all_set);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                /* ASCIIEncoding asen = new ASCIIEncoding();
                byte[] ba = asen.GetBytes(str);*/
                Console.WriteLine("Transmitting.....");

                /*stm.Write(ba, 0, ba.Length);

                byte[] bb = new byte[100];
                int k = stm.Read(bb, 0, 100);

                for (int i = 0; i < k; i++)
                    Console.Write(Convert.ToChar(bb[i]));
*/
                tcpclnt.Close();
            }

            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
            }
            //Heavy use of example from: http://www.codeproject.com/Articles/1415/Introduction-to-TCP-client-server-in-C
        }
    }
}
