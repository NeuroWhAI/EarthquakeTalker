using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using LinqToTwitter;

namespace EarthquakeTalker
{
    public class KMAEarthquakeFormatter : ITweetFormatter
    {
        public Message FormatTweet(Status tweet)
        {
            StringBuilder alarmText = new StringBuilder(tweet.Text);


            var msgLevel = Message.Priority.Normal;

            var koreaKeywords = new string[]
            {
                "경북", "경남", "경기", "전남", "전북", "제주", "서울", "충남", "충북",
                "북도", "남도", "광역시", "특별",
                "부산", "대구", "인천", "광주", "대전", "울산", "세종", "강원",
            };

            // 트윗 내용이 한국 행정구역 키워드를 포함하고 있으면
            if (koreaKeywords.Any((text) => tweet.Text.Contains(text)))
            {
                msgLevel = Message.Priority.High;


                Regex rgx = new Regex(@"규모\s?(\d{1,2}\.?\d*)");
                var match = rgx.Match(tweet.Text);
                if (match.Success)
                {
                    double scale = 0.0;
                    if (double.TryParse(match.Groups[1].ToString(), out scale))
                    {
                        alarmText.AppendLine();
                        alarmText.AppendLine();

                        alarmText.Append(Earthquake.GetKnowHowFromMScale(scale));
                    }
                }
            }


            return new Message()
            {
                Level = msgLevel,
                Text = $@"[기상청 지진정보서비스]
{alarmText.ToString()}",
            };
        }
    }
}
