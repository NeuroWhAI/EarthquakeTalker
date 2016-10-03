﻿using System;
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
                }


                System.Threading.Thread.Sleep(8000);
            }
        }

        static void Work()
        {
            Logger logger = new Logger();

            MultipleTalker talker = new MultipleTalker();
            talker.AddTalker(new TelegramBot("neurowhai_earthquake_channel"));
            talker.AddTalker(new KakaoTalker("■전국 지진 정보 공유소■", "지진봇알림"));
            talker.AddTalker(new KakaoTalker("지진 친목방", "지진봇알림"));

            List<Worker> workerList = new List<Worker>();
            workerList.Add(new UserWatcher("KMA_earthquake"));
            workerList.Add(new KmaHome());
            workerList.Add(new Seismograph("slinktool.exe", "00BH1", "IU_INCN", 3.352080e+09 / 100, "인천"));
            workerList.Add(new Seismograph("slinktool.exe", "BHE", "JP_JTU", 1.000000e+09 / 100, "대마도"));
            workerList.Add(new Seismograph("slinktool.exe", "BHE", "KG_TJN", 6.327240e+08 / 100, "대전"));
            workerList.Add(new IssueWatcher("지진", "지진+-동공+-일본+-원전", TimeSpan.FromSeconds(30.0), 24));


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
