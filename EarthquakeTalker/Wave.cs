using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public class Wave
    {
        public int Length
        { get; set; } = 0;

        public double MaxPga
        { get; set; } = 0;

        public IEnumerable<double> Buffer
        { get { return m_wave; } }

        public int BufferLength
        { get { return m_wave.Count; } }

        public double this[int index]
        { get { return m_wave[index]; } }

        public bool IsDanger
        { get; set; } = false;

        private List<double> m_wave = new List<double>();

        public void AddWave(IEnumerable<double> wave)
        {
            m_wave.AddRange(wave);
        }

        public void RemoveWave(int count)
        {
            m_wave.RemoveRange(0, count);
        }
    }
}
