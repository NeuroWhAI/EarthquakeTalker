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
        public FCMServer(string projectName, string keyFileName)
        {
            Endpoint = $"https://fcm.googleapis.com/v1/projects/{projectName}/messages:send";

            using (var stream = new FileStream(keyFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                m_credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(new[] { "https://www.googleapis.com/auth/firebase.messaging" })
                    .UnderlyingCredential as ServiceAccountCredential;
            }
        }

        private readonly string Endpoint;

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
            // 토큰을 얻으려고 시도할 때 갱신이 필요하면 알아서 해주지만
            // 미리 해서 메시지 보낼 때 지연을 줄이기 위한 목적.

            while (m_running)
            {
                try
                {
                    bool expired;
                    lock (m_syncCredential)
                    {
                        expired = m_credential.Token?.IsExpired(m_credential.Clock) ?? true;
                    }

                    if (expired)
                    {
                        lock (m_syncCredential)
                        {
                            m_credential.GetAccessTokenForRequestAsync().GetAwaiter().GetResult();
                        }

                        Console.WriteLine();
                        Console.WriteLine("Token renewed.");
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
