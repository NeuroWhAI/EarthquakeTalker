using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public class Talker
    {
        public Talker(string roomName = "")
        {
            UpdateEditbox(roomName);
        }

        //################################################################################################

        public string TalkerName
        { get; set; } = string.Empty;

        protected IntPtr m_editBox = IntPtr.Zero;

        protected string m_roomName = string.Empty;
        public string RoomName
        {
            get { return m_roomName; }
            set
            {
                UpdateEditbox(value);
            }
        }

        protected Queue<Message> m_msgQueue = new Queue<Message>();
        protected readonly object m_lockMsgQueue = new object();

        //################################################################################################

        protected void UpdateEditbox(string roomName)
        {
            m_roomName = roomName;


            IntPtr room = WinApi.FindWindow(null, roomName);

            if (room != IntPtr.Zero)
            {
                m_editBox = WinApi.FindWindowEx(room, IntPtr.Zero, "RichEdit20W", null);
            }
        }

        //################################################################################################

        public void PushMessage(Message message)
        {
            lock (m_lockMsgQueue)
            {
                m_msgQueue.Enqueue(message.Clone() as Message);
            }
        }

        public bool TalkAll()
        {
            if (m_editBox != IntPtr.Zero)
            {
                lock (m_lockMsgQueue)
                {
                    foreach (var msg in m_msgQueue)
                    {
                        string msgText = msg.ToString();

                        if (string.IsNullOrWhiteSpace(TalkerName) == false)
                        {
                            msgText = $"@@ {TalkerName} @@\n\n" + msgText;
                        }

                        WinApi.SendMessage(m_editBox, 0x000c, IntPtr.Zero, msgText);
                        WinApi.PostMessage(m_editBox, 0x0100, new IntPtr(0xD), new IntPtr(0x1C001));

                        System.Threading.Thread.Sleep(200);
                    }


                    m_msgQueue.Clear();
                }


                return true;
            }
            else
            {
                UpdateEditbox(m_roomName);
            }


            return false;
        }
    }
}
