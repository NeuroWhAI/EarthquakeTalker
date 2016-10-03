using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using LinqToTwitter;

namespace EarthquakeTalker
{
    public abstract class TwitterWorker : Worker
    {
        public TwitterWorker()
        {

        }

        //#########################################################################################################

        protected TwitterContext m_twitterCtx = null;

        //#########################################################################################################

        protected void AuthorizeContext()
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
                Console.WriteLine("트위터 키 파일을 찾을 수 없습니다.");
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine("트위터 키 파일을 읽을 수 없습니다.");
            }

            var auth = new SingleUserAuthorizer
            {
                CredentialStore = credential,
            };
            auth.AuthorizeAsync().Wait();

            m_twitterCtx = new TwitterContext(auth);
        }
    }
}
