using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FileTransferStatusWidget
{
    public class NodeModel : BaseModel
    {
        private string m_name;
        private string m_id;

        public string Name
        {
            get { return m_name; }
            set
            {
                m_name = value;
                OnPropertyChanged("Name");
            }
        }

        public string Id
        {
            get { return m_id; }
            set
            {
                m_id = value;
                OnPropertyChanged("Id");
            }
        }
    }
}
