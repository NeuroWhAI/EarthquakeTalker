using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public class EarthquakeKnowHow
    {
        public static string GetKnowHow(double richterMScale)
        {
            if (richterMScale < 0.0)
                return "";


            StringBuilder str = new StringBuilder("");
            str.AppendLine($"[국내 규모{richterMScale.ToString("F1")} 지진 발생시 행동요령]");


            if (richterMScale < 3.0)
            {
                // 민감한 사람은 느낄 수 있음.

                str.AppendLine("비교적 좁은 범위에서 민감한 사람이 진동을 감지할 수 있습니다.");
                str.AppendLine("우려되는 피해는 없으며 침착하시고 소식에 귀를 기울여주시기 바랍니다.");
            }
            else if (richterMScale < 4.0)
            {
                // 좁은 범위에서 많은 사람이 느끼고 사물이 움직일 수 있음.

                str.AppendLine("넓은 범위에서 많은 사람들이 진동을 감지할 수 있습니다.");
                str.AppendLine("떨어지기 쉬운 물건을 정비하시고 어려울경우 떨어져 계셔야 합니다.");
                str.AppendLine("크게 우려되는 피해는 없으며 소식에 귀를 기울여주시기 바랍니다.");
            }
            else if (richterMScale < 5.0)
            {
                // 넓은 범위에서 많은 사람이 느끼고 낮은 확률로 피해가 발생할 수 있음.

                str.AppendLine("넓은 범위에서 대부분의 사람들이 진동을 감지할 수 있으며");
                str.AppendLine("비교적 좁은 범위에서 강한 진동이 감지됩니다.");
                str.AppendLine("물건이 떨어질 수 있으므로 조심하시고");
                str.AppendLine("소식에 귀를 기울여주시기 바랍니다.");
            }
            else if (richterMScale < 6.0)
            {
                // 매우 넓은 범위에서 많은 사람이 느끼고 매우 높은 확률로 피해가 발생함.

                str.AppendLine("넓은 범위에서 강한 진동이 감지되며");
                str.AppendLine("비교적 좁은 범위에서 큰 피해가 발생할 수 있습니다.");
                str.AppendLine("전기/가스를 끄고 문을 열어두어 몸을 보호할 수 있는 곳에 숨어있다가");
                str.AppendLine("진동이 잦아들면 머리를 보호하며 바깥의 넓은 곳으로 대피하시기 바랍니다.");
                str.AppendLine("크게 규모 3~4의 여진이 뒤따를 수 있으니 주의하시기 바랍니다.");
                str.AppendLine("더 큰 지진의 전진일 수 있으므로 안전한 곳으로 대피하시기를 권장합니다.");
            }
            else if (richterMScale < 7.0)
            {
                // 큰 피해가 발생하며 원자력 발전소를 걱정할 정도.

                str.AppendLine("넓은 범위에서 강한 진동과 함께 순간적으로 큰 피해가 발생할 수 있습니다.");
                str.AppendLine("전기/가스를 끄고 문을 열어두어 몸을 보호할 수 있는 곳에 숨어있다가");
                str.AppendLine("진동이 잦아들면 머리를 보호하며 바깥의 넓은 곳으로 대피하시기 바랍니다.");
                str.AppendLine("산사태가 발생할 수 있으니 주의하시길 바랍니다.");
                str.AppendLine("크게 규모 4~5의 여진이 뒤따를 수 있으니 주의하시기 바랍니다.");
            }
            else
            {
                // 매우 큰 지진.

                str.AppendLine("매우 넓은 범위에서 강한 진동과 함께 괴멸적인 피해가 발생할 수 있습니다.");
                str.AppendLine("전기/가스를 끄고 문을 열어두어 몸을 보호할 수 있는 곳에 숨어있다가");
                str.AppendLine("진동이 잦아들면 머리를 보호하며 바깥의 넓은 곳으로 대피하시기 바랍니다.");
                str.AppendLine("산사태가 발생할 수 있으니 주의하시길 바랍니다.");
                str.AppendLine("큰 여진이 뒤따를 수 있으니 주의하시기 바랍니다.");
                str.AppendLine("해안 지진의 경우 해일이 발생할 수 있으므로 높은 곳으로 이동하시기 바랍니다.");
            }


            return str.ToString().TrimEnd();
        }
    }
}
