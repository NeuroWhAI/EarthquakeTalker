using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    /// <summary>
    /// FCM으로 보낼 데이터.
    /// string만 허용.
    /// </summary>
    class PewsJson
    {
#pragma warning disable IDE1006 // 명명 스타일
        public string time { get; set; }
        public string msg { get; set; }
        public string scale { get; set; }
        public string mmi { get; set; }
        public string grid { get; set; }
#pragma warning restore IDE1006 // 명명 스타일
    }
}
