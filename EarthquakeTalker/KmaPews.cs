using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Drawing;

namespace EarthquakeTalker
{
    class KmaPews : Worker
    {
        private const int HeadLength = 4;
        private const int MaxEqkStrLen = 60;
        private const int MaxEqkInfoLen = 120;
        private static string[] AreaNames = { "서울", "부산", "대구", "인천", "광주", "대전", "울산", "세종", "경기", "강원", "충북", "충남", "전북", "전남", "경북", "경남", "제주" };
        private static string[] MmiColors = { "#FFFFFF", "#FFFFFF", "#A0E6FF", "#92D050", "#FFFF00", "#FFC000", "#FF0000", "#A32777", "#632523", "#4C2600", "#000000" };

        //#############################################################################################

        public KmaPews()
        {
            m_mmiBrushes = MmiColors
                .Select((color) => new SolidBrush(HexCodeToColor(color)))
                .ToArray();
        }

        //#############################################################################################

        private readonly string DataPath = "https://www.weather.go.kr/pews/data";

        private string m_prevBinTime = string.Empty;
        private double m_tide = 1000;
        private DateTime m_nextSyncTime = DateTime.MinValue;
        private int m_prevPhase = 0;
        private string m_prevAlarmId = string.Empty;

        private string m_gridEqkId = null;
        private Image m_gridMap = null;
        private Brush[] m_mmiBrushes = null;
        private string m_gridFilePath = null;
        private readonly object m_syncGridPath = new object();

        //#############################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(0.2);

            m_gridMap = Image.FromFile("map.png");
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_prevBinTime = string.Empty;
            m_tide = 1000;
            m_nextSyncTime = DateTime.MinValue;
            m_prevPhase = 0;
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
                string binTime = DateTime.UtcNow.AddMilliseconds(-m_tide).ToString("yyyyMMddHHmmss");
                if (m_prevBinTime == binTime)
                {
                    return null;
                }
                m_prevBinTime = binTime;

                string url = $"{DataPath}/{binTime}.b";


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

        private Message HandleEqk(int phase, string body, byte[] infoBytes, out PointF epicenter)
        {
            string data = body.Substring(body.Length - (MaxEqkStrLen * 8 + MaxEqkInfoLen));
            string eqkStr = WebUtility.UrlDecode(Encoding.UTF8.GetString(infoBytes));

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

            // 페이즈가 넘어갔으며 이전에 전송한 것과 동일한 알람이 아니라면.
            if (phase > m_prevPhase && alarmId != m_prevAlarmId)
            {
                m_gridEqkId = eqkId;

                if (phase == 2)
                {
                    // 발생 시각, 규모, 최대 진도, 문구 정도는 부정확할 수 있어도 첫 정보에 포함되는 듯.

                    var buffer = new StringBuilder();
                    buffer.AppendLine("조기경보가 발표되었습니다.");
                    buffer.AppendLine($"정보 : {eqkStr}");
                    buffer.AppendLine($"발생 시각 : {eqkTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    buffer.AppendLine($"추정 규모 : {eqkMag.ToString("N1")}");
                    buffer.AppendLine($"최대 진도 : {Earthquake.MMIToString(eqkIntens)}({eqkIntens})");
                    buffer.AppendLine("대피 요령 : https://www.weather.go.kr/pews/man/m.html");
                    buffer.AppendLine("수동으로 분석한 정보는 추후 발표될 예정입니다.");
                    buffer.AppendLine();
                    buffer.Append(Earthquake.GetKnowHowFromMMI(eqkIntens));

                    m_prevAlarmId = alarmId;

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
                    buffer.AppendLine("지진정보가 발표되었습니다.");
                    buffer.AppendLine($"정보 : {eqkStr}");
                    buffer.AppendLine($"발생 시각 : {eqkTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    buffer.AppendLine($"규모 : {eqkMag.ToString("N1")}");
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

            var gridData = new List<int>();
            foreach (byte b in bytes)
            {
                string bStr = ByteToBinStr(b);
                gridData.Add(Convert.ToInt32(bStr.Substring(0, 4), 2));
                gridData.Add(Convert.ToInt32(bStr.Substring(4, 4), 2));
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
                File.Delete(oldestImg.FullName);
            }


            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd HHmmss");
            string fileName = Path.Combine(folderPath, $"{timestamp} {eqkId}.png");

            canvas.Save(fileName);


            return fileName;
        }
    }
}
