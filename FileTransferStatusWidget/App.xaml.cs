using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using CoolSign.API;

namespace FileTransferStatusWidget
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Logger.SetLogSink(HandleLogEntry);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Logger.SetLogSink(null);
        }

        private static void HandleLogEntry(Logger.LogEntry entry)
        {
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(entry.ToString());
            }
        }
    }
}
