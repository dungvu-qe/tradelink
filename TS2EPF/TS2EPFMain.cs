﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using TradeLink.Common;
using TradeLink.API;
using System.ComponentModel;

namespace TS2EPF
{
    public partial class TS2EPFMain : Form
    {
        const string PROGRAM = "PairedPredict";
        Auth _auth = new Auth(@"http://franta.com/auth/?a=" + PROGRAM);
        bool _valid = false;
        BackgroundWorker bw = new BackgroundWorker();
        public TS2EPFMain()
        {
            InitializeComponent();
            _valid = _auth.isAuthorized(Auth.GetCPUId());
            bw.WorkerReportsProgress = true;
            bw.DoWork += new DoWorkEventHandler(bw_DoWork);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
        }




        Dictionary<string, string> _filesyms = new Dictionary<string, string>();
        string _path = string.Empty;
        private void _inputbut_Click(object sender, EventArgs e)
        {
            if (!_valid) { debug("program not authorized to run"); return; }
            // make sure we only convert one group at a time
            if (bw.IsBusy) { debug("wait until conversion completes..."); return; }
            OpenFileDialog of = new OpenFileDialog();
            // allow selection of multiple inputs
            of.Multiselect = true;
            // keep track of bytes so we can approximate progress
            long bytes = 0;
            if (of.ShowDialog() == DialogResult.OK)
            {
                bool g = true;
                foreach (string file in of.FileNames)
                {
                    _path = Path.GetDirectoryName(file);
                    // get size of current file and append to total size
                    FileInfo fi = new FileInfo(file);
                    bytes += fi.Length;
                    // ask user to provide symbol name 
                    string sym = Microsoft.VisualBasic.Interaction.InputBox("What symbol is represented by this file?"+Environment.NewLine+Path.GetFileNameWithoutExtension(file), "Identify symbol", Path.GetFileNameWithoutExtension(file), 0, 0);
                    // keep track of relationship
                    _filesyms.Add(file, sym);
                }
                // estimate total ticks
                _approxtotal = (int)((double)bytes / 51);
                // reset progress bar
                progress(0);
                // start background thread to convert
                bw.RunWorkerAsync(of.FileNames);
                debug("started convesion");

            }
        }

        void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] filenames = (string[])e.Argument;
            bool g = e.Result != null ? (bool)e.Result : true;
            foreach (string file in filenames)
            {
                debug("input file: " + Path.GetFileNameWithoutExtension(file));
                // convert file
                bool fg = convert(file, _filesyms[file], (int)_defaultsize.Value);
                // report progress
                if (!fg) debug("error converting file: " + file);
                g &= fg;
            }
            e.Result = g;

        }

        void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // status back to user
            bool g = (bool)e.Result;
            debug("processed ticks: " + _ticksprocessed.ToString("N0"));
            if (g) debug("converted files successfully.");
            else debug("errors converting files.");
            // reset progress bar
            progress(0);
            // reset ticks processed
            _ticksprocessed = 0;
        }

        int _ticksprocessed = 0;
        int _approxtotal = 0;
        bool convert(string filename,string sym,int tradesize)
        {
            bool g = true;
            // get output filename
            string convertname = string.Empty;
            // setup writing to output
            StreamWriter outfile = null;
            // setup input file
            StreamReader infile = null;
            try
            {
                // open input file
                infile = new StreamReader(filename);
                // read in and ignore header of input file
                infile.ReadLine();
            }
            catch (Exception ex) { debug("error reading input header:" + ex.Message); g = false; }
            // setup previous tick
            TickImpl pk = new TickImpl();
            do
            {
                // get next tick from the file
                TickImpl k = parseline(infile.ReadLine(), sym, tradesize);
                // if dates don't match, we need to write new output file
                if (k.date != pk.date)
                {
                    // if the outfile was open previously, close it
                    if (outfile != null) outfile.Close();
                    try
                    {
                        // get file name
                        string fn = _path+"//"+sym + k.date + ".EPF";
                        // setup new file
                        outfile = new StreamWriter(fn, false);
                        // write file header
                        outfile.Write(eSigTick.EPFheader(sym, k.date));
                        // report progress
                        progress((double)_ticksprocessed / _approxtotal);
                    }
                    catch (Exception ex) { debug(ex.Message); g = false; }
                }
                try
                {
                    // write the tick
                    outfile.WriteLine(eSigTick.ToEPF(k));
                }
                catch (Exception ex) { debug("error writing output tick: " + ex.Message); g = false; }
                // save this tick as previous tick
                pk = k;
                // count the tick as processed
                _ticksprocessed++;
            }
            // keep going until input file is exhausted
            while (!infile.EndOfStream);
            // close output file
            outfile.Close();
            // return status
            return g;
        }

        // fields of tradestation files
        const int DATE = 0;
        const int TIME = 1;
        const int OPEN = 2;
        const int HIGH = 3;
        const int LOW = 4;
        const int CLOSE = 5;
        const int UP = 6;
        const int DOWN = 7;
        // here is where a line is converted
        TickImpl parseline(string line, string sym, int defaultsize)
        {
            // split line
            string[] r = line.Split(',');
            // create tick for this symbol
            TickImpl k = new TickImpl(sym);
            // setup temp vars
            int iv = 0;
            decimal dv = 0;
            DateTime date;
            // parse date
            if (DateTime.TryParse(r[DATE], out date))
                k.date = Util.ToTLDate(date);
            // parse time
            if (int.TryParse(r[TIME], out iv))
                k.time = iv * 100;
            // parse close as trade price
            if (decimal.TryParse(r[CLOSE], out dv))
            {
                k.trade = dv;
                k.size = defaultsize;
            }
            // return tick
            return k;
        }
        delegate void pdouble(double p);
        void progress(double percent)
        {
            int p = (int)(percent * 100);
            if (p > 100) p = 100;
            if (p < 0) p = 0;
            // if being called from a background thread, 
            // invoke UI thread to update screen
            if (statusStrip1.InvokeRequired)
                statusStrip1.Invoke(new pdouble(progress), new object[] { percent });
            else
            {
                _progress.Value = p;
                _progress.Invalidate();
            }
        }

        void debug(string msg)
        {
            if (InvokeRequired)
                Invoke(new DebugDelegate(debug), new object[] { msg });
            else
            {
                msg = DateTime.Now.ToShortTimeString() + " " + msg;
                _msg.Items.Add(msg);
                _msg.SelectedIndex = _msg.Items.Count - 1;
            }

        }
    }
}
