﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;

namespace EarthquakeTalkerController
{
    public class Graph
    {
        public Graph()
        {

        }

        //##############################################################################################

        public bool Visible
        { get; set; } = false;

        protected Queue<int> m_waveform = new Queue<int>();
        protected readonly object m_lockWaveform = new object();

        public string Name
        { get; set; } = "";

        public double Gain
        { get; set; } = 1.0;

        public double HeightScale
        { get; set; } = 10000.0;

        public int MaxLength
        { get; set; } = 3072;

        public double DangerValue
        { get; set; } = 0.0016;

        protected int m_tempMax = 0;

        protected int m_captureGage = 0;

        public string SavePath
        { get; set; } = string.Empty;

        private DateTime m_latestDataTime = DateTime.UtcNow;

        //##############################################################################################

        public void PushData(int data)
        {
            lock (m_lockWaveform)
            {
                m_waveform.Enqueue(data);
                if (m_waveform.Count > MaxLength)
                {
                    m_waveform.Dequeue();

                    if (m_captureGage > 0)
                        --m_captureGage;
                }
            }


            if (data / Gain > DangerValue / 2)
            {
                m_captureGage = this.MaxLength;
            }


            if (data > m_tempMax)
            {
                m_tempMax = data;
            }


            m_latestDataTime = DateTime.UtcNow;
        }

        public void Clear()
        {
            m_waveform.Clear();
        }

        public void ResetTempMax()
        {
            m_tempMax = 0;
        }

        //##############################################################################################

        public void Draw(Graphics g, Size size)
        {
            Bitmap bitmap = null;

            if (Visible == false)
            {
                if (m_captureGage > 0)
                {
                    bitmap = new Bitmap(size.Width, size.Height);
                    g = Graphics.FromImage(bitmap);
                }
                else
                {
                    return;
                }
            }


            g.Clear(Color.White);


            int[] copyWaveform = null;

            lock (m_lockWaveform)
            {
                copyWaveform = m_waveform.ToArray();
            }


            int maxData = (copyWaveform.Length > 0)
                ? copyWaveform.AsParallel().Max(Math.Abs)
                : 0;


            HeightScale = size.Height / 2 * 0.9 / Math.Max(maxData / Gain, DangerValue / 4);


            g.DrawString(Name + "    " + m_latestDataTime.ToString("s"), SystemFonts.DefaultFont, Brushes.Black,
                2, size.Height - SystemFonts.DefaultFont.Height - 2);
            g.DrawString("Gain: " + Gain, SystemFonts.DefaultFont, Brushes.Black, 2, 2);
            g.DrawString("Scale: " + HeightScale, SystemFonts.DefaultFont, Brushes.Black, 258, 2);
            g.DrawString("Max PGA or PGV: " + (m_tempMax / Gain),
                SystemFonts.DefaultFont, Brushes.Black, 258, 4 + SystemFonts.DefaultFont.Height);


            int halfHeight = size.Height / 2;

            float dangerY = (float)(DangerValue * HeightScale);
            g.DrawLine(Pens.Red, 0, halfHeight + dangerY,
                size.Width, halfHeight + dangerY);
            g.DrawLine(Pens.Red, 0, halfHeight - dangerY,
                size.Width, halfHeight - dangerY);
            

            if (copyWaveform.Length > 0)
            {
                double widthScale = (double)size.Width / copyWaveform.Length;

                int i = 0;
                float prevY = 0;

                foreach (var data in copyWaveform)
                {
                    float y = (float)(data / Gain * HeightScale);

                    g.DrawLine(Pens.Blue, (float)((i - 1) * widthScale), prevY + halfHeight,
                        (float)(i * widthScale), y + halfHeight);

                    prevY = y;
                    ++i;
                }
            }


            g.DrawString("Level: " + (maxData / Gain / DangerValue * 100.0) + "%",
                SystemFonts.DefaultFont, Brushes.Black, 516, 2);
            g.DrawString("PGA or PGV: " + (maxData / Gain),
                SystemFonts.DefaultFont, Brushes.Black, 516, 4 + SystemFonts.DefaultFont.Height);


            if (bitmap != null)
            {
                g.Dispose();
                g = null;

                var folderPath = Path.Combine(SavePath, Name);
                var folder = new DirectoryInfo(folderPath);

                Directory.CreateDirectory(folderPath);

                // 오래된 이미지 삭제.
                var imgs = folder.GetFiles();
                if (imgs.Length > 500)
                {
                    var oldestImg = imgs.OrderBy(info => info.CreationTime).First();
                    File.Delete(oldestImg.FullName);
                }
                

                string fileName = Path.Combine(SavePath, Name, DateTime.Now.ToString("yyyy_MM_dd HH_mm_ss") + ".bmp");
                if (File.Exists(fileName) == false)
                    bitmap.Save(fileName);

                bitmap.Dispose();
                bitmap = null;
            }
        }
    }
}
