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

        public double MaxValue
        { get; set; } = 0;

        public IEnumerable<double> Buffer
        { get { return m_wave; } }

        public int BufferLength
        { get { return m_wave.Count; } }

        public double this[int index]
        { get { return m_wave[index]; } }

        public bool IsDanger
        { get; set; } = false;

        public double TotalValue
        { get; set; } = 0;

        private List<double> m_wave = new List<double>();

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
    }
}
