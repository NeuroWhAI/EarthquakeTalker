using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public class MultipleTalker
    {
        public MultipleTalker()
        {

        }

        //##############################################################################################################

        protected List<Talker> m_talkerList = new List<Talker>();

        //##############################################################################################################

        public void AddTalker(string talkerName, string roomName)
        {
            m_talkerList.Add(new Talker(roomName)
            {
                TalkerName = talkerName,
            });
        }

        public void RemoveTalker(string talkerName)
        {
            m_talkerList.RemoveAll(delegate (Talker talker)
            {
                return (talker.TalkerName == talkerName);
            });
        }

        public void RemoveTalkerIn(string roomName)
        {
            m_talkerList.RemoveAll(delegate (Talker talker)
            {
                return (talker.RoomName == roomName);
            });
        }

        public void Clear()
        {
            m_talkerList.Clear();
        }

        public void PushMessage(Message message)
        {
            foreach (var talker in m_talkerList)
            {
                talker.PushMessage(message);
            }
        }

        public void TalkAll()
        {
            foreach (var talker in m_talkerList)
            {
                talker.TalkAll();
            }
        }
    }
}
