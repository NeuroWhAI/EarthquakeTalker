using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    break;
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp.Message);
                    Console.WriteLine(exp.StackTrace);


                    Talker talker = new Talker();
                    talker.PushMessage(new Message("지진봇이 예기치 못한 이유로 종료되었습니다.",
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
            workerList.Add(new Seismograph("slinktool.exe", "00BH1", "IU_INCN", 3.352080e+09 / 100, "인천"));
            workerList.Add(new Seismograph("slinktool.exe", "BHE", "JP_JTU", 1.000000e+09 / 100, "대마도"));
            workerList.Add(new Seismograph("slinktool.exe", "BHE", "KG_TJN", 6.327240e+08 / 100, "대전"));


            foreach (var worker in workerList)
            {
                worker.Start(talker, logger);
            }


            bool onRunning = true;

            var inputTask = Task.Factory.StartNew(new Action(() =>
            {
                while (true)
                {
                    var key = Console.ReadKey(true);

                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        onRunning = false;
                        break;
                    }

                    System.Threading.Thread.Sleep(1000);
                }
            }));

            while (onRunning)
            {
                talker.TalkAll();

                logger.LogAll();


                System.Threading.Thread.Sleep(100);
            }

            inputTask.Wait();


            foreach (var worker in workerList)
            {
                worker.Stop();
            }
        }
    }
}
