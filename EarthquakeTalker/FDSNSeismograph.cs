﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Threading;

namespace EarthquakeTalker
{
    public class FDSNSeismograph : Seismograph
    {
        public FDSNSeismograph(string seedViewerPath, string location, string channel, string network, string station, double gain, string name = "")
            : base(channel, network, station, gain, name)
        {
            SeedViewerPath = seedViewerPath;

            Location = location;
        }

        //###########################################################################################################
        
        public string SeedViewerPath
        { get; protected set; } = string.Empty;

        public string Location
        { get; set; } = "--";

        private StringBuilder m_buffer = new StringBuilder();
        private int m_leftSample = 0;

        private DateTime m_latestDownloadTime = DateTime.UtcNow;
        private DateTime m_latestCheckTime = DateTime.UtcNow;

        public TimeSpan DownloadInterval
        { get; set; } = TimeSpan.FromSeconds(8.0);

        public TimeSpan CheckInterval
        { get; set; } = TimeSpan.FromSeconds(5.0);
        
        private bool m_downloading = false;

        //###########################################################################################################

        protected void StartProcess(string seedDataFile)
        {
            using (var proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo()
                {
                    Arguments = "-D \"" + seedDataFile + "\"",
                    CreateNoWindow = true,
                    FileName = SeedViewerPath,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                };


                proc.Start();


                m_buffer.Clear();

                while (!proc.StandardOutput.EndOfStream)
                {
                    var outputLine = proc.StandardOutput.ReadLine();
                    OutputDataReceived(outputLine);
                }
            }
        }

        protected void StopProcess()
        {
            m_buffer.Clear();
            m_leftSample = 0;

            m_latestDownloadTime = DateTime.UtcNow;
            m_latestCheckTime = DateTime.UtcNow;
            
            m_downloading = false;
        }

        //###########################################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            base.BeforeStart(talker);
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            StopProcess();


            base.AfterStop(talker);
        }

        protected override Message OnWork()
        {
            var msg = base.OnWork();


            try
            {
                if (m_downloading == false && DateTime.UtcNow - m_latestCheckTime >= CheckInterval)
                {
                    var targetTime = m_latestDownloadTime + DownloadInterval;

                    if (DateTime.UtcNow >= targetTime)
                    {
                        m_downloading = true;


                        // 현재 시각과 차이가 너무 벌어지면 이전 데이터를 포기하고 다시 조정함.
                        if (DateTime.UtcNow - targetTime > TimeSpan.FromSeconds(60.0))
                        {
                            Console.Write('!');


                            m_latestDownloadTime = DateTime.UtcNow - TimeSpan.FromSeconds(60.0);
                            targetTime = m_latestDownloadTime + DownloadInterval;
                        }

                        string startTime = m_latestDownloadTime.ToString("s");
                        string endTime = targetTime.ToString("s");
                        string fileUri = $"http://service.iris.edu/fdsnws/dataselect/1/query?net={Network}&sta={Station}&loc={Location}&cha={Channel}&start={startTime}&end={endTime}";


                        Task.Factory.StartNew(() =>
                        {
                            string seedFile = Path.GetTempFileName() + ".mseed";


                            var wc = new WebClient();
                            wc.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                            try
                            {
                                wc.DownloadFile(fileUri, seedFile);
                            }
                            catch (Exception exp)
                            {
                                Console.WriteLine(exp.Message);
                                Console.WriteLine(exp.StackTrace);

                                Thread.Sleep(2000);
                            }


                            var seedFileInfo = new FileInfo(seedFile);
                            if (seedFileInfo.Exists && seedFileInfo.Length > 0)
                            {
                                m_latestDownloadTime = targetTime;

                                StartProcess(seedFile);
                            }


                            try
                            {
                                File.Delete(seedFile);
                            }
                            catch (Exception exp)
                            {
                                Console.WriteLine(exp.Message);
                                Console.WriteLine(exp.StackTrace);
                            }


                            m_downloading = false;
                        });
                    }


                    m_latestCheckTime = DateTime.UtcNow;
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);
            }


            return msg;
        }

        //###########################################################################################################

        private void OutputDataReceived(string outputLine)
        {
            m_buffer.Append(outputLine);
            string buf = m_buffer.ToString();


            if (m_leftSample <= 0)
            {
                Regex rgx = new Regex(@"([^\s]+),\s?([^,]+),\s?([^,]+),\s?([^,]+),\s?(\d+)\s?samples,\s?(\d+\.?\d*)\s?Hz,\s?([^\s]+)\s?");
                var m = rgx.Match(buf);
                if (m.Success)
                {
                    //Console.WriteLine("Location: " + m.Groups[1]); // string
                    //Console.WriteLine("Samples: " + m.Groups[5]); // int
                    //Console.WriteLine("Hz: " + m.Groups[6]); // double
                    //Console.WriteLine("Time: " + m.Groups[7]); // string


                    m_leftSample = int.Parse(m.Groups[5].ToString());
                    
                    double rate = 0;
                    double.TryParse(m.Groups[6].ToString(), out rate);

                    ReserveChunk(m.Groups[1].ToString(), m_leftSample, rate, m.Groups[7].ToString());


                    m_buffer.Remove(m.Index, m.Length);
                }
            }
            else
            {
                int beginIndex = -1, endIndex = 0;

                Regex rgx = new Regex(@"(-?\d+)\s+");
                var m = rgx.Match(buf);
                while (m.Success && m_leftSample > 0)
                {
                    int data = 0;
                    if (int.TryParse(m.Groups[1].ToString().Trim(), out data))
                    {
                        AppendSample(data);
                    }
                    else
                    {
                        m_leftSample = -1;
                        break;
                    }


                    if(beginIndex < 0)
                        beginIndex = m.Index;
                    endIndex = m.Index + m.Length;

                    m_leftSample--;

                    m = m.NextMatch();
                }

                if (beginIndex >= 0)
                {
                    m_buffer.Remove(beginIndex, endIndex - beginIndex);
                }
            }
        }
    }
}
