using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Net;
using System.IO;

namespace EarthquakeTalker
{
    class KmaNews : Worker
    {
        public KmaNews()
        {

        }

        //#############################################################################################

        protected string m_latestTitle = null;
        protected string m_latestEqk = null;
        protected DateTimeOffset m_notiTimeLimit;

        protected HttpClient m_client = new HttpClient();

        //#############################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(3.0);

            m_latestTitle = null;
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            
        }

        protected override Message OnWork(Action<Message> sender)
        {
            try
            {
                int index = -1;
                int endIndex = -1;

                var eqkList = m_client.GetStringAsync(@"https://www.weather.go.kr/w/wnuri-eqk-vol/rest/eqk/list.do?eqkType=d");
                string eqkListJson = eqkList.Result;

                if (eqkListJson.Contains("eqk_web"))
                {
                    index = eqkListJson.IndexOf("[지진속보]");

                    if (index >= 0)
                    {
                        index = eqkListJson.LastIndexOf('"', index);
                        endIndex = eqkListJson.IndexOf('"', index + 1);

                        if (index >= 0 && endIndex > index)
                        {
                            string eqkTitle = eqkListJson.Substring(index + 1, endIndex - index - 1).Trim();

                            if (m_latestTitle == null)
                            {
                                m_latestTitle = eqkTitle;
                                m_logger.PushLog($"테스트 출력\n\"{eqkTitle}\"");
                            }
                            else if (m_latestTitle != eqkTitle)
                            {
                                m_latestTitle = eqkTitle;
                                m_notiTimeLimit = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30.0);

                                // TODO: 가장 앞에 있는 정보를 가져오게 해두었는데 개선 필요함.
                                index = eqkListJson.IndexOf("eqk_web");
                                index = eqkListJson.LastIndexOf('"', index);
                                endIndex = eqkListJson.IndexOf('"', index + 1);

                                m_latestEqk = eqkListJson.Substring(index + 1, endIndex - index - 1);
                            }
                        }
                    }
                    else
                    {
                        m_latestTitle = string.Empty;
                    }

                    Console.Write('N');
                }


                index = -1;
                endIndex = -1;

                if (m_latestEqk != null && DateTimeOffset.UtcNow < m_notiTimeLimit)
                {
                    var eqkInfo = m_client.GetStringAsync(@"https://www.weather.go.kr/w/wnuri-eqk-vol/eqk/report.do?eqkType=a&eqk=" + m_latestEqk);
                    string eqkInfoHtml = eqkInfo.Result;


                    // 위에서 찾은 지진 속보에 해당하는 상세 페이지가 아니라면
                    // 파싱 중지.
                    if (!eqkInfoHtml.Contains(m_latestEqk.Replace("eqk_web_", "i_").Replace(".xml", ".png")))
                    {
                        m_latestEqk = null;
                        return null;
                    }


                    string time;
                    string scale;
                    string intensity;
                    string location;
                    string mapUrl;


                    index = eqkInfoHtml.IndexOf("발생시각");

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.IndexOf("<td", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.IndexOf(">", index + 1);

                    endIndex = eqkInfoHtml.IndexOf("</td", index + 1);

                    time = eqkInfoHtml.Substring(index + 1, endIndex - index - 1);

                    if (!time.Contains("년"))
                    {
                        return null;
                    }

                    time = Util.ConvertHtmlToText(time).Trim();


                    index = eqkInfoHtml.IndexOf("규모", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.IndexOf("<td", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.IndexOf(">", index + 1);

                    endIndex = eqkInfoHtml.IndexOf("</td", index + 1);

                    scale = eqkInfoHtml.Substring(index + 1, endIndex - index - 1);
                    scale = Util.ConvertHtmlToText(scale).Trim();

                    if (string.IsNullOrWhiteSpace(scale))
                    {
                        return null;
                    }


                    index = eqkInfoHtml.IndexOf("진도", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.IndexOf("<td", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.IndexOf(">", index + 1);

                    endIndex = eqkInfoHtml.IndexOf("</td", index + 1);

                    intensity = eqkInfoHtml.Substring(index + 1, endIndex - index - 1);
                    intensity = Util.ConvertHtmlToText(intensity).Trim();

                    if (string.IsNullOrWhiteSpace(intensity))
                    {
                        return null;
                    }


                    index = eqkInfoHtml.IndexOf("위치", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.IndexOf("<td", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.IndexOf(">", index + 1);

                    endIndex = eqkInfoHtml.IndexOf("</td", index + 1);

                    location = eqkInfoHtml.Substring(index + 1, endIndex - index - 1);
                    location = Util.ConvertHtmlToText(location).Trim();

                    if (string.IsNullOrWhiteSpace(location))
                    {
                        return null;
                    }


                    index = eqkInfoHtml.IndexOf("DATA/EQK/INTENSITY", index + 1);

                    if (index < 0)
                    {
                        return null;
                    }

                    index = eqkInfoHtml.LastIndexOf('"', index);
                    endIndex = eqkInfoHtml.IndexOf('"', index + 1);

                    mapUrl = eqkInfoHtml.Substring(index + 1, endIndex - index - 1);

                    endIndex = mapUrl.IndexOf(';');
                    if (endIndex >= 0)
                    {
                        mapUrl = mapUrl.Substring(0, endIndex);
                    }


                    // 속보 파싱 중지.
                    m_latestEqk = null;


                    var buffer = new StringBuilder();
                    buffer.AppendLine("⚠️ 지진속보가 발표되었습니다.");
                    buffer.AppendLine($"발생시각 : {time}");
                    buffer.AppendLine($"추정규모 : {scale}");
                    buffer.AppendLine($"예상진도 : {intensity}");
                    buffer.AppendLine($"추정위치 : {location}");
                    buffer.Append("수동으로 분석한 정보는 추가 발표될 예정입니다.");

                    sender(new Message()
                    {
                        Level = Message.Priority.Critical,
                        Sender = "기상청 지진속보",
                        Text = buffer.ToString(),
                    });


                    if (!string.IsNullOrWhiteSpace(mapUrl))
                    {
                        mapUrl = "http://www.weather.go.kr" + mapUrl;

                        sender(new Message()
                        {
                            Level = Message.Priority.Normal,
                            Sender = "기상청 시도별 진도 설명 이미지",
                            Text = mapUrl,
                        });
                    }
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


                if (m_client != null)
                {
                    m_client.Dispose();
                }

                Thread.Sleep(6000);


                m_client = new HttpClient();
            }


            return null;
        }
    }
}
