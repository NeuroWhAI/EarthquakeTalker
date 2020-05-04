using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    class PewsStation
    {
        /// <summary>
        /// 경도
        /// </summary>
        public double Longitude { get; set; } = 0;

        /// <summary>
        /// 위도
        /// </summary>
        public double Latitude { get; set; } = 0;

        /// <summary>
        /// 최대 진도
        /// </summary>
        public int Mmi { get; private set; } = 0;

        /// <summary>
        /// 최대 진도
        /// </summary>
        public int MaxMmi { get; private set; } = 0;

        private DateTime m_mmiLife = DateTime.MinValue;

        public void UpdateMmi(int newMmi, TimeSpan lifetime)
        {
            if (newMmi > MaxMmi)
            {
                MaxMmi = newMmi;
            }

            if (newMmi > Mmi || DateTime.UtcNow >= m_mmiLife)
            {
                Mmi = newMmi;
                m_mmiLife = DateTime.UtcNow + lifetime;
            }
        }

        public void ResetMmi()
        {
            Mmi = 0;
            MaxMmi = 0;
            m_mmiLife = DateTime.MinValue;
        }
    }
}
