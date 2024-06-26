﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Drawing;
using System.Globalization;
using System.IO.Compression;

namespace EarthquakeTalker
{
    class KmaPews : Worker
    {
        private string DataPath = "https://www.weather.go.kr/pews/data";
        private int HeadLength = 4;
        private const int MaxEqkStrLen = 60;
        private const int MaxEqkInfoLen = 120;
        private static string[] AreaNames = { "서울", "부산", "대구", "인천", "광주", "대전", "울산", "세종", "경기", "강원", "충북", "충남", "전북", "전남", "경북", "경남", "제주" };
        private static string[] MmiColors = { "#FFFFFF", "#FFFFFF", "#A0E6FF", "#92D050", "#FFFF00", "#FFC000", "#FF0000", "#A32777", "#632523", "#4C2600", "#000000" };

        //#############################################################################################

        public KmaPews(FCMServer fcmServer = null)
        {
            m_fcm = fcmServer;

            m_mmiBrushes = MmiColors
                .Select((color) => new SolidBrush(HexCodeToColor(color)))
                .ToArray();
        }

        //#############################################################################################

        private string m_prevBinTime = string.Empty;
        private double m_tide = 1000;
        private DateTime m_nextSyncTime = DateTime.MinValue;
        private string m_prevAlarmId = string.Empty;

        private string m_gridEqkId = null;
        private Image m_gridMap = null;
        private Brush[] m_mmiBrushes = null;
        private string m_gridFilePath = null;
        private readonly object m_syncGridPath = new object();

        private bool m_stationUpdate = true;
        private List<PewsStation> m_stations = new List<PewsStation>();
        private PewsStnDatabase m_stationDb = null;
        private static readonly int StnMmiTrigger = 2;
        private int m_maxStnMmi = StnMmiTrigger - 1;
        private readonly TimeSpan StnMmiLife = TimeSpan.FromSeconds(8.0);

        private bool m_simMode = false;
        private DateTime m_simEndTime = DateTime.MinValue;

        private FCMServer m_fcm = null;
        private PewsJson m_fcmMessage = null;

        //#############################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(0.2);

            m_gridMap = Image.FromFile("map.png");

            m_stationUpdate = true;
            m_stations.Clear();
            m_maxStnMmi = StnMmiTrigger - 1;

            SyncTime();

            m_stationDb = new PewsStnDatabase();
            m_stationDb.LoadDatabase("stations.csv");

#if DEBUG
            //StartSimulation("2017000407", "20171115142931"); // 포항 5.4
            //StartSimulation("2020005363", "20200511194506"); // 북한 3.8
            //StartSimulation("2021000517", "20210203121756"); // 인천 2.2
            //StartSimulation("2021007178", "20211214171904"); // 제주 4.9
#endif
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_prevBinTime = string.Empty;
            m_tide = 1000;
            m_nextSyncTime = DateTime.MinValue;
            m_prevAlarmId = string.Empty;

            m_gridEqkId = null;
            m_gridMap?.Dispose();
            m_gridMap = null;
            foreach (var brush in m_mmiBrushes)
            {
                brush.Dispose();
            }
            m_mmiBrushes = null;
            lock (m_syncGridPath)
            {
                m_gridFilePath = null;
            }

            m_stationUpdate = true;
            m_stations.Clear();
            m_maxStnMmi = StnMmiTrigger - 1;

            m_stationDb = null;
        }

