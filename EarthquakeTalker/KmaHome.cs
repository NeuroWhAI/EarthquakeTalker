using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;

namespace EarthquakeTalker
{
    public class KmaHome : Worker
    {
        public KmaHome()
        {

        }

        //#############################################################################################
        
        protected string m_latestNoti = string.Empty;

        protected HttpClient m_client = new HttpClient();

        //#############################################################################################

        protected override void BeforeStart(Talker talker)
        {
            
        }

        protected override void AfterStop(Talker talker)
        {
            m_latestNoti = string.Empty;

            m_client = null;
        }

        protected override Message OnWork()
        {
            try
            {
                var kmaNoti = m_client.GetByteArrayAsync(@"http://www.kma.go.kr/bangjae/bang.html");

                kmaNoti.Wait();


                var byteArray = kmaNoti.Result.ToArray();
                var encoding = Encoding.GetEncoding(51949/*euc-kr*/);
                var kmaNotiHtml = encoding.GetString(byteArray, 0, byteArray.Length);

                if (string.IsNullOrWhiteSpace(kmaNotiHtml) == false)
                {
                    int begin = kmaNotiHtml.IndexOf("</span><p>");

                    if (begin >= 0)
                    {
                        int end = kmaNotiHtml.IndexOf("</tr>", begin);

                        StringBuilder msgBdr = new StringBuilder(kmaNotiHtml.Substring(begin, end - begin));
                        msgBdr.Replace("</span><p>", "");
                        msgBdr.Replace("<br>", "\n");
                        msgBdr.Replace("</td>", "");
                        msgBdr.Replace("&nbsp;", " ");
                        msgBdr.Replace("&lt;", "<");
                        msgBdr.Replace("&gt;", ">");
                        msgBdr.Replace("&amp;", "&");
                        msgBdr.Replace("&quot;", "\"");

                        string noti = msgBdr.ToString().Trim();

                        if (noti.Contains("지진") || noti.Contains("여진"))
                        {
                            if (string.IsNullOrEmpty(m_latestNoti))
                            {
                                m_latestNoti = noti;


                                m_logger.PushLog($"테스트 출력\n{noti}");
                            }
                            else if (m_latestNoti != noti)
                            {
                                m_latestNoti = noti;

                                m_logger.PushLog(noti);


                                return new Message()
                                {
                                    Level = Message.Priority.High,
                                    Sender = "기상청 특보",
                                    Text = noti,
                                };
                            }
                            else
                            {
                                Console.Write(",");
                            }
                        }
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
            finally
            {
                Thread.Sleep(20000);
            }


            return null;
        }
    }
}
