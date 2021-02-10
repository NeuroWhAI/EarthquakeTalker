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
        /// 일정 시간 동안의 최대 진도
        /// </summary>
        public int Mmi { get; private set; } = 0;

        /// <summary>
        /// 실시간 생(12~14는 I) 진도
        /// </summary>
        public int RawMmi { get; private set; } = 0;

        /// <summary>
        /// 최대 진도
        /// </summary>
        public int MaxMmi { get; private set; } = 0;

        private DateTime m_mmiLife = DateTime.MinValue;

        public void UpdateMmi(int newRawMmi, TimeSpan lifetime)
        {
            RawMmi = newRawMmi;

            int newMmi = newRawMmi;
            if (newMmi < 0)
            {
                // 음수일 수 없음.
                newMmi = 0;
                RawMmi = 0;
            }
            else if (newMmi > 11)
            {
                // 세분화 된 진도 I.
                newMmi = 1;
            }
            else if (newMmi > 10)
            {
                // 이도저도 아님.
                // PEWS 사이트에서는 진도 X에 해당하는 색을 의미하긴 함.
                newMmi = 10;
            }

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
            RawMmi = 0;
            MaxMmi = 0;
            m_mmiLife = DateTime.MinValue;
        }
    }
}
