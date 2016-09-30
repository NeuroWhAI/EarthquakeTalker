using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthquakeTalker
{
    public abstract class Worker
    {
        public Worker()
        {

        }

        //############################################################################################

        private Talker m_talker = null;

        protected Logger m_logger = null;

        protected Task m_task = null;
        protected bool m_onRunning = false;
        public TimeSpan JobDelay
        { get; set; } = TimeSpan.FromSeconds(0.1);

        //############################################################################################

        public void Start(Talker talker, Logger logger)
        {
            if (talker == null)
                throw new ArgumentException("talker는 null이 아니어야 합니다.");
            if (logger == null)
                throw new ArgumentException("logger는 null이 아니어야 합니다.");


            Stop();


            m_talker = talker;
            m_logger = logger;

            BeforeStart(talker);

            m_onRunning = true;
            m_task = Task.Factory.StartNew(Work);
        }

        public void Stop()
        {
            if (m_task != null)
            {
                m_onRunning = false;
                m_task.Wait();

                AfterStop(m_talker);
            }
        }

        private void Work()
        {
            while (m_onRunning)
            {
                Message msg = OnWork();

                if (msg != null)
                {
                    m_talker.PushMessage(msg);
                }


                System.Threading.Thread.Sleep(JobDelay);
            }
        }

        //############################################################################################

        protected abstract void BeforeStart(Talker talker);

        protected abstract void AfterStop(Talker talker);

        protected abstract Message OnWork();
    }
}
