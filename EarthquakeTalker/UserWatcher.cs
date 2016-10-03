using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;
using LinqToTwitter;

namespace EarthquakeTalker
{
    public class UserWatcher : TwitterWorker
    {
        public UserWatcher(string userName = "NeuroWhAI")
        {
            this.UserName = userName;
        }

        //###########################################################################################################

        public string UserName
        { get; set; } = "";

        protected Status m_latestTweet = null;

        //###########################################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(20.0);


            AuthorizeContext();
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_latestTweet = null;

            m_twitterCtx = null;
        }

        protected override Message OnWork()
        {
            try
            {
                var statusTweets =
                    from tweet in m_twitterCtx.Status
                    where tweet.Type == StatusType.User && tweet.ScreenName == this.UserName
                    select tweet;


                var firstTweet = statusTweets.FirstOrDefault();

                if (firstTweet != null)
                {
                    if (m_latestTweet == null)
                    {
                        m_latestTweet = firstTweet;

                        m_logger.PushLog($@"테스트 출력
{firstTweet.Text}
$");
                    }
                    else if (m_latestTweet.CreatedAt < firstTweet.CreatedAt)
                    {
                        m_latestTweet = firstTweet;


                        StringBuilder alarmText = new StringBuilder(firstTweet.Text);

                        Regex rgx = new Regex(@"규모\s?(\d{1,2}\.?\d*)");
                        var match = rgx.Match(firstTweet.Text);
                        if (match.Success)
                        {
                            double scale = 0.0;
                            if (double.TryParse(match.Groups[1].ToString(), out scale))
                            {
                                alarmText.AppendLine();
                                alarmText.AppendLine();

                                alarmText.Append(EarthquakeKnowHow.GetKnowHow(scale));
                            }
                        }


                        return new Message()
                        {
                            Level = Message.Priority.Critical,
                            Sender = UserName + " 트위터",
                            Text = $@"[기상청 지진정보서비스]
{alarmText.ToString()}",
                        };
                    }
                    else
                    {
                        Console.Write('.');
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);


                Thread.Sleep(10000);

                AuthorizeContext();
            }


            return null;
        }
    }
}