        protected override Message OnWork(Action<Message> sender)
        {
            try
            {
                // 예약된 진도 그리드 파일이 있다면 전송.

                string filePath;
                lock (m_syncGridPath)
                {
                    filePath = m_gridFilePath;
                    m_gridFilePath = null;
                }

                if (filePath != null)
                {
                    sender(new Message()
                    {
                        Level = Message.Priority.Normal,
                        Sender = "기상청 실시간 지진감시",
                        Text = filePath,
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            try
            {
                var binTime = DateTime.UtcNow.AddMilliseconds(-m_tide);
                string binTimeStr = binTime.ToString("yyyyMMddHHmmss");
                if (m_prevBinTime == binTimeStr)
                {
                    return null;
                }
                m_prevBinTime = binTimeStr;

                if (m_simMode && binTime >= m_simEndTime)
                {
                    StopSimulation();
                    return null;
                }

                string url = $"{DataPath}/{binTimeStr}";


                byte[] bytes = null;

                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                    try
                    {
                        bytes = client.DownloadData(url + ".b");
                    }
                    catch (WebException)
                    {
                        if (!m_simMode)
                        {
                            // 서버 시간과 동기화 실패 시 적절히 오프셋 조정.
                            if (!SyncTime())
                            {
                                if (m_tide < 1000)
                                {
                                    m_tide += 200;
                                }
                                else
                                {
                                    m_tide -= 200;
                                }
                            }
                        }

                        return null;
                    }


                    // 시간 동기화.
                    if (!m_simMode && DateTime.UtcNow >= m_nextSyncTime)
                    {
                        m_nextSyncTime = DateTime.UtcNow + TimeSpan.FromSeconds(10.0);

                        string stStr = client.ResponseHeaders.Get("ST");
                        if (!string.IsNullOrWhiteSpace(stStr)
                            && double.TryParse(stStr, out double serverTime))
                        {
                            m_tide = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - serverTime * 1000 + 1000;
                        }
                    }
                }


                if (bytes != null && bytes.Length > MaxEqkStrLen)
                {
                    var headerBuff = new StringBuilder();
                    for (int i = 0; i < HeadLength; ++i)
                    {
                        headerBuff.Append(ByteToBinStr(bytes[i]));
                    }
                    string header = headerBuff.ToString();

                    var bodyBuff = new StringBuilder();
                    for (int i = HeadLength; i < bytes.Length; ++i)
                    {
                        bodyBuff.Append(ByteToBinStr(bytes[i]));
                    }
                    string body = bodyBuff.ToString();


                    // 관측소 정보 업데이트 신호 확인.
                    m_stationUpdate = (m_stationUpdate || (header[0] == '1'));

                    int phase = 0;
                    if (header[1] == '0')
                    {
                        phase = 1;
                    }
                    else if (header[1] == '1' && header[2] == '0')
                    {
                        phase = 2;
                    }
                    else if (header[2] == '1')
                    {
                        phase = 3;
                    }


                    Message msg = null;

                    if (phase > 1)
                    {
                        var infoBytes = bytes.Skip(bytes.Length - MaxEqkStrLen).ToArray();
                        msg = HandleEqk(phase, body, infoBytes, out var epicenter);

                        if (m_gridEqkId != null)
                        {
                            string eqkId = m_gridEqkId;
                            m_gridEqkId = null;

                            Task.Factory.StartNew(async () =>
                            {
                                await Task.Delay(200);

                                try
                                {
                                    await RequestGridData(eqkId, phase, epicenter);
                                }
                                catch (Exception)
                                {
                                    // 재시도.
                                    m_gridEqkId = eqkId;
                                }
                            });
                        }
                    }


                    if (m_stationUpdate)
                    {
                        byte[] stnBytes = null;

                        using (var client = new WebClient())
                        {
                            client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                            try
                            {
                                stnBytes = client.DownloadData(url + ".s");
                            }
                            catch (WebException)
                            {
                                stnBytes = null;
                            }
                        }

                        if (stnBytes != null && stnBytes.Length > 0)
                        {
                            bodyBuff = new StringBuilder();
                            for (int i = 0; i < stnBytes.Length; ++i)
                            {
                                bodyBuff.Append(ByteToBinStr(stnBytes[i]));
                            }

                            HandleStn(bodyBuff.ToString());
                        }
                    }


                    // 지진 정보 발표가 없는 상태이고 관측소 데이터가 있으면 진도 분석.
                    if (phase <= 1 && m_stations.Count > 0)
                    {
                        // 위에서 관측소 데이터 갱신시 성공했을때만.
                        if (!m_stationUpdate)
                        {
                            msg = HandleMmi(body);
                        }
                    }
                    else
                    {
                        m_maxStnMmi = StnMmiTrigger - 1;

                        // 관측소 진도 초기화.
                        foreach (var stn in m_stations)
                        {
                            stn.ResetMmi();
                        }
                    }


                    Console.Write('P');


                    return msg;
                }
            }
            catch (Exception exp)
            {
                if (exp.InnerException == null)
                {
                    Console.WriteLine(exp.Message);
                    Console.WriteLine(exp.StackTrace);
                }
                else
                {
                    Console.WriteLine(exp.Message);
                    Console.WriteLine(exp.InnerException.Message);
                    Console.WriteLine(exp.InnerException.StackTrace);
                }

                Thread.Sleep(5000);
            }


            return null;
        }

        //#############################################################################################

        private Color HexCodeToColor(string code)
        {
            if (code.First() != '#')
            {
                throw new ArgumentException();
            }

            code = code.Substring(1);

            if (code.Length == 6)
            {
                code = "FF" + code;
            }

            return Color.FromArgb(Convert.ToInt32(code, 16));
        }

        private string ByteToBinStr(byte val)
        {
            return Convert.ToString(val, 2).PadLeft(8, '0');
        }

        private bool SyncTime()
        {
            try
            {
                var serverTime = TimeManager.GetNetworkTime("time.windows.com", 3000);
                m_tide = (DateTime.UtcNow - serverTime).TotalMilliseconds + 1000;
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private void StartSimulation(string eqkId, string eqkStartTime)
        {
            var startTime = DateTime.ParseExact(eqkStartTime, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            m_simMode = true;
            m_simEndTime = startTime.AddHours(-9) + TimeSpan.FromSeconds(300);

            HeadLength = 1;
            DataPath += $"/{eqkId}";
            m_tide = (DateTime.UtcNow.AddHours(9) - startTime).TotalMilliseconds;
        }

        private void StopSimulation()
        {
            m_simMode = false;

            HeadLength = 4;
            DataPath = "https://www.weather.go.kr/pews/data";
            m_tide = 1000;

            m_stationUpdate = true;
        }

        private Message HandleEqk(int phase, string body, byte[] infoBytes, out PointF epicenter)
        {
            string data = body.Substring(body.Length - (MaxEqkStrLen * 8 + MaxEqkInfoLen));
            string eqkStr = WebUtility.UrlDecode(Encoding.UTF8.GetString(infoBytes)).Trim();

            double origLat = 30 + (double)Convert.ToInt32(data.Substring(0, 10), 2) / 100;
            double origLon = 124 + (double)Convert.ToInt32(data.Substring(10, 10), 2) / 100;
            double eqkMag = (double)Convert.ToInt32(data.Substring(20, 7), 2) / 10;
            double eqkDep = (double)Convert.ToInt32(data.Substring(27, 10), 2) / 10;
            int eqkUnixTime = Convert.ToInt32(data.Substring(37, 32), 2);
            var eqkTime = DateTimeOffset.FromUnixTimeSeconds(eqkUnixTime + 9 * 3600).AddHours(9.0);
            string eqkId = "20" + Convert.ToInt32(data.Substring(69, 26), 2); // TODO: 22세기가 되면 "20"이 아니게 되는건가?
            int eqkIntens = Convert.ToInt32(data.Substring(95, 4), 2);
            string eqkMaxAreaStr = data.Substring(99, 17);
            var eqkMaxArea = new List<string>();
            if (eqkMaxAreaStr != new string('1', eqkMaxAreaStr.Length))
            {
                for (int i = 0; i < eqkMaxAreaStr.Length; ++i)
                {
                    if (eqkMaxAreaStr[i] == '1')
                    {
                        eqkMaxArea.Add(AreaNames[i]);
                    }
                }
            }

            epicenter = new PointF(
                (float)((origLon - 124.5) * 113 - 4),
                (float)((38.9 - origLat) * 138.4 - 7));

            string alarmId = eqkId + phase;
            Message msg = null;

            // 페이즈가 바뀌었거나 이전에 전송한 것과 동일한 알람이 아니라면.
            if (alarmId != m_prevAlarmId)
            {
                m_gridEqkId = eqkId;

                if (!string.IsNullOrWhiteSpace(eqkId))
                {
                    string eqkLoc = RequestLocData(eqkId, phase);
                    if (!string.IsNullOrEmpty(eqkLoc))
                    {
                        eqkStr = eqkLoc;
                    }
                }

                if (phase == 2)
                {
                    // 발생 시각, 규모, 최대 진도, 문구 정도는 부정확할 수 있어도 첫 정보에 포함되는 듯.

                    var buffer = new StringBuilder();
                    buffer.AppendLine("⚠️ 지진 신속정보가 발표되었습니다.");
                    buffer.AppendLine($"정보 : {eqkStr}");
                    buffer.AppendLine($"발생 시각 : {eqkTime:yyyy-MM-dd HH:mm:ss}");
                    buffer.AppendLine($"추정 규모 : {eqkMag:N1}");
                    buffer.AppendLine($"최대 진도 : {Earthquake.MMIToString(eqkIntens)}({eqkIntens})");
                    buffer.AppendLine("대피 요령 : https://www.weather.go.kr/pews/man/m.html");
                    buffer.AppendLine("수동으로 분석한 정보는 추후 발표될 예정입니다.");
                    buffer.AppendLine();
                    buffer.Append(Earthquake.GetKnowHowFromMMI(eqkIntens));

                    m_prevAlarmId = alarmId;

                    m_fcmMessage = new PewsJson
                    {
                        time = (eqkUnixTime + 9 * 3600).ToString(),
                        msg = eqkStr,
                        scale = eqkMag.ToString("N1"),
                        mmi = eqkIntens.ToString(),
                    };

                    return new Message()
                    {
                        Level = Message.Priority.Critical,
                        Sender = "기상청 실시간 지진감시",
                        Text = buffer.ToString().TrimEnd(),
                    };
                }
                else if (phase == 3)
                {
                    // 분석 완료된 것 같고 깊이, 영향 지역이 나옴.

                    var buffer = new StringBuilder();
                    buffer.AppendLine("지진 상세정보가 발표되었습니다.");
                    buffer.AppendLine($"정보 : {eqkStr}");
                    buffer.AppendLine($"발생 시각 : {eqkTime:yyyy-MM-dd HH:mm:ss}");
                    buffer.AppendLine($"규모 : {eqkMag:N1}");
                    buffer.Append($"깊이 : ");
                    buffer.AppendLine((eqkDep == 0) ? "-" : $"{eqkDep} km");
                    buffer.AppendLine($"최대 진도 : {Earthquake.MMIToString(eqkIntens)}({eqkIntens})");
                    buffer.AppendLine($"영향 지역 : {string.Join(", ", eqkMaxArea)}");

                    m_prevAlarmId = alarmId;

                    return new Message()
                    {
                        Level = Message.Priority.High,
                        Sender = "기상청 실시간 지진감시",
                        Text = buffer.ToString().TrimEnd(),
                    };
                }
            }

            return msg;
        }

        private void HandleStn(string body)
        {
            var stnLat = new List<double>();
            var stnLon = new List<double>();

            for (int i = 0; i + 20 <= body.Length; i += 20)
            {
                stnLat.Add(30 + (double)Convert.ToInt32(body.Substring(i, 10), 2) / 100);
                stnLon.Add(120 + (double)Convert.ToInt32(body.Substring(i + 10, 10), 2) / 100);
            }

            if (stnLat.Count < 99)
            {
                // 재시도.
                return;
            }

            if (m_stations.Count <= 0)
            {
                m_logger.PushLog($"관측소 수 : {stnLat.Count}");
            }

            m_stations.Clear();
            for (int i = 0; i < stnLat.Count; ++i)
            {
                string location = m_stationDb.GetStationInfoAround(stnLat[i], stnLon[i]).Location;
                if (string.IsNullOrEmpty(location))
                {
                    m_logger.PushLog($"Can not found a station on {stnLat[i]}, {stnLon[i]}.");
                }

                m_stations.Add(new PewsStation
                {
                    Latitude = stnLat[i],
                    Longitude = stnLon[i],
                    Location = location,
                });
            }

            // 다음 업데이트 신호 대기.
            m_stationUpdate = false;
        }

        private Message HandleMmi(string body)
        {
            const double ClusterDistance = 60.0;
            const int MinClusterSize = 4;

            if (m_stations.Count <= 0)
            {
                return null;
            }

            var mmiData = new List<int>();

            for (int i = 0; i < body.Length; i += 4)
            {
                if (mmiData.Count >= m_stations.Count)
                {
                    break;
                }

                int rawMmi = Convert.ToInt32(body.Substring(i, 4), 2);
                mmiData.Add(rawMmi);
            }

            if (mmiData.Count < m_stations.Count)
            {
                return null;
            }

            // 관측소 진도 갱신.
            for (int i = 0; i < m_stations.Count; ++i)
            {
                var stn = m_stations[i];
                int rawMmi = mmiData[i];

                stn.UpdateMmi(rawMmi, StnMmiLife);
            }

            int maxClusterMmi = 0;
            var largeClusterMmi = new List<int> { 0 };
            string maxClusterLoc = string.Empty;

            bool[] visited = new bool[m_stations.Count];
            for (int i = 0; i < m_stations.Count; ++i)
            {
                if (visited[i])
                {
                    continue;
                }

                var clusterMmi = new List<int> { 0 };
                int centerStn = -1;

                var leftStns = new Queue<int>();
                leftStns.Enqueue(i);

                while (leftStns.Count > 0)
                {
                    int current = leftStns.Dequeue();

                    if (visited[current])
                    {
                        continue;
                    }
                    visited[current] = true;

                    var stn = m_stations[current];
                    int mmi = stn.Mmi;

                    // 진도 2 이상이거나 진도 1 안에서 큰 축에 속하는 경우에만 노드로 취급.
                    if (mmi < 2 && stn.RawMmi < 14)
                    {
                        continue;
                    }

                    clusterMmi.Add(mmi);

                    if (centerStn < 0 || stn.Mmi > m_stations[centerStn].Mmi)
                    {
                        centerStn = current;
                    }

                    double centerX = (stn.Longitude - 124.5) * 113;
                    double centerY = (38.9 - stn.Latitude) * 138.4;

                    for (int next = 0; next < m_stations.Count; ++next)
                    {
                        if (visited[next])
                        {
                            continue;
                        }

                        var nextStn = m_stations[next];

                        double subX = (nextStn.Longitude - 124.5) * 113 - centerX;
                        double subY = (38.9 - nextStn.Latitude) * 138.4 - centerY;

                        double distanceSqr = subX * subX + subY * subY;
                        if (distanceSqr < ClusterDistance * ClusterDistance)
                        {
                            leftStns.Enqueue(next);
                        }
                    }
                }

                if (centerStn >= 0 && m_stations[centerStn].Mmi > maxClusterMmi)
                {
                    maxClusterMmi = m_stations[centerStn].Mmi;
                    largeClusterMmi = clusterMmi;
                    maxClusterLoc = m_stations[centerStn].Location;
                }
            }

            if (maxClusterMmi <= m_maxStnMmi)
            {
                Message msg = null;

                // 안정화 되었으면.
                if (maxClusterMmi < StnMmiTrigger)
                {
                    // NOTE: 큰 지진의 경우 신속히 지진 속보가 발표되므로 이 부분은 실행되지 않을 수도 있음.

                    // 트리거를 초과했었으면 진도 지도 송출.
                    if (m_maxStnMmi >= StnMmiTrigger)
                    {
                        string filePath = SaveStnMaxMmiToFile();

                        // NOTE: 법적 문제로 제공 중단.
                        /*msg = new Message()
                        {
                            Level = Message.Priority.Normal,
                            Sender = "기상청 실시간 지진감시",
                            Text = filePath,
                        };*/
                    }

                    // 관측소 진도 초기화.
                    foreach (var stn in m_stations)
                    {
                        stn.ResetMmi();
                    }

                    // 트리거 레벨 초기화.
                    m_maxStnMmi = StnMmiTrigger - 1;
                }

                return msg;
            }

            // 진도 I 개수는 가중치를 낮게 두고 클러스터 크기 확인.
            int mmi1Cnt = largeClusterMmi.Count((m) => m == 1);
            if ((largeClusterMmi.Count - mmi1Cnt + mmi1Cnt * 0.5) >= MinClusterSize + 1)
            {
                int[] mmiCnt = new int[14];
                for (int mmi = 0; mmi < mmiCnt.Length; ++mmi)
                {
                    mmiCnt[mmi] = largeClusterMmi.Count((m) => m == mmi);
                }

                var buffer = new StringBuilder();

                if (m_maxStnMmi >= StnMmiTrigger)
                {
                    buffer.AppendLine("⚠️ 감시 화면에 대한 요약정보가 갱신되었습니다.");
                }
                else
                {
                    buffer.AppendLine("⚠️ 실시간 감시 화면에 대한 요약정보입니다.");
                }

                if (!string.IsNullOrEmpty(maxClusterLoc))
                {
                    buffer.AppendLine($"관측소 위치 : {maxClusterLoc} 인근");
                }

                buffer.AppendLine($"최대진도 : {Earthquake.MMIToString(maxClusterMmi)}({maxClusterMmi})");

                for (int mmi = mmiCnt.Length - 1; mmi >= 1; --mmi)
                {
                    if (mmiCnt[mmi] > 0)
                    {
                        buffer.Append($"진도 {Earthquake.MMIToString(mmi)}({mmi}) : ");
                        buffer.AppendLine($"{mmiCnt[mmi]}건");
                    }
                }

                if (maxClusterMmi >= 5)
                {
                    buffer.AppendLine("흔들림이 느껴질 때 : https://www.weather.go.kr/pews/man/m.html");
                }

                buffer.AppendLine("실제 지진은 신속/상세정보로 발표됩니다.");
                buffer.AppendLine();
                buffer.Append(Earthquake.GetKnowHowFromMMI(maxClusterMmi));

                // 임시 트리거 레벨을 높힘.
                m_maxStnMmi = maxClusterMmi;

                // NOTE: 법적 문제로 제공 중단.
                /*return new Message()
                {
                    Level = ((maxClusterMmi >= 5) ? Message.Priority.Critical : Message.Priority.High),
                    Sender = "기상청 실시간 지진감시",
                    Text = buffer.ToString().TrimEnd(),
                };*/
            }

            return null;
        }

        private async Task RequestGridData(string eqkId, int phase, PointF epicenter)
        {
            string url = $"{DataPath}/{eqkId}.{(phase == 2 ? 'e' : 'i')}";

            byte[] bytes = null;

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                    bytes = await client.DownloadDataTaskAsync(url);
                }
            }
            catch (Exception)
            {
                bytes = null;
            }

            if (bytes == null || bytes.Length <= 0)
            {
                // 나중에 재시도.
                m_gridEqkId = eqkId;
                return;
            }

            if (m_fcm != null && m_fcmMessage != null && phase == 2)
            {
                byte[] compressedBytes = Ionic.Zlib.ZlibStream.CompressBuffer(bytes);

                m_fcmMessage.grid = BitConverter.ToString(compressedBytes).Replace("-", "");
                Task.Factory.StartNew(new Action(() =>
                {
                    for (int retry = 4; retry >= 0; --retry)
                    {
                        try
                        {
#if DEBUG
                            m_fcm.SendData(m_fcmMessage, "test", 60);
#else
                            m_fcm.SendData(m_fcmMessage, "eqk", 60);
#endif
                            Console.Write('F');
                            break;
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(err.Message);
                            Console.WriteLine(err.StackTrace);
                        }
                    }
                })).GetAwaiter();
            }

            var gridData = new List<int>();
            foreach (byte b in bytes)
            {
                string bStr = ByteToBinStr(b);
                for (int offset = 0; offset < 8; offset += 4)
                {
                    int mmi = Convert.ToInt32(bStr.Substring(offset, 4), 2);
                    if (mmi < 0)
                    {
                        mmi = 0;
                    }
                    else if (mmi > 11)
                    {
                        // 세분화 된 진도 I.
                        mmi = 1;
                    }

                    gridData.Add(mmi);
                }
            }

            using (var canvas = new Bitmap(m_gridMap.Width, m_gridMap.Height))
            using (var g = Graphics.FromImage(canvas))
            using (var brhBack = new SolidBrush(Color.FromArgb(211, 211, 211)))
            {
                // Background
                g.FillRectangle(brhBack, 0, 0, canvas.Width, canvas.Height);

                // Intensity
                var mmiIterator = gridData.GetEnumerator();
                bool isEnd = false;
                for (double i = 38.85; i > 33; i -= 0.05)
                {
                    for (double j = 124.5; j < 132.05; j += 0.05)
                    {
                        if (!mmiIterator.MoveNext())
                        {
                            isEnd = true;
                            break;
                        }

                        int mmi = mmiIterator.Current;

                        if (mmi >= 0 && mmi < m_mmiBrushes.Length)
                        {
                            var brush = m_mmiBrushes[mmi];
                            float x = (float)((j - 124.5) * 113 - 4);
                            float y = (float)((38.9 - i) * 138.4 - 7);

                            g.FillRectangle(brush, x, y, 8, 8);
                        }
                    }

                    if (isEnd)
                    {
                        break;
                    }
                }

                // Map
                // DPI 상관없이 그리기 위한 버전 사용.
                g.DrawImage(m_gridMap, new Rectangle(0, 0, m_gridMap.Width, m_gridMap.Height));

                // Epicenter
                if (epicenter.X > -32 && epicenter.X < canvas.Width + 32
                    && epicenter.Y > -32 && epicenter.Y < canvas.Height + 32)
                {
                    g.FillEllipse(Brushes.Blue, epicenter.X - 4, epicenter.Y - 4, 8, 8);
                    g.DrawEllipse(Pens.Blue, epicenter.X - 8, epicenter.Y - 8, 16, 16);
                    g.DrawEllipse(Pens.Blue, epicenter.X - 12, epicenter.Y - 12, 24, 24);
                }

                g.Flush();

                string filePath = SaveGridToFile(canvas, eqkId);

                lock (m_syncGridPath)
                {
                    m_gridFilePath = filePath;
                }
            }
        }

        private string RequestLocData(string eqkId, int phase)
        {
            byte[] bytes = null;

            try
            {
                string fileName = string.Empty;
                if (phase == 2)
                {
                    fileName = $"{eqkId}.le";
                }
                else if (phase == 3)
                {
                    fileName = $"{eqkId}.li";
                }
                else
                {
                    return string.Empty;
                }

                if (!string.IsNullOrEmpty(fileName))
                {
                    using (var client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                        string url = $"{DataPath}/{fileName}";
                        bytes = client.DownloadData(url);
                    }
                }
            }
            catch
            {
                bytes = null;
            }

            if (bytes == null || bytes.Length <= 0)
            {
                return string.Empty;
            }

            string json = Encoding.UTF8.GetString(bytes);

            string propName = "\"info_ko\"";
            int idx = json.IndexOf(propName);
            if (idx < 0)
            {
                return string.Empty;
            }

            idx = json.IndexOf('"', idx + propName.Length);
            if (idx < 0)
            {
                return string.Empty;
            }

            int endIdx = json.IndexOf('"', idx + 1);
            if (endIdx < 0)
            {
                return string.Empty;
            }

            return json.Substring(idx + 1, endIdx - idx - 1).Trim();
        }

        private string SaveGridToFile(Bitmap canvas, string eqkId)
        {
            var folderPath = "Grid";
            var folder = new DirectoryInfo(folderPath);

            Directory.CreateDirectory(folderPath);


            // 오래된 이미지 삭제.
            var imgs = folder.GetFiles();
            if (imgs.Length > 100)
            {
                var oldestImg = imgs.OrderBy(info => info.CreationTime).First();
                oldestImg.Delete();
            }


            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd HHmmss");
            string fileName = Path.Combine(folderPath, $"{timestamp} {eqkId}.png");

            canvas.Save(fileName);


            return fileName;
        }

        private string SaveStnMaxMmiToFile()
        {
            int maxMmi = -1;

            using (var canvas = new Bitmap(m_gridMap.Width, m_gridMap.Height))
            using (var g = Graphics.FromImage(canvas))
            using (var brhBack = new SolidBrush(Color.FromArgb(211, 211, 211)))
            {
                // Background
                g.FillRectangle(brhBack, 0, 0, canvas.Width, canvas.Height);

                // Map
                // DPI 상관없이 그리기 위한 버전 사용.
                g.DrawImage(m_gridMap, new Rectangle(0, 0, m_gridMap.Width, m_gridMap.Height));

                // Intensity
                foreach (var stn in m_stations.OrderBy((s) => s.MaxMmi))
                {
                    int mmi = stn.MaxMmi;

                    if (maxMmi < 0)
                    {
                        maxMmi = mmi;
                    }

                    if ((mmi >= 2 || stn.RawMmi >= 14) && mmi < m_mmiBrushes.Length)
                    {
                        var brush = m_mmiBrushes[mmi];
                        float x = (float)((stn.Longitude - 124.5) * 113 + 1);
                        float y = (float)((38.9 - stn.Latitude) * 138.4 + 1);

                        // 진도가 클수록 원을 작게 그림.
                        float circleRadius = Math.Max(24.0f - mmi * 2.0f, 1.0f);

                        g.FillEllipse(brush, x - circleRadius, y - circleRadius,
                            circleRadius * 2, circleRadius * 2);
                    }
                }

                // Station
                foreach (var stn in m_stations.OrderBy((s) => s.MaxMmi))
                {
                    int mmi = stn.MaxMmi;
                    if (mmi >= 0 && mmi < m_mmiBrushes.Length)
                    {
                        var brush = m_mmiBrushes[mmi];
                        float x = (float)((stn.Longitude - 124.5) * 113 - 4);
                        float y = (float)((38.9 - stn.Latitude) * 138.4 - 4);

                        g.FillRectangle(brush, x, y, 10, 10);
                        g.DrawRectangle(Pens.Black, x, y, 10, 10);
                    }
                }

                g.Flush();


                // Save to file.
                //

                var folderPath = "Station";
                var folder = new DirectoryInfo(folderPath);

                Directory.CreateDirectory(folderPath);


                // 오래된 이미지 삭제.
                var imgs = folder.GetFiles();
                if (imgs.Length > 100)
                {
                    var oldestImg = imgs.OrderBy(info => info.CreationTime).First();
                    oldestImg.Delete();
                }


                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd HHmmss");
                string fileName = Path.Combine(folderPath, $"{timestamp} {maxMmi}.png");

                canvas.Save(fileName);

                return fileName;
            }
        }
    }
}
