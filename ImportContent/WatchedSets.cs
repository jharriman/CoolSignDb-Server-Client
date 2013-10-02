using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;


namespace DataTableEditor
{
    class WatchedSets
    {
        private const char DELIM = ';';
        public WatchedSets(string db_path)
        {
            /* Initialize database list */
            all_set = new List<w_set>();

            /* Read in database */
            using (FileStream fs = File.Open(db_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                StreamReader sr = new StreamReader(fs);
                while (!sr.EndOfStream)
                {
                    var parts = sr.ReadLine().Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    string oid, tb_name, tb_db;
                    oid = String.IsNullOrEmpty(parts[0]) ? "NO DATA" : parts[0];
                    tb_name = String.IsNullOrEmpty(parts[1]) ? "NO DATA" : parts[1];
                    tb_db = String.IsNullOrEmpty(parts[2]) ? "NO DATA" : parts[2];
                    addToWatchedSets(oid, tb_name, tb_db);
                }
                fs.Close();
            }
        }
        public void addToWatchedSets(string oid_str, string table_name, string table_db_path)
        {
            w_set add_set = new w_set();
            add_set.OID_STR = oid_str;
            add_set.TABLE_NAME = table_name;
            add_set.TABLE_DB_PATH = table_db_path;
            all_set.Add(add_set);
        }
        public bool isInAllSet(string oid_str)
        {
            bool inSet = false;
            foreach (w_set test_set in all_set)
            {
                if (test_set.OID_STR == oid_str)
                {
                    inSet = true;
                    break;
                }
            }
            return inSet;
        }
        public void saveToDbFile(string db_path)
        {
            /* Open file using FileMode.Create in order to overwrite existing database file */
            using (FileStream fs = File.Open(db_path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                StreamWriter sw = new StreamWriter(fs);
                foreach(w_set write_set in all_set)
                {
                    sw.WriteLine(write_set.OID_STR + DELIM + write_set.TABLE_NAME + DELIM + write_set.TABLE_DB_PATH);
                    sw.Flush();
                }
                fs.Close();
            }
        }
        public List<w_set> all_set { get; set; }
    }
    class w_set
    {
        public string OID_STR { get; set; }
        public string TABLE_NAME { get; set; }
        public string TABLE_DB_PATH { get; set; }
    }
}
