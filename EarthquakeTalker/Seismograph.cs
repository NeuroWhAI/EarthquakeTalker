using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;

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

        public bool IsAccel
        { get; set; } = true;

        public string TypeText => IsAccel ? "가속도계" : "속도계";

        /// <summary>
        /// 진폭을 이 값으로 나누면 지반 속도or가속도가 나온다.
        /// </summary>
        public double Gain
        { get; set; }

        public double DangerValue
        { get; set; } = 0.0016;

        private double TriggerValue
        { get; set; } = 0.0016;

        private double NormalValue
        { get; set; } = 0.0016;

        private double MinNormalValue
        { get; set; } = 0.0016;
        private double MaxNormalValue
        { get; set; } = 0.0024;

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

        public double DangerWaveTime
        { get; set; } = 0.3;

        public double MinDangerWaveTime
        { get; set; } = 0.05;

        private int SamplingRate
        { get; set; } = 0;
        
        private List<Wave> m_waveBuffer = new List<Wave>();
        private int WindowSize
        { get; set; } = 10;

        private Queue<Message> m_msgQueue = new Queue<Message>();

        //###########################################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromMilliseconds(200.0);

            TriggerValue = DangerValue;

            if (IsAccel)
            {
                NormalValue = 0.03;
                MinNormalValue = 0.03;
                MaxNormalValue = 0.1;
            }
            else
            {
                NormalValue = 0.01;
                MinNormalValue = 0.01;
                MaxNormalValue = 0.038;
            }
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            TriggerValue = DangerValue;

            m_samples.Clear();

            m_sampleCountList.Clear();

            m_prevRawData = null;
            m_prevProcData = 0;
        }

        protected override Message OnWork(Action<Message> sender)
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
                        double maxData = -1;
                        int maxDataIndex = -1;

                        /// 청크 샘플
                        List<double> subSamples = null;

                        lock (m_lockSamples)
                        {
                            // 파형 저장.
                            subSamples = Enumerable.Take(m_samples, sampleCount).ToList();


                            // 최댓값을 찾음.
                            int index = 0;
                            foreach (double data in subSamples)
                            {
                                double absData = Math.Abs(data);
                                if (absData > maxData)
                                {
                                    maxData = absData;
                                    maxDataIndex = index;
                                }

                                ++index;
                            }


                            // 처리한 파형 제거.
                            m_samples.RemoveRange(0, sampleCount);
                        }


                        // 비동기로 데이터 수신 이벤트 발생.
                        if (WhenDataReceived != null)
                        {
                            Task.Factory.StartNew(delegate ()
                            {
                                WhenDataReceived(this.Index, subSamples);
                            });
                        }


                        /// Max PGA or PGV
                        double groundValue = maxData / Gain;

                        // 측정값이 위험 수치를 넘어서면
                        if (groundValue > TriggerValue)
                        {
                            TriggerValue = groundValue;


                            // 분석 중인 경우
                            if (m_waveBuffer.Count > 0)
                            {
                                // 가장 최근 파형에 트리거 전까지의 데이터 추가
                                var lastWave = m_waveBuffer.Last();
                                lastWave.AddWave(subSamples.Take(maxDataIndex));
                                lastWave.AddWaveToDraw(subSamples.Take(maxDataIndex));
                            }

                            // 새 파형 생성
                            var newWave = new Wave()
                            {
                                EventTimeUtc = DateTime.UtcNow,
                                IsAccel = IsAccel,
                                Length = 1,
                                MaxValue = groundValue,
                                TotalValue = groundValue,
                            };

                            // 초기 데이터가 없어서 분석이 종료되는 불상사 방지
                            // 어차피 최대값 풀링 때문에 0은 무시된다.
                            newWave.AddWave(0);

                            // 트리거 이후의 데이터 추가
                            newWave.AddWave(subSamples.Skip(maxDataIndex + 1));

                            // 파형 그렸을 때 트리거 이전 부분도 보이면 좋으므로 전부 추가
                            newWave.AddWaveToDraw(subSamples);

                            m_waveBuffer.Add(newWave);
                        }
                        else if (m_waveBuffer.Count > 0)
                        {
                            var lastWave = m_waveBuffer.Last();
                            lastWave.AddWave(subSamples);
                            lastWave.AddWaveToDraw(subSamples);
                        }


                        for (int w = 0; w < m_waveBuffer.Count; ++w)
                        {
                            Wave wave = m_waveBuffer[w];


                            int stopIndex = wave.BufferLength - WindowSize;
                            int checkedCount = 0;

                            for (int d = 0; d <= stopIndex; ++d)
                            {
                                ++checkedCount;

                                double max = wave.Buffer.Skip(d).Take(WindowSize)
                                    .Max((wav) => Math.Abs(wav));
                                double poolingValue = max / Gain;

                                // 안정화 되었거나 다른 파형이 나왔으면
                                if (poolingValue < NormalValue || poolingValue > wave.MaxValue)
                                {
                                    // 분석 종료
                                    checkedCount = wave.BufferLength;

                                    break;
                                }

                                ++wave.Length;
                                wave.TotalValue += poolingValue;
                            }

                            // 마지막 파형이 아니라면
                            if (w < m_waveBuffer.Count - 1)
                            {
                                // 분석 종료
                                checkedCount = wave.BufferLength;
                            }

                            // 처리한 데이터 제거
                            wave.RemoveWave(checkedCount);


                            double waveTime = (double)wave.Length / SamplingRate;

                            if (wave.IsDanger == false && waveTime > DangerWaveTime)
                            {
                                wave.IsDanger = true;


                                int mmi = 0;
                                if (IsAccel)
                                {
                                    mmi = Earthquake.ConvertPgaToMMI(wave.MaxValue);
                                }
                                else
                                {
                                    mmi = Earthquake.ConvertPgvToMMI(wave.MaxValue);
                                }

                                var msg = new Message()
                                {
                                    Level = Message.Priority.Critical,
                                    Sender = Channel + " " + Network + "_" + Station + " Station",
                                    Text = $@"{Name} {TypeText}의 진동에 관한 조기 분석 결과.
수치 : {(wave.MaxValue / DangerValue * 100.0).ToString("F2")}%
진도 : {Earthquake.MMIToString(mmi)}
지속시간 : 약 {string.Format("{0:F3}", waveTime)}초 이상
진동 수치 : {string.Format("{0:F3}", wave.TotalValue)} 이상

{Earthquake.GetKnowHowFromMMI(mmi)}",
                                };

                                m_msgQueue.Enqueue(msg);
                            }


                            // 분석이 종료되었으면
                            if (wave.BufferLength <= 0)
                            {
                                if (waveTime >= MinDangerWaveTime)
                                {
                                    int mmi = 0;
                                    if (IsAccel)
                                    {
                                        mmi = Earthquake.ConvertPgaToMMI(wave.MaxValue);
                                    }
                                    else
                                    {
                                        mmi = Earthquake.ConvertPgvToMMI(wave.MaxValue);
                                    }

                                    var msg = new Message()
                                    {
                                        Level = Message.Priority.Normal,
                                        Sender = Channel + " " + Network + "_" + Station + " Station",
                                        Text = $@"{Name} {TypeText}의 진동에 관한 최종 분석 결과.
수치 : {(wave.MaxValue / DangerValue * 100.0).ToString("F2")}%
진도 : {Earthquake.MMIToString(mmi)}
지속시간 : 약 {string.Format("{0:F3}", waveTime)}초
진동 수치 : {string.Format("{0:F3}", wave.TotalValue)}
지속시간이 매우 짧은 경우 오류일 확률이 높습니다.",
                                    };

                                    m_msgQueue.Enqueue(msg);


                                    try
                                    {
                                        string waveFile = SaveWaveToFile(wave);

                                        msg = new Message()
                                        {
                                            Level = Message.Priority.Normal,
                                            Sender = "해석 : https://neurowhai.tistory.com/356",
                                            Text = waveFile,
                                        };

                                        m_msgQueue.Enqueue(msg);
                                    }
                                    catch (Exception exp)
                                    {
                                        Console.WriteLine(exp.Message);
                                        Console.WriteLine(exp.StackTrace);
                                    }
                                }


                                // 파형 제거
                                m_waveBuffer.RemoveAt(w);
                                --w;
                            }
                        }


                        if (m_waveBuffer.Count <= 0)
                        {
                            // 트리거 기준 리셋
                            TriggerValue = DangerValue;


                            // 평소 수치 계산
                            NormalValue = subSamples
                                .Select((wav) => Math.Abs(wav) / Gain)
                                .Max();
                            NormalValue *= 2;

                            NormalValue = Math.Max(NormalValue, MinNormalValue);
                            NormalValue = Math.Min(NormalValue, MaxNormalValue);
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);
            }


            if (m_msgQueue.Count > 0)
            {
                m_logger.PushLog(m_msgQueue.Peek());


                return m_msgQueue.Dequeue();
            }
            else
            {
                return null;
            }
        }

        //###########################################################################################################

        protected void ReserveChunk(int sampleCount, double samplingRate)
        {
            Console.Write(this.Index);


            if ((int)samplingRate > 0)
            {
                this.SamplingRate = (int)samplingRate;
            }


            if (sampleCount > 1)
            {
                lock (m_lockSampleCount)
                {
                    // 얻을 샘플 개수인데 첫번째 데이터는 버리므로 1을 뺌.
                    m_sampleCountList.Enqueue(sampleCount - 1);
                }
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

        private string SaveWaveToFile(Wave wave)
        {
            var folderPath = "Waves";
            var folder = new DirectoryInfo(folderPath);

            Directory.CreateDirectory(folderPath);


            // 오래된 이미지 삭제.
            var imgs = folder.GetFiles();
            if (imgs.Length > 200)
            {
                var oldestImg = imgs.OrderBy(info => info.CreationTime).First();
                File.Delete(oldestImg.FullName);
            }


            string timestamp = wave.EventTimeUtc.ToString("yyyyMMdd HHmmss");
            string fileName = Path.Combine(folderPath, $"{Name} {timestamp} {wave.Length}.png");

            wave.DrawWave(fileName, 700, 186, Name, Gain);


            return fileName;
        }
    }
}
