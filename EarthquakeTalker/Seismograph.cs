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
        public Seismograph(string channel, string network, string station, double gain, string name = "")
        {
            Channel = channel;
            Network = network;
            Station = station;
            Gain = gain;
            Name = name;
        }

        //###########################################################################################################

        public string Channel
        { get; protected set; } = string.Empty;

        public string Network
        { get; protected set; } = string.Empty;

        public string Station
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

        private List<double> m_samples = new List<double>();
        private readonly object m_lockSamples = new object();

        private Queue<int> m_sampleCountList = new Queue<int>();
        private readonly object m_lockSampleCount = new object();

        private double? m_prevRawData = null;
        private double m_prevProcData = 0;

        public int Index
        { get; set; } = -1;
        public delegate void SeismographDataReceivedEventHandler(int index, List<double> waveform);
        public event SeismographDataReceivedEventHandler WhenDataReceived = null;

        //###########################################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(1.0);
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_samples.Clear();

            m_sampleCountList.Clear();

            m_prevRawData = null;
            m_prevProcData = 0;
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

                        /// 청크 샘플
                        List<double> subSamples = null;

                        lock (m_lockSamples)
                        {
                            // 파형 저장.
                            subSamples = Enumerable.Take(m_samples, sampleCount).ToList();


                            // 최댓값을 찾음.
                            foreach (double data in subSamples)
                            {
                                double absData = Math.Abs(data);
                                if (absData > maxData)
                                    maxData = absData;
                            }


                            // 처리한 파형 제거.
                            m_samples.RemoveRange(0, sampleCount);
                        }


                        // 비동기로 데이터 수신 이벤트 발생.
                        Task.Factory.StartNew(delegate ()
                        {
                            WhenDataReceived(this.Index, subSamples);
                        });


                        /// Max PGA
                        double pga = maxData / Gain;

                        if (pga > DangerPga / 2)
                        {
                            m_logger.PushLog(Name + " PGA : " + pga);


                            // PGA가 위험 수치를 넘어서면
                            if (pga > DangerPga)
                            {
                                int mmi = Earthquake.ConvertToMMI(pga);


                                var msg = new Message()
                                {
                                    Level = Message.Priority.Critical,
                                    Sender = Channel + " " + Network + "_" + Station + " Station",
                                    Text = $@"{Name} 지진계에서 진동 감지됨.
수치 : {(pga / DangerPga * 100.0).ToString("F2")}%
예상 진도(MMI) : {Earthquake.MMIToString(mmi)}
진원지 : 알 수 없음.
오류일 수 있으니 침착하시고 소식에 귀 기울여 주시기 바랍니다.

{Earthquake.GetKnowHowFromMMI(mmi)}",
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
            }


            return null;
        }

        //###########################################################################################################

        protected void ReserveChunk(string location, int sampleCount, int samplingRate, string time)
        {
            Console.Write('~');


            lock (m_lockSampleCount)
            {
                // 얻을 샘플 개수인데 첫번째 데이터는 버리므로 1을 뺌.
                m_sampleCountList.Enqueue(sampleCount - 1);
            }

            m_prevRawData = null;
            m_prevProcData = 0;
        }

        protected void AppendSample(int data)
        {
            // 첫번째 데이터는 샘플에 넣지 않는다.
            // 청크 단위로 파형을 얻고 있기에 이전 파형과 시간적으로 맞물리지 않는 경우가 있어
            // 고역통과필터에 문제가 생긴다.

            if (m_prevRawData != null)
            {
                const double weight = 0.16;
                double procData = ((weight + 1) / 2) * (data - m_prevRawData.Value) + weight * m_prevProcData;

                lock (m_lockSamples)
                {
                    m_samples.Add(procData);
                }

                m_prevProcData = procData;
            }

            m_prevRawData = data;
        }
    }
}
