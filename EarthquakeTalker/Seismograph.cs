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

        public double DangerStaLta
        { get; set; } = 9.5;

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
        protected double? m_prevLongAvg = null;

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

            m_prevLongAvg = null;
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

                        /// Max STA/LTA
                        double maxStaLta = 0;

                        lock (m_lockSamples)
                        {
                            /// LTA
                            double longAvg = 0;

                            // 최댓값을 찾음과 동시에 LTA 계산.
                            foreach (double data in m_samples)
                            {
                                double absData = Math.Abs(data);
                                if (absData > maxData)
                                    maxData = absData;

                                longAvg += (data / Gain) * (data / Gain);
                            }

                            if (longAvg < 0.000001)
                            {
                                longAvg = 0.000001;
                            }


                            // Max STA/LTA 계산.
                            if (m_prevLongAvg != null)
                            {
                                int shortTerm = sampleCount / 10;

                                for (int partEnd = shortTerm; partEnd <= sampleCount; partEnd += shortTerm)
                                {
                                    /// STA
                                    double shortAvg = 0;

                                    for (int part = partEnd - shortTerm; part < partEnd; ++part)
                                    {
                                        shortAvg += (m_samples[part] / Gain) * (m_samples[part] / Gain);
                                    }


                                    /// STA/LTA
                                    double staLta = shortAvg / m_prevLongAvg.Value;

                                    if (staLta > maxStaLta)
                                    {
                                        maxStaLta = staLta;
                                    }
                                }
                            }

                            m_prevLongAvg = longAvg;


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

                        if (maxStaLta > DangerStaLta / 2 || pga > DangerPga / 2)
                        {
                            m_logger.PushLog(Name + "\n PGA: " + pga + "\n STA/LTA: " + maxStaLta);


                            // STA/LTA나 PGA가 위험 수치를 넘어서면
                            if (maxStaLta > DangerStaLta || pga > DangerPga)
                            {
                                double mScale = 2.0 * Math.Log10(pga * 980.665);


                                var msgLevel = Message.Priority.High;

                                if (maxStaLta > DangerStaLta)
                                    msgLevel = Message.Priority.Critical;


                                var msg = new Message()
                                {
                                    Level = msgLevel,
                                    Sender = Selector + " " + Stream + " Station",
                                    Text = $@"{Name}에서 진동 감지됨.
수치 : PGA-{(pga / DangerPga * 100.0).ToString("F2")}%, STA/LTA-{(maxStaLta / DangerStaLta * 100.0).ToString("F2")}%
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


                    m_leftSample = int.Parse(m.Groups[2].ToString());

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
                            if (m_prevRawData == null)
                            {
                                m_samples.Add(data);
                            }
                            else
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
                            if (m_samples.Count > 0)
                            {
                                m_samples.Add(m_samples.Last());
                            }
                            else
                            {
                                m_samples.Add(0);
                            }
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
