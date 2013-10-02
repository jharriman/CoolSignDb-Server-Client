using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace FileTransferStatusWidget
{
    public class FileTransferModel : BaseModel
    {
        private string m_name;
        private string m_reason;
        private long m_size;
        private ObservableCollection<FileTransferHopModel> m_hops = new ObservableCollection<FileTransferHopModel>();

        public string FileName
        {
            get { return m_name; }
            set
            {
                m_name = value;
                OnPropertyChanged("FileName");
            }
        }

        public string Reason
        {
            get { return m_reason; }
            set
            {
                m_reason = value;
                OnPropertyChanged("Reason");
            }
        }

        public long Size
        {
            get { return m_size; }
            set
            {
                m_size = value;
                OnPropertyChanged("Size");
            }
        }

        public ObservableCollection<FileTransferHopModel> Hops
        {
            get { return m_hops; }
        }
    }
}
