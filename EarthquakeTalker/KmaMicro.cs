using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace EarthquakeTalker
{
    class KmaMicro : Worker
    {
        public KmaMicro()
        {

        }

        //#############################################################################################

        protected string m_latestNoti = string.Empty;

        protected HttpClient m_client = new HttpClient();

        //#############################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(10.0);
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
                var kmaNoti = m_client.GetByteArrayAsync(@"https://www.weather.go.kr/w/wnuri-eqk-vol/eqk/eqk-micro.do");

                kmaNoti.Wait();


                var byteArray = kmaNoti.Result.ToArray();
                var kmaNotiHtml = Encoding.UTF8.GetString(byteArray, 0, byteArray.Length);

                string noti = Util.ConvertHtmlToText(kmaNotiHtml).Trim();

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


                        var msg = new Message()
                        {
                            Level = Message.Priority.Normal,
                            Sender = "기상청 미소지진 안내",
                            Text = noti,
                        };


                        return msg;
                    }
                    else
                    {
                        Console.Write('`');
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
                    Console.WriteLine(exp.InnerException.Message);
                    Console.WriteLine(exp.InnerException.StackTrace);
                }


                if (m_client != null)
                {
                    m_client.Dispose();
                }

                Thread.Sleep(10000);


                m_client = new HttpClient();
            }


            return null;
        }
    }
}
