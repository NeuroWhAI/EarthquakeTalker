using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

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
        { get; set; } = 2048;

        public double DangerPga
        { get; set; } = 0.0028;

        //##############################################################################################

        public void PushData(int data)
        {
            lock (m_lockWaveform)
            {
                m_waveform.Enqueue(data);
                if (m_waveform.Count > MaxLength)
                    m_waveform.Dequeue();
            }
        }

        public void Clear()
        {
            m_waveform.Clear();
        }

        //##############################################################################################

        public void Draw(Graphics g, Size size)
        {
            if (Visible == false)
                return;


            g.DrawString(Name, SystemFonts.DefaultFont, Brushes.Black,
                2, size.Height - SystemFonts.DefaultFont.Height - 2);
            g.DrawString("Gain: " + Gain, SystemFonts.DefaultFont, Brushes.Black, 2, 2);
            g.DrawString("Scale: " + HeightScale, SystemFonts.DefaultFont, Brushes.Black, 258, 2);

            int maxData = 0;


            int halfHeight = size.Height / 2;

            float dangerY = (float)(DangerPga * HeightScale);
            g.DrawLine(Pens.Red, 0, halfHeight + dangerY,
                size.Width, halfHeight + dangerY);
            g.DrawLine(Pens.Red, 0, halfHeight - dangerY,
                size.Width, halfHeight - dangerY);

            lock (m_lockWaveform)
            {
                if (m_waveform.Count > 0)
                {
                    double widthScale = (double)size.Width / m_waveform.Count;

                    int i = 0;
                    float prevY = 0;

                    foreach (var data in m_waveform)
                    {
                        if (Math.Abs(data) > maxData)
                            maxData = Math.Abs(data);


                        float y = (float)(data / Gain * HeightScale);

                        g.DrawLine(Pens.Blue, (float)((i - 1) * widthScale), prevY + halfHeight,
                            (float)(i * widthScale), y + halfHeight);

                        prevY = y;
                        ++i;
                    }
                }
            }


            g.DrawString("Level: " + (maxData / Gain / DangerPga * 100.0) + "%",
                SystemFonts.DefaultFont, Brushes.Black, 516, 2);
            g.DrawString("PGA: " + (maxData / Gain) + "g",
                SystemFonts.DefaultFont, Brushes.Black, 516, 4 + SystemFonts.DefaultFont.Height);
        }
    }
}
