using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public List<object> available_tables;
        public CoolSignSets()
        {
            createSession();
        }
        public void createSession()
        {
            m_session = m_api.CreateServerSession(m_host, m_port);

            bool isAuthenticated = false;
            try
            {
                using (var result = m_session.Authenticate("jharriman", "5l0wn!c"))
                {
                    if (result.IsSuccess)
                    {

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
        /* public void RefreshTableList()
        {
            using (var result = m_session.DataAccess.Brokers.DataTable.Read(m_session.ModelFactory.CreateAllSelector(), null))
            {
                if (result.IsSuccess)
                {
                    m_tableComboBox.ItemsSource = result.Value.Items;
                    m_errors.Text = "";
                }
                else
                {
                    m_errors.Text = "Failed to read DataTable list: " + result.ToString();
                }
            }
        }*/
    }

}
