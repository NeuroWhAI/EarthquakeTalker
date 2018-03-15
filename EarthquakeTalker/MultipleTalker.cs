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

        public void AddTalker(Talker talker)
        {
            m_talkerList.Add(talker);
        }

        public void RemoveTalker(Talker talker)
        {
            m_talkerList.Remove(talker);
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
            var tasks = new Task[m_talkerList.Count];

            int index = 0;
            foreach (var talker in m_talkerList)
            {
                tasks[index] = Task.Factory.StartNew(() => talker.TalkAll());

                ++index;
            }

            Task.WaitAll(tasks);
        }
    }
}
