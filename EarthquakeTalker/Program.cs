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
            string winstonIp = string.Empty;
            int winstonPort = -1;

            // Winston 지진계 정보 불러오기
            try
            {
                using (var sr = new StreamReader(new FileStream("winston.txt", FileMode.Open)))
                {
                    winstonIp = sr.ReadLine();
                    string temp = sr.ReadLine();
                    int.TryParse(temp, out winstonPort);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("winston.txt 파일을 찾을 수 없습니다.");
                return;
            }


            int msgServerPort = -1;

            // 메세지 서버 정보 불러오기
            try
            {
                using (var sr = new StreamReader(new FileStream("server.txt", FileMode.Open)))
                {
                    string temp = sr.ReadLine();
                    int.TryParse(temp, out msgServerPort);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("server.txt 파일을 찾을 수 없습니다.");
                return;
            }


            /// GUI 표준 입력 동기화 객체
            object lockControllerInput = new object();
#if DEBUG
            bool needController = File.Exists("../../../EarthquakeTalkerController/bin/Debug/EarthquakeTalkerController.exe");
#else
            bool needController = File.Exists("EarthquakeTalkerController.exe");
#endif

            /// GUI
            Process controller = null;

            if (needController)
            {
                controller = new Process();
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
            }


            /// 로거
            Logger logger = new Logger();

            /// 메세지 처리자
            MultipleTalker talker = new MultipleTalker();
#if DEBUG
            talker.AddTalker(new TelegramBot("@neurowhai_test_bot"));
#else
            talker.AddTalker(new TelegramBot("@neurowhai_earthquake_channel"));
#endif
            talker.AddTalker(new MessageServer(msgServerPort, "messages.dat"));

            /// 지진계
            List<Seismograph> seismographList = new List<Seismograph>();
            seismographList.Add(new SLinkSeismograph("slinktool.exe", "00BH1", "IU", "INCN", 3.352080e+09 / 100, "인천")
            { DangerValue = 0.01, DangerWaveTime = 0.01, MinDangerWaveTime = 0.005, IsAccel = false });
            seismographList.Add(new SLinkSeismograph("slinktool.exe", "BHZ", "JP", "JTU", 1.000000e+09 / 100, "대마도")
            { DangerValue = 0.07, DangerWaveTime = 0.01, MinDangerWaveTime = 0.005, IsAccel = false });
            seismographList.Add(new SLinkSeismograph("slinktool.exe", "BHZ", "KG", "TJN", 6.327240e+08 / 100, "대전")
            { DangerValue = 0.01, DangerWaveTime = 0.01, MinDangerWaveTime = 0.005, IsAccel = false });
            seismographList.Add(new WinstonSeismograph(winstonIp, winstonPort, "00", "EHZ", "AM", "R3E8F", 3.36e+08 / 100, "포항")
            { DangerValue = 0.03, DangerWaveTime = 0.5, MinDangerWaveTime = 0.2, IsAccel = false, Endian = true });

            /// 지진계를 포함한 메세지 생성자
            List<Worker> workerList = new List<Worker>();
            workerList.AddRange(seismographList);
            workerList.Add(new UserWatcher("KMA_earthquake", new KMAEarthquakeFormatter()));
            workerList.Add(new KmaHome());
            workerList.Add(new KmaMicro());
            workerList.Add(new IssueWatcher("지진", "지진+-동공+-일본+-원전+-http+-카메라+-ㅋㅋㅋ+-캠",
                triggerTime: TimeSpan.FromSeconds(30), maxStatusCount: 20, maxTextLength: 32));
            workerList.Add(new NecisEarlyWarning());
            workerList.Add(new KmaNews());

            int sensorIndex = 0;
            foreach (var sensor in seismographList)
            {
                // 지진계 인덱스 설정
                sensor.Index = sensorIndex;

                // GUI에 지진계 정보 전달
                if (controller != null)
                {
                    controller.StandardInput.WriteLine(sensor.Name + "|" + sensor.Gain + "|" + sensor.DangerValue);

                    sensor.WhenDataReceived += new Seismograph.SeismographDataReceivedEventHandler((index, waveform) =>
                    {
                        lock (lockControllerInput)
                        {
                            foreach (int data in waveform)
                            {
                                if (controller.HasExited == false)
                                    controller.StandardInput.WriteLine(index + "|" + data);
                            }
                        }
                    });
                }

                ++sensorIndex;
            }


            // 메세지 생성자 시작
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


            if (controller != null && controller.HasExited == false)
                controller.WaitForExit();

            foreach (var worker in workerList)
            {
                worker.Stop();
            }
        }
    }
}
