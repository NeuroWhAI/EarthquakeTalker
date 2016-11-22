using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public class KakaoTalker : Talker
    {
        public KakaoTalker(string roomName = "", string talkerName = null)
        {
            this.TalkerName = talkerName;
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

        protected override bool Talk(Message message)
        {
            if (m_editBox != IntPtr.Zero)
            {
                string msgText = message.ToString();

                if (string.IsNullOrWhiteSpace(TalkerName) == false)
                {
                    msgText = $"@@ {TalkerName} @@\n\n" + msgText;
                }

                WinApi.SendMessage(m_editBox, 0x000c, IntPtr.Zero, msgText);
                System.Threading.Thread.Sleep(1000);
                WinApi.PostMessage(m_editBox, 0x0100, new IntPtr(0xD), new IntPtr(0x1C001));
                System.Threading.Thread.Sleep(1000);


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
