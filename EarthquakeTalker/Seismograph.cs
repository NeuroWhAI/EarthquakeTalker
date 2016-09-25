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

        protected Queue<int> m_sampleCountList = new Queue<int>();
        protected readonly object m_lockSampleCount = new object();

        protected double? m_normalScale = null;

        //###########################################################################################################

        protected void StartProcess()
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

        protected void StopProcess()
        {
            m_proc.Close();
            m_proc.WaitForExit(3000);
            m_proc.Kill();
            m_proc.Dispose();
            m_proc = null;

            m_buffer.Clear();
            m_leftSample = 0;

            m_samples.Clear();

            m_sampleCountList.Clear();

            m_normalScale = null;
        }

        //###########################################################################################################

        protected override void BeforeStart(Talker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(1.0);


            StartProcess();
        }

        protected override void AfterStop(Talker talker)
        {
            StopProcess();
        }

        protected override Message OnWork()
        {
            try
            {
                int sampleCount = 0;

                lock (m_lockSampleCount)
                {
                    if (m_sampleCountList.Count > 0)
                    {
                        sampleCount = m_sampleCountList.Peek();
                    }
                }


                if (sampleCount > 0)
                {
                    bool runAvg = false;

                    lock (m_lockSamples)
                    {
                        if (m_samples.Count >= sampleCount)
                        {
                            runAvg = true;
                        }
                    }


                    if (runAvg)
                    {
                        lock (m_lockSampleCount)
                        {
                            m_sampleCountList.Dequeue();
                        }


                        int maxData = 0;

                        double sum = 0.0;
                        int count = 0;

                        lock (m_lockSamples)
                        {
                            for (int i = 0; i < sampleCount; ++i)
                            {
                                int data = Math.Abs(m_samples[i]);

                                if (data == 0)
                                    continue;

                                if (data > maxData)
                                    maxData = data;

                                sum += 1.0 / data;
                                ++count;
                            }


                            m_samples.RemoveRange(0, sampleCount);
                        }


                        if (count > 0)
                        {
                            double avgM = Math.Log10(1.0 / sum * count);

                            if (m_normalScale == null)
                            {
                                m_normalScale = avgM;

                                m_logger.PushLog(Name + " : " + m_normalScale);
                            }
                            else
                            {
                                m_normalScale += (avgM - m_normalScale) / 16.0;

                                double gap = avgM - (double)m_normalScale;
                                if (Math.Abs(gap) > 2.0 || maxData > 200000)
                                {
                                    double scale = Math.Log10(maxData) - 1.25;

                                    var msg = new Message()
                                    {
                                        Level = Message.Priority.Critical,
                                        Sender = Selector + " " + Stream + " Station",
                                        Text = $@"{Name}에서 최대 {maxData}의 진폭 감지됨.
지역 예상규모 : {scale}±2
오보일 수 있으니 침착하시고 소식에 귀 기울여 주시기 바랍니다.
{EarthquakeKnowHow.GetKnowHow(scale)}",
                                    };

                                    m_logger.PushLog(msg);

                                    return msg;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);

                StopProcess();
                System.Threading.Thread.Sleep(3000);
                StartProcess();
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

                    lock (m_lockSampleCount)
                    {
                        m_sampleCountList.Enqueue(m_leftSample);
                    }

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
