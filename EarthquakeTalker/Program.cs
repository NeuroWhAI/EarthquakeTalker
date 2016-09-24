using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using LinqToTwitter;

namespace EarthquakeTalker
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 0; i < 3; ++i)
            {
                try
                {
                    Work();
                }
                catch (Exception)
                {
                    Talker talker = new Talker();
                    talker.PushMessage(new Message("[기상청 지진정보서비스]가 예기치 못한 오류로 종료되었습니다.",
                        "지진봇",
                        Message.Priority.Critical));
                    talker.TalkAll();
                }


                System.Threading.Thread.Sleep(8000);
            }
        }

        static void Work()
        {
            Logger logger = new Logger();

            Talker talker = new Talker("■전국 지진 정보 공유소■");
            talker.TalkerName = "지진봇알림";

            List<Worker> workerList = new List<Worker>();
            workerList.Add(new TwitterWatcher("KMA_earthquake"));
            workerList.Add(new KmaHome());


            foreach (var worker in workerList)
            {
                worker.Start(talker, logger);
            }

            while (true)
            {
                talker.TalkAll();

                logger.LogAll();
            }

            foreach (var worker in workerList)
            {
                worker.Stop();
            }
        }
    }
}
