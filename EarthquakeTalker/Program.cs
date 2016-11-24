using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace EarthquakeTalker
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
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
                

                // 비정상 종료
                

                try
                {
                    List<Process> targetProcesses = new List<Process>();
                    targetProcesses.AddRange(Process.GetProcessesByName("EarthquakeTalkerController"));
                    targetProcesses.AddRange(Process.GetProcessesByName("slinktool"));

                    foreach (var proc in targetProcesses)
                    {
                        proc.CloseMainWindow();

                        Thread.Sleep(3000);

                        if (proc.HasExited == false)
                            proc.Kill();

                        proc.Dispose();
                    }
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp.Message);
                    Console.WriteLine(exp.StackTrace);
                }


                GC.Collect();


                Thread.Sleep(8000);
            }
        }

        static void Work()
        {
            /// 카카오톡 방 제목
            List<string> kakaoRooms = GetKakaoRoomList("kakao.txt");


            /// GUI 표준 입력 동기화 객체
            object lockControllerInput = new object();

            /// GUI
            Process controller = new Process();
            controller.StartInfo = new ProcessStartInfo()
            {
#if DEBUG
                FileName = "../../../EarthquakeTalkerController/bin/Debug/EarthquakeTalkerController.exe",
#else
                FileName = "EarthquakeTalkerController.exe",
#endif
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            controller.Start();


            /// 로거
            Logger logger = new Logger();

            /// 메세지 처리자
            MultipleTalker talker = new MultipleTalker();
            talker.AddTalker(new TelegramBot("neurowhai_earthquake_channel"));
            foreach (string room in kakaoRooms)
            {
                talker.AddTalker(new KakaoTalker(room, "지진봇알림"));
            }

            /// 지진계
            List<Seismograph> seismographList = new List<Seismograph>();
            seismographList.Add(new Seismograph("slinktool.exe", "00BH1", "IU_INCN", 3.352080e+09 / 100, "인천"));
            seismographList.Add(new Seismograph("slinktool.exe", "BHE", "JP_JTU", 1.000000e+09 / 100, "대마도"));
            seismographList.Add(new Seismograph("slinktool.exe", "BHE", "KG_TJN", 3.352080e+09 / 100, "대전"));

            /// 지진계를 포함한 메세지 생성자
            List<Worker> workerList = new List<Worker>();
            workerList.Add(new UserWatcher("KMA_earthquake", new KMAEarthquakeFormatter()));
            workerList.Add(new KmaHome());
            workerList.AddRange(seismographList);
            workerList.Add(new IssueWatcher("지진", "지진+-동공+-일본+-원전+-http", TimeSpan.FromSeconds(25.0), 30));


            int sensorIndex = 0;
            foreach (var sensor in seismographList)
            {
                sensor.Index = sensorIndex;

                controller.StandardInput.WriteLine(sensor.Name + "|" + sensor.Gain + "|" + sensor.DangerPga);

                sensor.WhenDataReceived += new Seismograph.SeismographDataReceivedEventHandler((index, waveform) =>
                {
                    lock (lockControllerInput)
                    {
                        foreach (int data in waveform)
                        {
                            if (controller.HasExited == false)
                                controller.StandardInput.WriteLine(index + " " + data);
                        }
                    }
                });

                ++sensorIndex;
            }


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

                    Thread.Sleep(1000);
                }
            }));

            while (onRunning)
            {
                talker.TalkAll();

                logger.LogAll();


                Thread.Sleep(100);
            }

            inputTask.Wait();


            if (controller.HasExited == false)
                controller.WaitForExit();

            foreach (var worker in workerList)
            {
                worker.Stop();
            }
        }

        static List<string> GetKakaoRoomList(string fileName)
        {
            List<string> kakaoRooms = new List<string>();

            using (StreamReader sr = new StreamReader(new FileStream(fileName, FileMode.Open)))
            {
                while (!sr.EndOfStream)
                {
                    string room = sr.ReadLine().Trim();
                    if (string.IsNullOrWhiteSpace(room) == false)
                    {
                        kakaoRooms.Add(room);
                    }
                }

                sr.Close();
            }


            return kakaoRooms;
        }
    }
}
