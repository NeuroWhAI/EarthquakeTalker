using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public class Logger
    {
        public Logger()
        {

        }

        //#############################################################################################

        protected Queue<Message> m_msgQueue = new Queue<Message>();
        protected readonly object m_lockMsgQueue = new object();

        //#############################################################################################

        public void PushLog(Message message)
        {
            lock (m_lockMsgQueue)
            {
                m_msgQueue.Enqueue(message.Clone() as Message);
            }
        }

        public void PushLog(string text)
        {
            lock (m_lockMsgQueue)
            {
                m_msgQueue.Enqueue(new Message(text: text,
                    sender: "Logger",
                    level: Message.Priority.Low));
            }
        }

        public void LogAll()
        {
            lock (m_lockMsgQueue)
            {
                foreach (var msg in m_msgQueue)
                {
                    Console.WriteLine("");
                    Console.WriteLine(msg.ToString());
                    Console.WriteLine("");
                }

                m_msgQueue.Clear();
            }
        }
    }
}
