using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CoolSign.API;
using CoolSign.API.Version1;

namespace DataTableEditor
{
    /// <summary>
    /// Interaction logic for LoginDialog.xaml
    /// </summary>
    public partial class LoginDialog : Window
    {
        private CSAPI m_api;
        private string m_host;
        private int m_port;
        private IServerSession m_session;

        public LoginDialog(CSAPI api, string host, int port)
        {
            m_api = api;
            m_host = host;
            m_port = port;

            InitializeComponent();

            m_loginButton.Click += m_loginButton_Click;
            m_cancelButton.Click += m_cancelButton_Click;

            m_serverUrl.Text = string.Format("{0}:{1}", host, port);

            using (var conn = m_api.CreateNodeConnection(m_host, m_port))
            {
                using (var result = conn.ExecuteCommand("CoolSign.System.GetInfo", null))
                {
                    if (result.IsSuccess)
                    {
                        m_nc.Text = result.ReadBodyXml().Element("NetworkName").Value;
                    }
                    else
                    {
                        m_error.Text = "Failed to connect to server!";
                    }
                }
            }
        }

        public IServerSession Session { get { return m_session; } }

        private void m_loginButton_Click(object sender, RoutedEventArgs e)
        {
            m_session = m_api.CreateServerSession(m_host, m_port);

            bool isAuthenticated = false;
            try
            {
                using (var result = m_session.Authenticate(m_username.Text, m_password.Password))
                {
                    if (result.IsSuccess)
                    {
                        DialogResult = true;
                        Close();
                        isAuthenticated = true;
                    }
                    else
                    {
                        if (result.Result == AuthenticateResultType.AuthFailed)
                        {
                            m_error.Text = "Invalid username/password";
                        }
                        else if (result.Result == AuthenticateResultType.ServerRefused)
                        {
                            m_error.Text = "Server rejected authentication, could be starting up";
                        }
                        else
                        {
                            m_error.Text = "Failed login: " + result.ToString();
                        }
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

        private void m_cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
