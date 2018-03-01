using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;

namespace EarthquakeTalker
{
    public class WeatherI : Worker
    {
        public WeatherI()
        {

        }

        //#############################################################################################

        protected string m_latestNoti = string.Empty;

        protected HttpClient m_client = new HttpClient();

        //#############################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(6.0);
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_latestNoti = string.Empty;

            m_client = null;
        }

        protected override Message OnWork(Action<Message> sender)
        {
            try
            {
                var bytes = m_client.GetByteArrayAsync(@"https://www.weatheri.co.kr/special/special03.php");

                bytes.Wait();


                var byteArray = bytes.Result.ToArray();
                var encoding = Encoding.UTF8;
                var html = encoding.GetString(byteArray, 0, byteArray.Length);

                if (string.IsNullOrWhiteSpace(html) == false)
                {
                    int offset = 0;

                    string date = GetTableContent(html, "진원시", offset, out offset);
                    string location = GetTableContent(html, "<td", offset, out offset);
                    string scaleText = GetTableContent(html, "<td", offset, out offset);
                    string description = GetTableContent(html, "<td", offset, out offset);

                    if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(location)
                        || string.IsNullOrEmpty(scaleText) || string.IsNullOrEmpty(description))
                    {
                        return null;
                    }

                    var buffer = new StringBuilder();
                    buffer.Append("진원시 : ");
                    buffer.AppendLine(date);
                    buffer.Append("진앙 : ");
                    buffer.AppendLine(location);
                    buffer.Append("규모 : ");
                    buffer.AppendLine(scaleText);
                    buffer.Append("설명 : ");
                    buffer.AppendLine(description);

                    string message = buffer.ToString();

                    double scale = 0.0;

                    if (double.TryParse(scaleText, out scale) == false)
                    {
                        return null;
                    }
                    else if (string.IsNullOrEmpty(m_latestNoti))
                    {
                        m_latestNoti = message;

                        m_logger.PushLog(message);
                    }
                    else if (m_latestNoti != message)
                    {
                        m_latestNoti = message;

                        var msgLevel = Message.Priority.Normal;

                        var koreaKeywords = new string[]
                        {
                            "경북", "경남", "경기", "전남", "전북", "제주", "서울", "충남", "충북",
                            "북도", "남도", "광역시", "특별",
                            "부산", "대구", "인천", "광주", "대전", "울산", "세종", "강원",
                        };

                        if (koreaKeywords.Any((text) => location.Contains(text)))
                        {
                            msgLevel = Message.Priority.High;


                            if (scale > 0)
                            {
                                buffer.AppendLine();

                                buffer.Append(Earthquake.GetKnowHowFromMScale(scale));
                            }
                        }

                        return new Message()
                        {
                            Level = msgLevel,
                            Sender = "웨더아이",
                            Text = buffer.ToString(),
                        };
                    }
                    else
                    {
                        Console.Write('i');
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);


                Thread.Sleep(8000);

                m_client = new HttpClient();
            }


            return null;
        }

        private string GetTableContent(string html, string prefix, int beginIndex, out int endIndex)
        {
            if (beginIndex < 0)
            {
                endIndex = -1;

                return string.Empty;
            }


            int begin = html.IndexOf(prefix, beginIndex);

            if (begin >= 0)
            {
                begin = html.IndexOf("<td", begin + 1);
                begin = html.IndexOf(">", begin + 1);

                int end = html.IndexOf("<", begin + 1);

                if (begin >= 0 && end >= 0)
                {
                    endIndex = end;

                    string content = html.Substring(begin + 1, end - begin - 1);

                    return Util.ConvertHtmlToText(content).Trim();
                }
            }


            endIndex = -1;

            return string.Empty;
        }
    }
}
