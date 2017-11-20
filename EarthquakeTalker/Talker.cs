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
            var failedMsg = new List<Message>();


            int count = m_msgQueue.Count;

            for (int m = 0; m < count; ++m)
            {
                Message msg = null;

                lock (m_lockMsgQueue)
                {
                    msg = m_msgQueue.Dequeue();
                }


                bool result = Talk(msg);

                if (result == false)
                {
                    failedMsg.Add(msg);
                }
            }


            if (failedMsg.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Failed messages : " + failedMsg.Count);
                Console.WriteLine();
            }

            foreach (var msg in failedMsg)
            {
                m_msgQueue.Enqueue(msg);
            }
        }

        //################################################################################################

        protected abstract bool Talk(Message message);
    }
}
