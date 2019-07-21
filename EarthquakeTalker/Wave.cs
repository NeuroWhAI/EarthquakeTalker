using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace EarthquakeTalker
{
    public class Wave
    {
        public DateTime EventTimeUtc
        { get; set; }

        public bool IsAccel
        { get; set; } = true;

        public int Length
        { get; set; } = 0;

        public double MaxValue
        { get; set; } = 0;

        public IEnumerable<double> Buffer
        { get { return m_wave; } }

        public IEnumerable<double> TotalBuffer
        { get { return m_totalWave; } }

        public int BufferLength
        { get { return m_wave.Count; } }

        public double this[int index]
        { get { return m_wave[index]; } }

        public bool IsDanger
        { get; set; } = false;

        private List<double> m_wave = new List<double>();
        private List<double> m_totalWave = new List<double>();

        public void AddWave(double wave)
        {
            m_wave.Add(wave);
        }

        public void AddWave(IEnumerable<double> wave)
        {
            m_wave.AddRange(wave);
        }

        public void RemoveWave(int count)
        {
            m_wave.RemoveRange(0, count);
        }

        public void AddWaveToDraw(IEnumerable<double> wave)
        {
            m_totalWave.AddRange(wave);
        }

        public void DrawWave(string fileName, int width, int height, string stationInfo, double gain)
        {
            using (var bitmap = new Bitmap(width, height))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);


                // Draw informations.
                g.DrawString(stationInfo + "  " + EventTimeUtc.AddHours(9).ToString("s"),
                    SystemFonts.DefaultFont, Brushes.Black,
                    2, height - SystemFonts.DefaultFont.Height - 2);
                g.DrawString((IsAccel ? "PGA: " : "PGV: ") + MaxValue,
                    SystemFonts.DefaultFont, Brushes.Black, 516, 2);


                if (m_totalWave.Count >= 2)
                {
                    double widthScale = (double)width / (m_totalWave.Count - 1);
                    double heightScale = height * 0.5 * 0.88 / MaxValue;
                    int halfHeight = height / 2;

                    int i = 0;
                    float prevY = 0;

                    // Draw wave.
                    foreach (double data in m_totalWave)
                    {
                        float y = (float)(data / gain * heightScale);

                        g.DrawLine(Pens.Blue, (float)((i - 1) * widthScale), prevY + halfHeight,
                            (float)(i * widthScale), y + halfHeight);

                        prevY = y;
                        ++i;
                    }
                }


                g.Flush();

                bitmap.Save(fileName);
            } // using
        }
    }
}
