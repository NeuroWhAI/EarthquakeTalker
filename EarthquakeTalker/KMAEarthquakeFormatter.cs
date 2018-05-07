using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http;
using System.IO;
using LinqToTwitter;

namespace EarthquakeTalker
{
    public class KMAEarthquakeFormatter : ITweetFormatter
    {
        private void SendImage(object prm)
        {
            var sender = prm as Action<Message>;


            System.Threading.Thread.Sleep(TimeSpan.FromMinutes(2));


            for (int retry = 0; retry < 32; ++retry)
            {
                try
                {
                    HttpClient client = new HttpClient();

                    var task = client.GetByteArrayAsync(@"http://www.weather.go.kr/weather/earthquake_volcano/report.jsp");

                    task.Wait();


                    var byteArray = task.Result.ToArray();
                    var encoding = Encoding.GetEncoding(51949/*euc-kr*/);
                    var html = encoding.GetString(byteArray, 0, byteArray.Length);

                    if (string.IsNullOrWhiteSpace(html) == false)
                    {
                        int centerIndex = html.IndexOf("eqk_img");

                        if (centerIndex > 0)
                        {
                            int endIndex = html.IndexOf('\"', centerIndex, html.Length - centerIndex);

                            int beginIndex = centerIndex - 1;

                            while (html[beginIndex] != '\"')
                            {
                                --beginIndex;

                                if (beginIndex < 0)
                                    break;
                            }

                            if (beginIndex >= 0 && endIndex >= 0)
                            {
                                string imgUri = "http://www.weather.go.kr" + html.Substring(beginIndex + 1, endIndex - beginIndex - 1);

                                var wc = new WebClient();
                                wc.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                                string tempFileName = Path.GetTempFileName();
                                File.Delete(tempFileName);

                                try
                                {
                                    // 파일이 존재하는지 확인.
                                    wc.DownloadFile(imgUri, tempFileName);

                                    if (File.Exists(tempFileName))
                                    {
                                        File.Delete(tempFileName);


                                        // 진도 얻기.
                                        string intensity = string.Empty;

                                        centerIndex = html.IndexOf("진도");
                                        if (centerIndex >= 0)
                                        {
                                            beginIndex = html.IndexOf(">", centerIndex + 16) + 1;
                                            endIndex = html.IndexOf("</td", beginIndex);

                                            intensity = html.Substring(beginIndex, endIndex - beginIndex);

                                            intensity = Util.RemoveHtmlTag(intensity);

                                            intensity = WebUtility.HtmlDecode(intensity);
                                        }


                                        // 메세지 전송.
                                        sender(new Message()
                                        {
                                            Level = Message.Priority.Normal,
                                            Sender = (string.IsNullOrEmpty(intensity) ? "기상청 지진 통보문" : intensity),
                                            Text = imgUri,
                                        });


                                        return;
                                    }
                                }
                                catch
                                {
                                    Console.Write('^');
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


                System.Threading.Thread.Sleep(8000);
            }
        }

        public Message FormatTweet(Status tweet, Action<Message> sender)
        {
            StringBuilder alarmText = new StringBuilder(tweet.Text);


            var msgLevel = Message.Priority.Normal;

            var koreaKeywords = new string[]
            {
                "경북", "경남", "경기", "전남", "전북", "제주", "서울", "충남", "충북",
                "북도", "남도", "광역시", "특별",
                "부산", "대구", "인천", "광주", "대전", "울산", "세종", "강원",
            };

            // 트윗 내용이 한국 행정구역 키워드를 포함하고 있으면
            if (koreaKeywords.Any((text) => tweet.Text.Contains(text)))
            {
                msgLevel = Message.Priority.High;


                double scale = 0.0;

                if (tweet.Text.Contains("추정규모"))
                {
                    Regex rgx = new Regex(@"규모\s*:\s*(\d{1,2}\.?\d*)");
                    var match = rgx.Match(tweet.Text);
                    if (match.Success)
                    {
                       double.TryParse(match.Groups[1].ToString(), out scale);
                    }
                }
                else
                {
                    Regex rgx = new Regex(@"규모\s?(\d{1,2}\.?\d*)");
                    var match = rgx.Match(tweet.Text);
                    if (match.Success)
                    {
                        double.TryParse(match.Groups[1].ToString(), out scale);
                    }


                    Task.Factory.StartNew(SendImage, sender);
                }


                if (scale > 0)
                {
                    alarmText.AppendLine();
                    alarmText.AppendLine();

                    alarmText.Append(Earthquake.GetKnowHowFromMScale(scale));
                }
            }


            return new Message()
            {
                Level = msgLevel,
                Text = $@"[기상청 지진정보서비스]
{alarmText.ToString()}",
            };
        }
    }
}
