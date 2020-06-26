using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Net;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;

namespace EarthquakeTalker
{
    class FCMServer
    {
        public FCMServer(string keyFileName)
        {
            using (var stream = new FileStream(keyFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                m_credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(new[] { "https://www.googleapis.com/auth/firebase.messaging" })
                    .UnderlyingCredential as ServiceAccountCredential;
            }
        }

        private readonly string Endpoint = "https://fcm.googleapis.com/v1/projects/neurowhai-pews/messages:send";

        private ServiceAccountCredential m_credential;
        private Thread m_task = null;
        private bool m_running = false;
        private readonly object m_syncCredential = new object();

        public void Start()
        {
            Stop();

            m_task = new Thread(new ThreadStart(UpdateToken));
            m_running = true;
            m_task.Start();
        }

        public void Stop()
        {
            if (m_task != null)
            {
                m_running = false;
                m_task.Join();
                m_task = null;
            }
        }

        public void SendData(object jsonDataObj, string topic, int ttlInSeconds)
        {
            string msg = JsonConvert.SerializeObject(new
            {
                message = new
                {
                    topic,
                    data = jsonDataObj,
                    android = new
                    {
                        priority = "high",
                        ttl = $"{ttlInSeconds}s",
                    },
                },
            });
            var msgBytes = Encoding.UTF8.GetBytes(msg);

            string token;
            lock (m_syncCredential)
            {
                token = m_credential.GetAccessTokenForRequestAsync().GetAwaiter().GetResult();
            }

            var request = WebRequest.Create(Endpoint);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = msgBytes.Length;
            request.Headers.Add(HttpRequestHeader.Authorization, $"Bearer {token}");

            using (var stream = request.GetRequestStream())
            {
                stream.Write(msgBytes, 0, msgBytes.Length);
            }

            using (var res = request.GetResponse())
            {
                return;
            }
        }

        private void UpdateToken()
        {
            var nextTime = DateTime.MinValue;
            string prevToken = string.Empty;

            while (m_running)
            {
                try
                {
                    if (DateTime.UtcNow >= nextTime)
                    {
                        string token;
                        long expiresInSeconds;
                        lock (m_syncCredential)
                        {
                            token = m_credential.GetAccessTokenForRequestAsync().GetAwaiter().GetResult();
                            expiresInSeconds = m_credential.Token.ExpiresInSeconds ?? 3600;
                        }

                        if (token != prevToken)
                        {
                            prevToken = token;

                            var expireTime = TimeSpan.FromSeconds(expiresInSeconds);
                            nextTime = DateTime.UtcNow + expireTime - TimeSpan.FromMinutes(1);
                        }
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine(err.Message);
                    Console.WriteLine(err.StackTrace);
                }

                Thread.Sleep(5000);
            }
        }
    }
}
