using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CoolSign.API;
using CoolSign.API.Version2;
using CoolSign.API.Version2.DataAccess;

namespace FileTransferStatusWidget
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string HOST = "JoshVM-2003";
        private const int PORT = 80;
        private const string USERNAME = "admin";
        private const string PASSWORD = "password";

        private ObservableCollection<NodeModel> m_nodes = new ObservableCollection<NodeModel>();
        private ObservableCollection<FileTransferModel> m_transfers = new ObservableCollection<FileTransferModel>();
        private IServerSession m_session;
        private IFileTransferStatusView m_view;

        public MainWindow()
        {
            InitializeComponent();

            m_nodeComboBox.ItemsSource = m_nodes;
            m_nodeComboBox.SelectionChanged += m_nodeComboBox_SelectionChanged;

            m_transferView.ItemsSource = m_transfers;

            m_session = CSAPI.Create().CreateServerSession(HOST, PORT);
            using (var result = m_session.Authenticate(USERNAME, PASSWORD))
            {
                if (!result.IsSuccess)
                {
                    m_errors.Text = "Login failed: " + result.ToString();
                    m_session.Dispose();
                    m_session = null;
                    return;
                }
            }

            using (var result = m_session.DataAccess.Brokers.Node.Read(m_session.ModelFactory.CreateAllSelector(), null))
            {
                if (result.IsSuccess)
                {
                    foreach (INode node in result.Value.Items)
                    {
                        NodeModel nm = new NodeModel()
                        {
                            Name = node.Name,
                            Id = node.Id.ToString(),
                        };
                        m_nodes.Add(nm);
                    }
                }
                else
                {
                    m_errors.Text = result.ToString();
                    return;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            m_session.Dispose();
            m_session = null;

            base.OnClosed(e);
        }

        private void m_nodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (null != m_view)
            {
                m_view.StatusChanged -= m_view_StatusChanged;
                m_view.Dispose();
                m_view = null;
            }

            NodeModel node = m_nodeComboBox.SelectedItem as NodeModel;

            if (null != node)
            {
                m_errors.Text = "";
                using (var result = m_session.OpenFileTransferStatusView(new Oid(node.Id)))
                {
                    if (result.IsSuccess)
                    {
                        lock (result.Value)
                        {
                            m_view = result.Value;
                            SyncView();
                            m_view.StatusChanged += m_view_StatusChanged;
                        }
                    }
                    else
                    {
                        m_errors.Text = result.ToString();
                    }
                }
            }
        }

        private void m_view_StatusChanged()
        {
            SyncView();
        }

        private void SyncView()
        {
            if (CheckAccess())
            {
                if (null != m_view)
                {
                    lock (m_view.Sync)
                    {
                        foreach (var fts in m_view.FileStatuses)
                        {
                            FileTransferModel ftm = m_transfers.FirstOrDefault((candidate) => 0 == string.Compare(fts.Name, candidate.FileName, StringComparison.InvariantCultureIgnoreCase));
                            if (null == ftm)
                            {
                                ftm = new FileTransferModel()
                                {
                                    FileName = fts.Name
                                };
                                m_transfers.Add(ftm);
                            }
                            ftm.Reason = fts.StatusType.ToString();
                            ftm.Size = fts.Size;
                            foreach (var fths in fts.HopStatuses)
                            {
                                NodeModel parentNode = m_nodes.FirstOrDefault((candidate) => 0 == string.Compare(candidate.Id, fths.ParentNodeId.ToString(), StringComparison.InvariantCultureIgnoreCase));
                                NodeModel childNode = m_nodes.FirstOrDefault((candidate) => 0 == string.Compare(candidate.Id, fths.ChildNodeId.ToString(), StringComparison.InvariantCultureIgnoreCase));
                                FileTransferHopModel fthm = ftm.Hops.FirstOrDefault((candidate) => candidate.ParentNode == parentNode && candidate.ChildNode == childNode);
                                if (null == fthm)
                                {
                                    fthm = new FileTransferHopModel()
                                    {
                                        ParentNode = parentNode,
                                        ChildNode = childNode,
                                        Transfer = ftm,
                                    };
                                    ftm.Hops.Add(fthm);
                                }
                                fthm.Progress = fths.Progress;
                                fthm.Rate = fths.Rate;
                            }
                        }
                    }
                }
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(m_view_StatusChanged), null);
            }
        }
    }
}
