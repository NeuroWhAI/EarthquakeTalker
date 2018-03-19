using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace EarthquakeTalker
{
    public class MessageServer : Talker
    {
        public MessageServer(int port, string savefile)
        {
            this.SavePath = savefile;

            LoadMessages();


            m_server = new TcpListener(IPAddress.Any, port);
            m_server.Start();

            Task.Factory.StartNew(Listen);
        }

        //################################################################################################

        public string SavePath
        { get; set; }

        /// <summary>
        /// 메세지 캐시의 최대 크기.
        /// </summary>
        public int QueueSize
        { get; set; } = 32;

        /// <summary>
        /// 메세지 캐시.
        /// 인덱스가 클수록 최신 메세지라고 보면 됨.
        /// </summary>
        private List<Message> m_msgList = new List<Message>();
        private readonly object m_lockMsgList = new object();

        private TcpListener m_server = null;

        //################################################################################################

        private void SaveMessages()
        {
            lock (m_lockMsgList)
            {
                using (var bw = new BinaryWriter(new FileStream(this.SavePath, FileMode.Create)))
                {
                    bw.Write(m_msgList.Count);

                    foreach (var msg in m_msgList)
                    {
                        msg.WriteToStream(bw);
                    }


                    bw.Close();
                }
            }
        }

        private void LoadMessages()
        {
            if (File.Exists(this.SavePath))
            {
                m_msgList.Clear();


                using (var br = new BinaryReader(new FileStream(this.SavePath, FileMode.Open)))
                {
                    int count = br.ReadInt32();

                    for (int i = 0; i < count; ++i)
                    {
                        var msg = new Message();
                        msg.ReadFromStream(br);

                        m_msgList.Add(msg);
                    }


                    br.Close();
                }


                Console.WriteLine();
                Console.WriteLine("Load {0} messages.", m_msgList.Count);
                Console.WriteLine();
            }
        }

        private void Listen()
        {
            while (m_server != null)
            {
                using (var client = m_server.AcceptTcpClient())
                using (var stream = client.GetStream())
                {
                    try
                    {
                        ProceedProtocol(stream);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                    }


                    stream.Close();
                    client.Close();
                }
            }
        }

        private void ProceedProtocol(NetworkStream stream)
        {
            string ident = ReadStringFromStream(stream);

            if (ident != "neurowhai")
            {
                return;
            }


            Message msg = null;


            string command = ReadStringFromStream(stream);

            if (command == "recent")
            {
                string offset = ReadStringFromStream(stream);

                msg = GetRecentMessage(int.Parse(offset));
            }
            else if (command == "after")
            {
                string guid = ReadStringFromStream(stream);

                msg = GetMessageAfter(Guid.Parse(guid));
            }
            else
            {
                return;
            }


            if (msg == null)
            {
                SendString(stream, "nope");
            }
            else
            {
                SendString(stream, "msg");

                SendString(stream, msg.Id.ToString());
                SendString(stream, msg.CreationTime.ToBinary().ToString());
                SendString(stream, ((int)msg.Level).ToString());
                SendString(stream, msg.Sender.ToString());
                SendString(stream, msg.Text.ToString());
            }
        }

        private void SendString(NetworkStream stream, string str)
        {
            var strBuffer = Encoding.UTF8.GetBytes(str);
            var lenBuffer = BitConverter.GetBytes(strBuffer.Length);


            stream.Write(lenBuffer, 0, lenBuffer.Length);
            stream.Write(strBuffer, 0, strBuffer.Length);
        }

        private string ReadStringFromStream(NetworkStream stream)
        {
            int num = 0;
            byte[] buffer = new byte[4];
            Task<int> task = null;


            task = stream.ReadAsync(buffer, 0, buffer.Length);
            task.Wait(5_000);

            if (task.IsCompleted == false || task.Result <= 0)
            {
                throw new Exception("Connection reset.");
            }

            num = BitConverter.ToInt32(buffer, 0);

            if (num <= 0 || num > 65535)
            {
                throw new Exception(num + " is invalid.");
            }


            buffer = new byte[num];


            task = stream.ReadAsync(buffer, 0, buffer.Length);
            task.Wait(10_000);

            if (task.IsCompleted == false || task.Result <= 0)
            {
                throw new Exception("Connection reset.");
            }

            return Encoding.UTF8.GetString(buffer);
        }

        protected override bool Talk(Message message)
        {
            lock (m_lockMsgList)
            {
                m_msgList.Add(message);

                if (m_msgList.Count > this.QueueSize)
                {
                    m_msgList.RemoveAt(0);
                }
            }


            SaveMessages();


            return true;
        }

        private Message GetRecentMessage(int offset)
        {
            lock (m_lockMsgList)
            {
                if (offset >= 0 && m_msgList.Count > offset)
                {
                    return m_msgList[m_msgList.Count - 1 - offset];
                }
            }


            return null;
        }

        private Message GetMessageAfter(Guid guid)
        {
            lock (m_lockMsgList)
            {
                int index = m_msgList.FindLastIndex((msg) => msg.Id == guid);

                if (index + 1 < m_msgList.Count)
                {
                    return m_msgList[index + 1];
                }


                // 못찾았으면 가장 오래된 메세지를 반환.
                if (index < 0 && m_msgList.Count > 0)
                {
                    return m_msgList[0];
                }
            }


            return null;
        }
    }
}
