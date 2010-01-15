﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using TradeLink.Common;
using SterlingLib;
using TradeLink.API;
using TradeLink.AppKit;

namespace SterServer
{
    public partial class SterMain : Form
    {
        // basic structures needed for operation
        ServerSterling tl = new ServerSterling();
        public const string PROGRAM = "SterServer ";
        DebugControl _dc = new DebugControl(true);
        public SterMain()
        {
            InitializeComponent();
            _dc.Parent = this;
            _dc.Dock = DockStyle.Fill;
            ContextMenu = new ContextMenu();
            ContextMenu.MenuItems.Add("report", new EventHandler(report));
            tl.SendDebug += new DebugDelegate(tl_SendDebug);
            tl.Start();
            FormClosing += new FormClosingEventHandler(SterMain_FormClosing);
        }

        void tl_SendDebug(string msg)
        {
            debug(msg);
        }

        void report(object sender, EventArgs e)
        {
            CrashReport.Report(PROGRAM, string.Empty, string.Empty, _msgs.ToString(), null, new AssemblaTicketWindow.LoginSucceedDel(success), false);
        }

        StringBuilder _msgs = new StringBuilder();
        void success(string u, string p)
        {
            _msgs = new StringBuilder();
        }

        void debug(string msg)
        {
            _dc.GotDebug(msg);
        }



        void SterMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                tl.Stop();
            }
            catch (Exception)
            {
                // incase stering was already closed 
            }
        }






    }
}
