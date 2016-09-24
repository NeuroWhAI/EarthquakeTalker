using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using LinqToTwitter;

namespace EarthquakeTalker
{
    public class TwitterWatcher : Worker
    {
        public TwitterWatcher(string userName = "NeuroWhAI")
        {
            this.UserName = userName;
        }

        //###########################################################################################################

        public string UserName
        { get; set; } = "";

        protected Status m_latestTweet = null;

        protected TwitterContext m_twitterCtx = null;

        //###########################################################################################################

        protected void CreateContext()
        {
            var credential = new SingleUserInMemoryCredentialStore();

            try
            {
                using (StreamReader sr = new StreamReader(new FileStream("twitterKey.txt", FileMode.Open)))
                {
                    credential.ConsumerKey = sr.ReadLine();
                    credential.ConsumerSecret = sr.ReadLine();
                    credential.AccessToken = sr.ReadLine();
                    credential.AccessTokenSecret = sr.ReadLine();


                    sr.Close();
                }
            }
            catch (FileNotFoundException)
            {
                m_logger.PushLog("트위터 키 파일을 찾을 수 없습니다.");
            }
            catch (EndOfStreamException)
            {
                m_logger.PushLog("트위터 키 파일을 읽을 수 없습니다.");
            }

            var auth = new SingleUserAuthorizer
            {
                CredentialStore = credential,
            };
            auth.AuthorizeAsync().Wait();

            m_twitterCtx = new TwitterContext(auth);
        }

        //###########################################################################################################

        protected override void BeforeStart(Talker talker)
        {
            CreateContext();
        }

        protected override void AfterStop(Talker talker)
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


                var firstTweet = statusTweets.First();

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
                        Console.Write(".");
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
                Console.WriteLine(exp.StackTrace);


                Thread.Sleep(10000);

                CreateContext();
            }
            finally
            {
                Thread.Sleep(20000);
            }


            return null;
        }
    }
}
