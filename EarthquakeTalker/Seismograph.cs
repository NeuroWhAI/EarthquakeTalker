using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EarthquakeTalker
{
    public class Seismograph : Worker
    {
        public Seismograph(string slinktoolPath, string selector, string stream, string name = "")
        {
            SLinkToolPath = slinktoolPath;
            Selector = selector;
            Stream = stream;
            Name = name;
        }

        //###########################################################################################################

        public string SLinkToolPath
        { get; protected set; } = string.Empty;

        public string Selector
        { get; protected set; } = string.Empty;

        public string Stream
        { get; protected set; } = string.Empty;

        public string Name
        { get; set; }

        protected Process m_proc = null;

        protected StringBuilder m_buffer = new StringBuilder();
        protected int m_leftSample = 0;

        protected List<int> m_samples = new List<int>();
        protected readonly object m_lockSamples = new object();

        //###########################################################################################################

        protected override void BeforeStart(Talker talker)
        {
            m_proc = new Process();
            m_proc.StartInfo = new ProcessStartInfo()
            {
                Arguments = "-p -u -s " + Selector + " -S " + Stream + " rtserve.iris.washington.edu:18000",
                CreateNoWindow = true,
                FileName = SLinkToolPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };


            m_proc.OutputDataReceived += Proc_OutputDataReceived;


            m_proc.Start();

            m_proc.BeginOutputReadLine();
        }

        protected override void AfterStop(Talker talker)
        {
            m_proc.Close();
            m_proc.WaitForExit(3000);
            m_proc.Kill();
            m_proc.Dispose();
            m_proc = null;

            m_buffer.Clear();
            m_leftSample = 0;

            m_samples.Clear();
        }

        protected override Message OnWork()
        {
            try
            {
                lock (m_lockSamples)
                {
                    double totalData = 0;

                    if (m_samples.Count > 512)
                    {
                        foreach (var data in m_samples)
                        {
                            totalData += Math.Abs(data);
                        }

                        m_logger.PushLog(Name + " Avg: " + totalData / m_samples.Count);

                        m_samples.Clear();
                    }


                    // TODO: 경보
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);
            }
            finally
            {
                System.Threading.Thread.Sleep(1000);
            }


            return null;
        }

        //###########################################################################################################

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            m_buffer.Append(e.Data);
            string buf = m_buffer.ToString();


            if (m_leftSample <= 0)
            {
                Regex rgx = new Regex(@"([^,]+),\s?(\d+)\s?samples,\s?(\d+)\s?Hz,\s?([^\s]+)\s?\(.+\)");
                var m = rgx.Match(buf);
                if (m.Success)
                {
                    //Console.WriteLine("Location: " + m.Groups[1]);
                    //Console.WriteLine("Samples: " + m.Groups[2]);
                    //Console.WriteLine("Hz: " + m.Groups[3]);
                    //Console.WriteLine("Time: " + m.Groups[4]);

                    Console.Write("~");

                    m_leftSample = int.Parse(m.Groups[2].ToString());

                    m_buffer = new StringBuilder(buf.Substring(m.Index + m.Length));
                }
            }
            else
            {
                int index = -1, length = 0;

                Regex rgx = new Regex(@"(-?\d+)\s+");
                var m = rgx.Match(buf);
                while (m.Success)
                {
                    //Console.Write(m.Groups[1].ToString() + '\t');

                    lock (m_lockSamples)
                    {
                        m_samples.Add(int.Parse(m.Groups[1].ToString().Trim()));
                    }


                    index = m.Index;
                    length = m.Length;

                    m_leftSample--;

                    m = m.NextMatch();
                }

                if (index >= 0)
                {
                    m_buffer = new StringBuilder(buf.Substring(index + length));
                }
            }
        }
    }
}
