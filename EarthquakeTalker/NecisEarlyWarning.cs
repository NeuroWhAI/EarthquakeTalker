using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace EarthquakeTalker
{
    public class NecisEarlyWarning : Worker
    {
        public NecisEarlyWarning()
        {

        }

        //#############################################################################################

        protected int m_latestCount = -1;

        protected HttpClient m_client = new HttpClient();

        //#############################################################################################

        private void ConvertAngle(double angle, out int degrees, out int minutes, out double seconds)
        {
            degrees = (int)Math.Floor(angle);
            double temp = (angle - degrees) * 60.0;
            minutes = (int)Math.Floor(temp);
            seconds = (temp - minutes) * 60.0;
        }

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(5.0);
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_latestCount = -1;

            m_client = null;
        }

        protected override Message OnWork(Action<Message> sender)
        {
            try
            {
                var korTime = DateTime.UtcNow + TimeSpan.FromHours(9); // KST

                var uri = new StringBuilder(@"http://necis.kma.go.kr/necis-dbf/usernl/earthquake/earthquakeForAlertList.do?selectDate=custom");
                uri.Append("&startDate=");
                uri.Append((korTime - TimeSpan.FromMinutes(1)).ToString("yyyy-MM-dd"));
                uri.Append("&endDate=");
                uri.Append(korTime.ToString("yyyy-MM-dd"));

                var task = m_client.GetByteArrayAsync(uri.ToString());

                task.Wait();


                var byteArray = task.Result.ToArray();
                var encoding = Encoding.GetEncoding(65001/*utf-8*/);
                var html = encoding.GetString(byteArray, 0, byteArray.Length);

                if (string.IsNullOrWhiteSpace(html) == false)
                {
                    int beginIndex = html.IndexOf("value", html.IndexOf("totCnt") + 1);
                    beginIndex = html.IndexOf("\"", beginIndex + 1);

                    int endIndex = html.IndexOf("\"", beginIndex + 1);

                    if (int.TryParse(html.Substring(beginIndex + 1, endIndex - beginIndex - 1), out int count))
                    {
                        if (m_latestCount < 0 || m_latestCount > count)
                        {
                            m_latestCount = count;
                        }


                        if (count > m_latestCount)
                        {
                            m_latestCount = count;


                            // Warning

                            string warningTime = "";
                            string lati = "";
                            string longi = "";
                            double magnitude = -1;
                            string location = "";

                            beginIndex = html.IndexOf("</thead>", beginIndex);

                            var matches = Regex.Matches(html.Substring(beginIndex + 1),
                                @"<td>(.*)<\/td>");

                            if (matches.Count >= 5)
                            {
                                warningTime = matches[1].Groups[1].Value;
                                lati = matches[2].Groups[1].Value;
                                longi = matches[3].Groups[1].Value;
                                double.TryParse(matches[4].Groups[1].Value, out magnitude);

                                var match = Regex.Match(html.Substring(matches[4].Index),
                                    "<td class=\".+\">(.*)<\\/td>");
                                if (match.Success)
                                {
                                    location = match.Groups[1].Value;
                                }
                            }


                            if (magnitude > 0 && magnitude < 13)
                            {
                                ConvertAngle(double.Parse(lati), out int latiD, out int latiM, out double latiS);
                                ConvertAngle(double.Parse(longi), out int longiD, out int longiM, out double longiS);

                                var mapLink = new StringBuilder("https://www.google.com/maps/place/");
                                mapLink.Append(latiD + "°" + latiM + "\'" + latiS + "%22N+");
                                mapLink.Append(longiD + "°" + longiM + "\'" + longiS + "%22E/@");
                                mapLink.Append(lati + ",");
                                mapLink.Append(longi + ",7z");

                                var msg = new StringBuilder();
                                msg.AppendLine(warningTime);
                                msg.AppendLine("지진조기경보가 발표되었습니다.");
                                msg.AppendLine("규모 : " + magnitude);
                                msg.AppendLine("지역 : " + location);
                                msg.AppendLine("진앙 : " + mapLink.ToString());
                                msg.AppendLine();
                                msg.AppendLine(Earthquake.GetKnowHowFromMScale(magnitude));

                                return new Message()
                                {
                                    Sender = "NECIS 지진조기경보",
                                    Level = Message.Priority.Critical,
                                    Text = msg.ToString(),
                                };
                            }
                        }


                        Console.Write("@");
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);


                Thread.Sleep(10000);

                m_client = new HttpClient();
            }


            return null;
        }
    }
}
