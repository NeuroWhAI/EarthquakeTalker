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
        public Seismograph(string slinktoolPath, string selector, string stream, double gain, string name = "")
        {
            SLinkToolPath = slinktoolPath;
            Selector = selector;
            Stream = stream;
            Gain = gain;
            Name = name;
        }

        //###########################################################################################################

        public string SLinkToolPath
        { get; protected set; } = string.Empty;

        public string Selector
        { get; protected set; } = string.Empty;

        public string Stream
        { get; protected set; } = string.Empty;

        /// <summary>
        /// 진폭을 이 값으로 나누면 지반 속도or가속도가 나온다.
        /// </summary>
        public double Gain
        { get; set; }

        public double DangerPga
        { get; set; } = 0.0028;

        public string Name
        { get; set; }

        protected Process m_proc = null;

        protected StringBuilder m_buffer = new StringBuilder();
        protected int m_leftSample = 0;

        protected List<double> m_samples = new List<double>();
        protected readonly object m_lockSamples = new object();

        protected Queue<int> m_sampleCountList = new Queue<int>();
        protected readonly object m_lockSampleCount = new object();

        protected double? m_prevRawData = null;
        protected double m_prevProcData = 0;

        public int Index
        { get; set; } = -1;
        public delegate void SeismographDataReceivedEventHandler(int index, List<double> waveform);
        public event SeismographDataReceivedEventHandler WhenDataReceived = null;

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

            m_prevRawData = null;
            m_prevProcData = 0;
        }

        //###########################################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(1.0);


            StartProcess();
        }

        protected override void AfterStop(MultipleTalker talker)
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
                    bool runCheck = false;

                    lock (m_lockSamples)
                    {
                        // 현재까지 얻은 샘플 개수가 충분하면
                        if (m_samples.Count >= sampleCount)
                        {
                            runCheck = true;
                        }
                    }


                    if (runCheck)
                    {
                        lock (m_lockSampleCount)
                        {
                            m_sampleCountList.Dequeue();
                        }


                        /// Max Raw Data
                        double maxData = 0;

                        lock (m_lockSamples)
                        {
                            // 최댓값을 찾음.
                            foreach (double data in m_samples)
                            {
                                double absData = Math.Abs(data);
                                if (absData > maxData)
                                    maxData = absData;
                            }


                            // 비동기로 데이터 수신 이벤트 발생.
                            var subSamples = Enumerable.Take(m_samples, sampleCount).ToList();
                            Task.Factory.StartNew(delegate ()
                            {
                                WhenDataReceived(this.Index, subSamples);
                            });


                            // 처리한 파형 제거.
                            m_samples.RemoveRange(0, sampleCount);
                        }
                        

                        /// Max PGA
                        double pga = maxData / Gain;

                        if (pga > DangerPga / 2)
                        {
                            m_logger.PushLog(Name + " PGA : " + pga);


                            // PGA가 위험 수치를 넘어서면
                            if (pga > DangerPga)
                            {
                                double mScale = 2.0 * Math.Log10(pga * 980.665);


                                var msg = new Message()
                                {
                                    Level = Message.Priority.Critical,
                                    Sender = Selector + " " + Stream + " Station",
                                    Text = $@"{Name} 지진계에서 진동 감지됨.
수치 : {(pga / DangerPga * 100.0).ToString("F2")}%
진원지 : 알 수 없음.
오류일 수 있으니 침착하시고 소식에 귀 기울여 주시기 바랍니다.

{EarthquakeKnowHow.GetKnowHow(mScale)}",
                                };


                                m_logger.PushLog(msg);


                                return msg;
                            }
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);

                if (m_onRunning)
                {
                    StopProcess();
                    System.Threading.Thread.Sleep(3000);
                    StartProcess();
                }
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

                    Console.Write('~');


                    // 얻을 샘플 개수인데 첫번째 데이터는 버리므로 1을 뺌.
                    m_leftSample = int.Parse(m.Groups[2].ToString()) - 1;

                    lock (m_lockSampleCount)
                    {
                        m_sampleCountList.Enqueue(m_leftSample);
                    }

                    m_buffer = new StringBuilder(buf.Substring(m.Index + m.Length));


                    m_prevRawData = null;
                    m_prevProcData = 0;
                }
            }
            else
            {
                int index = -1, length = 0;

                Regex rgx = new Regex(@"(-?\d+)\s+");
                var m = rgx.Match(buf);
                while (m.Success)
                {
                    lock (m_lockSamples)
                    {
                        int data = 0;
                        if (int.TryParse(m.Groups[1].ToString().Trim(), out data))
                        {
                            // 첫번째 데이터는 샘플에 넣지 않는다.
                            // 청크 단위로 파형을 얻고 있기에 이전 파형과 시간적으로 맞물리지 않는 경우가 있어
                            // 고역통과필터에 문제가 생긴다.

                            if (m_prevRawData != null)
                            {
                                const double weight = 0.16;
                                double procData = ((weight + 1) / 2) * (data - m_prevRawData.Value) + weight * m_prevProcData;

                                m_samples.Add(procData);

                                m_prevProcData = procData;
                            }

                            m_prevRawData = data;
                        }
                        else
                        {
                            m_leftSample = -1;
                            break;
                        }
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
