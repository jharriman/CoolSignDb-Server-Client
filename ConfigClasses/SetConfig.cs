using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace ConfigClasses
{
    public class SetConfig
    {
        private string dbPath;
        private char[] DELIMARR = {';'};
        private string DELIM = ";";
        public SetConfig(string db_path, bool init)
        {
            dbPath = db_path;
            /* Initialize public lists */
            cols = new List<colConf>();
            ns_list = new List<nsConf>();

            /* Read db into column list */
            if (init == false)
            {
                try
                {
                    FileStream fs = File.Open(dbPath, FileMode.Open, FileAccess.Read, FileShare.None); //Add support for this configuration file being edited at the same time.
                    StreamReader sr = new StreamReader(fs);
                    while (!sr.EndOfStream)
                    {
                        var parts = sr.ReadLine().Split(DELIMARR, StringSplitOptions.None);
                        if (parts[0] == ".")
                        {
                            addToList(parts[1], parts[2], parts[3], parts[4], parts[5].Equals("1"), parts[6]); //ERROR CHECKING!
                        }
                        else if (parts[0] == "..") /* Namespace Info */
                        {
                            addtoNS_List(parts[1], parts[2]);
                        }
                        else if (parts[0] == "...")
                        {
                            break;
                        }
                        else /* Initial config information */
                        {
                            oidForWrite = parts[0];
                            sourceUrl = parts[1];
                            allOneRecord = parts[2].Equals("1");
                            if (allOneRecord) { itemOfRec = parts[3]; }
                            if (!allOneRecord) { numRows = Convert.ToInt32(parts[3]); }

                            /* Remember to add error checking */
                        }
                        //Console.ReadLine();
                    }
                    sr.Close();
                }
                catch (IOException)
                {
                    Thread.Sleep(5000);
                }
            }
            else /* Setup init data structure. NOTE: We need this case for WatcherSetupDialog so that it can add new things to it and then send it along the network */
            {

            }
        }
        public bool allOneRecord;
        public string itemOfRec; //If set to null then allOneRecord must be set to false
        public string oidForWrite;
        public string sourceUrl;
        public int numRows;
        public List<colConf> cols { get; set; }
        public void addToList(string name_of_col, string description, string attrib, string source, bool trigger, string trigger_id)
        {
            colConf to_add = new colConf();
            to_add.name_of_col = name_of_col;
            to_add.description = description;
            to_add.attrib = attrib;
            if (!allOneRecord) { to_add.source = source; }
            if (trigger) { to_add.firesTrigger = true; to_add.triggerID = trigger_id; }
            else { to_add.firesTrigger = false; }
            cols.Add(to_add);
        }
        public List<nsConf> ns_list { get; set; }
        public void addtoNS_List(string ns, string source)
        {
            nsConf new_ns = new nsConf();
            new_ns.ns = ns;
            new_ns.ns_source = source;
            ns_list.Add(new_ns);
        }
        public void printConfig()
        {
            Console.WriteLine(oidForWrite + DELIM + sourceUrl + DELIM + allOneRecord.ToString() + DELIM + numRows.ToString());
            foreach (colConf col in cols)
            {
                Console.WriteLine("\t" + col.name_of_col + DELIM + col.description + DELIM + col.attrib + DELIM + col.source + DELIM
                    + col.firesTrigger.ToString() + DELIM + col.triggerID);
            }
            foreach (nsConf ns in ns_list)
            {
                Console.WriteLine("\t\t" + ns.ns + DELIM + ns.ns_source);
            }
        }
    }
    [Serializable()]
    public class colConf
    {
        /* Per row settings */
        public string name_of_col;
        public string description; // If all one record is false then description is the full path
        public string attrib; // If null then no attrib
        public string source; // If all one record is false then source points to the document of each element, else null
        public bool firesTrigger;
        public string triggerID; // If firesTrigger is true then trigger ID points to the trigger fired when there is data, else null
    }
    [Serializable()]
    public class nsConf
    {
        public string ns;
        public string ns_source;
    }
}
