using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Net.Http;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EarthquakeTalker
{
    class KmaTsunami : Worker
    {
        public KmaTsunami()
        {

        }

        //#############################################################################################

        private readonly string ApiUrl = "http://apis.data.go.kr/1360000/EqkInfoService/getTsunamiMsg";
        private string m_apiKey = string.Empty;

        protected HttpClient m_client = new HttpClient();

        private DateTime? m_latestNotiTime = null;

        //#############################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            // 하루 API 호출 제한 횟수로 인한 제약.
            this.JobDelay = TimeSpan.FromSeconds(9.0);

            using (var sr = new StreamReader(new FileStream("EqkInfoService.txt", FileMode.Open)))
            {
                m_apiKey = sr.ReadLine().Trim();
            }

            m_latestNotiTime = null;
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_client?.Dispose();
            m_client = null;
        }

        protected override Message OnWork(Action<Message> sender)
        {
            try
            {
                var now = DateTime.UtcNow.AddHours(9); // UTC+9
                string startTime = now.AddDays(-1).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                string endTime = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                var apiTask = m_client.GetByteArrayAsync($"{ApiUrl}?serviceKey={m_apiKey}&dataType=JSON&numOfRows=5&pageNo=1&fromTmFc={startTime}&toTmFc={endTime}&cache={DateTimeOffset.Now.ToUnixTimeSeconds()}");
                apiTask.Wait();

                var byteArray = apiTask.Result.ToArray();
                string jsonStr = Encoding.UTF8.GetString(byteArray, 0, byteArray.Length);

                if (string.IsNullOrWhiteSpace(jsonStr))
                {
                    return null;
                }

                JObject json = JObject.Parse(jsonStr);

                string resultCode = json["response"]?["header"]?["resultCode"]?.Value<string>();
                if (resultCode == null || (resultCode != "00" && resultCode != "03"))
                {
                    // 성공도 아니고 데이터 없음도 아니라면 요청 실패이므로 재시도.
                    if (resultCode == null)
                    {
                        resultCode = "null";
                    }
                    Console.WriteLine();
                    Console.WriteLine($"T({resultCode})");
                    return null;
                }

                if (m_latestNotiTime == null && resultCode == "03")
                {
                    // 데이터가 없다는 것은 통보된 해일이 없다는 것이므로 없는 것으로 기록.
                    m_latestNotiTime = DateTime.MinValue;
                    m_logger.PushLog("기상청 지진해일 통보문 데이터 없음.");
                    return null;
                }

                JToken notiList = json["response"]?["body"]?["items"]?["item"];
                if (notiList == null || notiList.Type != JTokenType.Array)
                {
                    return null;
                }

                JToken latestNoti = null;
                DateTime latestNotiTime = DateTime.MinValue;
                foreach (var noti in notiList)
                {
                    if (noti["fcTp"] == null)
                    {
                        continue;
                    }

                    string notiTimeStr = noti["tmEf"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(notiTimeStr)
                        || !DateTime.TryParseExact(notiTimeStr, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out var notiTime))
                    {
                        continue;
                    }

                    if (latestNoti == null)
                    {
                        latestNoti = noti;
                        latestNotiTime = notiTime;
                    }
                    else if (notiTime > latestNotiTime)
                    {
                        latestNoti = noti;
                        latestNotiTime = notiTime;
                    }
                }

                if (m_latestNotiTime == null)
                {
                    m_latestNotiTime = latestNotiTime;

                    m_logger.PushLog(new Message
                    {
                        Level = Message.Priority.Low,
                        Sender = "기상청 지진해일 통보문",
                        Text = $"Latest time: {latestNotiTime.ToShortDateString()}",
                    });
                }
                else if (latestNoti != null && m_latestNotiTime < latestNotiTime)
                {
                    m_latestNotiTime = latestNotiTime;

                    int phase = latestNoti["fcTp"].Value<int>();
                    if (phase == 1 || phase == 2)
                    {
                        string phaseName = (phase == 1) ? "주의보" : "경보";
                        string location = latestNoti["reg"]?.Value<string>() ?? string.Empty;
                        string message = latestNoti["ann"]?.Value<string>() ?? string.Empty;
                        string order = latestNoti["rem"]?.Value<string>() ?? string.Empty;

                        var buffer = new StringBuilder();
                        buffer.AppendLine($"⚠️ 지진해일 {phaseName}가 발표되었습니다.");
                        buffer.AppendLine($"발효 시각 : {latestNotiTime:yyyy-MM-dd HH:mm}");
                        buffer.AppendLine($"지역 : {location}");
                        buffer.AppendLine("대피 요령 : http://www.safekorea.go.kr/idsiSFK/neo/sfk/cs/contents/prevent/prevent16.html");
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            buffer.AppendLine(message.Replace("\\n", "\n"));
                        }
                        if (!string.IsNullOrWhiteSpace(order))
                        {
                            buffer.AppendLine(order.Replace("\\n", "\n"));
                        }

                        return new Message()
                        {
                            Level = Message.Priority.Critical,
                            Sender = "기상청 지진해일 통보문",
                            Text = buffer.ToString().TrimEnd(),
                        };
                    }
                    else if (phase == 3)
                    {
                        string location = latestNoti["loc"]?.Value<string>() ?? string.Empty;

                        var buffer = new StringBuilder();
                        if (!string.IsNullOrWhiteSpace(location))
                        {
                            buffer.Append(location);
                            buffer.Append(" 지진으로 인한 ");
                        }
                        buffer.AppendLine("해일 주의보/경보가 해제되었습니다.");

                        return new Message()
                        {
                            Level = Message.Priority.Normal,
                            Sender = "기상청 지진해일 통보문",
                            Text = buffer.ToString().TrimEnd(),
                        };
                    }
                }
                else
                {
                    Console.Write('T');
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
                    Console.WriteLine(exp.InnerException.Message);
                    Console.WriteLine(exp.InnerException.StackTrace);
                }
            }


            return null;
        }
    }
}
