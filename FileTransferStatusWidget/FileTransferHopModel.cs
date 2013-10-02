using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace FileTransferStatusWidget
{
    public class FileTransferHopModel : BaseModel
    {
        private FileTransferModel m_transfer;
        private NodeModel m_parent;
        private NodeModel m_child;
        private long m_progress;
        private int m_rate;

        public FileTransferModel Transfer
        {
            get { return m_transfer; }
            set
            {
                m_transfer = value;
                OnPropertyChanged("Transfer");
            }
        }

        public NodeModel ParentNode
        {
            get { return m_parent; }
            set
            {
                m_parent = value;
                OnPropertyChanged("ParentNode");
            }
        }

        public NodeModel ChildNode
        {
            get { return m_child; }
            set
            {
                m_child = value;
                OnPropertyChanged("ChildNode");
            }
        }

        public long Progress
        {
            get { return m_progress; }
            set
            {
                m_progress = value;
                OnPropertyChanged("Progress");
            }
        }

        public int Rate
        {
            get { return m_rate; }
            set
            {
                m_rate = value;
                OnPropertyChanged("Rate");
            }
        }
    }
}
