using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using LinqToTwitter;

namespace EarthquakeTalker
{
    public class UserWatcher : TwitterWorker
    {
        public UserWatcher(string userName = "NeuroWhAI", ITweetFormatter messageMaker = null)
        {
            this.UserName = userName;
            this.TweetFormatter = messageMaker;
        }

        //###########################################################################################################

        public string UserName
        { get; set; } = "";

        protected Status m_latestTweet = null;

        public ITweetFormatter TweetFormatter
        { get; set; } = null;

        //###########################################################################################################

        protected override void BeforeStart(MultipleTalker talker)
        {
            this.JobDelay = TimeSpan.FromSeconds(8.0);


            AuthorizeContext();
        }

        protected override void AfterStop(MultipleTalker talker)
        {
            m_latestTweet = null;

            m_twitterCtx = null;
        }

        protected override Message OnWork(Action<Message> sender)
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

                        m_logger.PushLog(firstTweet.Text);


                        if (TweetFormatter != null)
                        {
                            var msg = TweetFormatter.FormatTweet(firstTweet, sender);

                            msg.Sender = UserName + " 트위터";


                            return msg;
                        }
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


                Thread.Sleep(TimeSpan.FromSeconds(4));

                AuthorizeContext();
            }


            return null;
        }
    }
}
