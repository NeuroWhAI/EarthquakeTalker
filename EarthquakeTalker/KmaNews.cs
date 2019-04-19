using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Http;

namespace EarthquakeTalker
{
    class KmaNews : Worker
    {
        public KmaNews()
        {

        }

        //#############################################################################################

        protected string m_latestTitle = string.Empty;
        protected string m_latestTime = string.Empty;
        protected bool m_notiMode = true; // 이전 시간 정보를 얻기 위해 true로 설정.
        protected DateTime m_notiTimeLimit = DateTime.MinValue;

        protected HttpClient m_client = new HttpClient();

        //#############################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(6.0);
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_latestTitle = string.Empty;
        }

        protected override Message OnWork(Action<Message> sender)
        {
            try
            {
                var kmaNoti = m_client.GetByteArrayAsync(@"http://www.weather.go.kr/weather/earthquake_volcano/report.jsp");

                kmaNoti.Wait();


                var byteArray = kmaNoti.Result.ToArray();
                var encoding = Encoding.GetEncoding(51949/*euc-kr*/);
                var html = encoding.GetString(byteArray, 0, byteArray.Length);

                int index = -1;
                int endIndex = -1;


                if (m_notiMode)
                {
                    if (DateTime.UtcNow > m_notiTimeLimit)
                    {
                        m_notiMode = false;
                    }


                    string time;
                    string scale;
                    string intensity;
                    string location;


                    index = html.IndexOf("발생시각");

                    if (index < 0)
                    {
                        return null;
                    }

                    index = html.IndexOf("<td", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = html.IndexOf(">", index + 1);

                    endIndex = html.IndexOf("</td", index + 1);

                    time = html.Substring(index + 1, endIndex - index - 1);

                    if (!time.Contains("년"))
                    {
                        return null;
                    }

                    time = Util.ConvertHtmlToText(time).Trim();

                    if (string.IsNullOrEmpty(m_latestTime))
                    {
                        m_latestTime = time;

                        m_notiMode = false;

                        m_logger.PushLog($"테스트 출력\n{time}");

                        return null;
                    }
                    else if (time == m_latestTime)
                    {
                        return null;
                    }


                    index = html.IndexOf("규모", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = html.IndexOf("<td", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = html.IndexOf(">", index + 1);

                    endIndex = html.IndexOf("</td", index + 1);

                    scale = html.Substring(index + 1, endIndex - index - 1);
                    scale = Util.ConvertHtmlToText(scale).Trim();

                    if (!double.TryParse(scale, out double _))
                    {
                        return null;
                    }


                    index = html.IndexOf("진도", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = html.IndexOf("<td", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = html.IndexOf(">", index + 1);

                    endIndex = html.IndexOf("</td", index + 1);

                    intensity = html.Substring(index + 1, endIndex - index - 1);
                    intensity = Util.ConvertHtmlToText(intensity).Trim();

                    if (string.IsNullOrWhiteSpace(intensity))
                    {
                        return null;
                    }


                    index = html.IndexOf("위치", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = html.IndexOf("<td", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = html.IndexOf(">", index + 1);

                    endIndex = html.IndexOf("</td", index + 1);

                    location = html.Substring(index + 1, endIndex - index - 1);
                    location = Util.ConvertHtmlToText(location).Trim();

                    if (string.IsNullOrWhiteSpace(location))
                    {
                        return null;
                    }


                    m_notiMode = false;
                    m_latestTime = time;


                    var buffer = new StringBuilder();
                    buffer.AppendLine("지진속보가 발표되었습니다.");
                    buffer.AppendLine($"발생시각 : {time}");
                    buffer.AppendLine($"추정규모 : {scale}");
                    buffer.AppendLine($"예상진도 : {intensity}");
                    buffer.AppendLine($"추정위치 : {location}");
                    buffer.Append("수동으로 분석한 정보는 추가 발표될 예정입니다.");


                    return new Message()
                    {
                        Level = Message.Priority.Critical,
                        Sender = "기상청 지진속보",
                        Text = buffer.ToString(),
                    };
                }


                index = html.IndexOf("earthquake_report");

                if (index < 0)
                {
                    return null;
                }

                index = html.IndexOf(".xml", index + 1);

                if (index < 0)
                {
                    return null;
                }

                index = html.IndexOf(">", index + 1);

                if (index < 0)
                {
                    return null;
                }

                endIndex = html.IndexOf("<", index + 1);

                if (endIndex < 0)
                {
                    return null;
                }

                string title = html.Substring(index + 1, endIndex - index - 1);
                title = Util.ConvertHtmlToText(title);

                if (string.IsNullOrEmpty(m_latestTitle))
                {
                    m_latestTitle = title;

                    m_logger.PushLog($"테스트 출력\n{title}");
                }

                if (title.Contains("[지진속보]"))
                {
                    if (m_latestTitle != title)
                    {
                        m_latestTitle = title;

                        m_notiMode = true;
                        m_notiTimeLimit = DateTime.UtcNow + TimeSpan.FromMinutes(3.0);

                        m_logger.PushLog(title);
                    }
                }


                Console.Write('N');
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


                if (m_client != null)
                {
                    m_client.Dispose();
                }

                Thread.Sleep(5000);


                m_client = new HttpClient();
            }


            return null;
        }
    }
}
