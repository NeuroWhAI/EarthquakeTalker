using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Net;
using System.IO;
using System.Web;

namespace EarthquakeTalker
{
    class KmaPews : Worker
    {
        private const int HeadLength = 4;
        private const int MaxEqkStrLen = 60;
        private const int MaxEqkInfoLen = 120;
        private static string[] AreaNames = { "서울", "부산", "대구", "인천", "광주", "대전", "울산", "세종", "경기", "강원", "충북", "충남", "전북", "전남", "경북", "경남", "제주" };

        //#############################################################################################

        public KmaPews()
        {
            
        }

        //#############################################################################################

        private string m_prevBinTime = string.Empty;
        private double m_tide = 1000;
        private DateTime m_nextSyncTime = DateTime.MinValue;
        private int m_prevPhase = 0;

        //#############################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(0.2);
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_prevBinTime = string.Empty;
            m_tide = 1000;
            m_nextSyncTime = DateTime.MinValue;
            m_prevPhase = 0;
        }

        protected override Message OnWork(Action<Message> sender)
        {
            try
            {
                string binTime = DateTime.UtcNow.AddMilliseconds(-m_tide).ToString("yyyyMMddHHmmss");
                if (m_prevBinTime == binTime)
                {
                    return null;
                }
                m_prevBinTime = binTime;

                string url = $"https://www.weather.go.kr/pews/data/{binTime}.b";


                byte[] bytes = null;

                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                    bytes = client.DownloadData(url);


                    // 시간 동기화.
                    if (DateTime.UtcNow >= m_nextSyncTime)
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

                    var bodyBuff = new StringBuilder(ByteToBinStr(bytes[0]));
                    for (int i = HeadLength; i < bytes.Length; ++i)
                    {
                        bodyBuff.Append(ByteToBinStr(bytes[i]));
                    }
                    string body = bodyBuff.ToString();


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
                        msg = HandleEqk(phase, body, infoBytes);
                    }

                    m_prevPhase = phase;


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

        private string ByteToBinStr(byte val)
        {
            return Convert.ToString(val, 2).PadLeft(8, '0');
        }

        private Message HandleEqk(int phase, string body, byte[] infoBytes)
        {
            string data = body.Substring(body.Length - (MaxEqkStrLen * 8 + MaxEqkInfoLen));
            string eqkStr = WebUtility.UrlDecode(Encoding.UTF8.GetString(infoBytes));

            double origLat = 30 + (double)Convert.ToInt32(data.Substring(0, 10), 2) / 100;
            double origLon = 124 + (double)Convert.ToInt32(data.Substring(10, 10), 2) / 100;
            double eqkMag = (double)Convert.ToInt32(data.Substring(20, 7), 2) / 10;
            double eqkDep = (double)Convert.ToInt32(data.Substring(27, 10), 2) / 10;
            int eqkUnixTime = Convert.ToInt32(data.Substring(37, 32), 2);
            var eqkTime = DateTimeOffset.FromUnixTimeSeconds(eqkUnixTime + 9 * 3600).AddHours(9.0);
            string eqkId = "20" + Convert.ToInt32(data.Substring(69, 26), 2);
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

            Message msg = null;

            if (phase > m_prevPhase)
            {
                if (phase == 2)
                {
                    // 발생 시각, 규모, 최대 진도, 문구 정도는 부정확할 수 있어도 첫 정보에 포함되는 듯.

                    var buffer = new StringBuilder();
                    buffer.AppendLine("(시범) 조기경보가 발표되었습니다.");
                    buffer.AppendLine($"정보 : {eqkStr}");
                    buffer.AppendLine($"발생 시각 : {eqkTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    buffer.AppendLine($"추정 규모 : {eqkMag.ToString("N1")}");
                    buffer.AppendLine($"최대 진도 : {Earthquake.MMIToString(eqkIntens)}({eqkIntens})");
                    buffer.AppendLine("대피 요령 : https://www.weather.go.kr/pews/man/m.html");
                    buffer.AppendLine("수동으로 분석한 정보는 추후 발표될 예정입니다.");
                    buffer.AppendLine();
                    buffer.Append(Earthquake.GetKnowHowFromMMI(eqkIntens));

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
                    buffer.AppendLine("(시범) 지진정보가 발표되었습니다.");
                    buffer.AppendLine($"정보 : {eqkStr}");
                    buffer.AppendLine($"발생 시각 : {eqkTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    buffer.AppendLine($"규모 : {eqkMag.ToString("N1")}");
                    buffer.AppendLine($"깊이 : {eqkDep} km");
                    buffer.AppendLine($"최대 진도 : {Earthquake.MMIToString(eqkIntens)}({eqkIntens})");
                    buffer.AppendLine($"영향 지역 : {string.Join(", ", eqkMaxArea)}");

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
    }
}
