using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public abstract class Talker
    {
        public Talker()
        {
            
        }

        //################################################################################################
    
        private Queue<Message> m_msgQueue = new Queue<Message>();
        private readonly object m_lockMsgQueue = new object();

        //################################################################################################

        public void PushMessage(Message message)
        {
            lock (m_lockMsgQueue)
            {
                m_msgQueue.Enqueue(message.Clone() as Message);
            }
        }

        public void TalkAll()
        {
            lock (m_lockMsgQueue)
            {
                foreach (var msg in m_msgQueue)
                {
                    Talk(msg);
                }


                m_msgQueue.Clear();
            }
        }

        //################################################################################################

        protected abstract bool Talk(Message message);
    }
}
