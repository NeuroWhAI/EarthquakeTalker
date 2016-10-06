using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EarthquakeTalkerController
{
    public partial class Form_Main : Form
    {
        public Form_Main()
        {
            InitializeComponent();


            m_graphList.Add(new Graph());
            m_graphList.Add(new Graph());
            m_graphList.Add(new Graph());
        }

        //##############################################################################################

        protected Task m_inputTask = null;
        protected List<Graph> m_graphList = new List<Graph>();
        protected bool m_onRun = false;

        //##############################################################################################

        private void Form_Main_Load(object sender, EventArgs e)
        {
            foreach (var graph in m_graphList)
            {
                var args = Console.ReadLine().Split('|');
                graph.Name = args[0];
                graph.Gain = double.Parse(args[1]);
            }

            m_onRun = true;
            m_inputTask = Task.Factory.StartNew(delegate ()
            {
                try
                {
                    while (m_onRun)
                    {
                        var args = Console.ReadLine().Split(' ');
                        int index = int.Parse(args[0]);
                        int data = int.Parse(args[1]);

                        m_graphList[index].PushData(data);


                        this.Invoke(new Action(() =>
                        {
                            if (this.timer_update.Enabled == false)
                                this.timer_update.Start();
                        }));
                    }
                }
                catch (Exception)
                {
                    return;
                }
            });
        }

        private void Form_Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_onRun = false;
            m_inputTask.Wait(2000);
        }

        //##############################################################################################

        private void panel_graph1_Paint(object sender, PaintEventArgs e)
        {
            m_graphList[0].Draw(e.Graphics, this.panel_graph1.Size);
        }

        private void panel_graph2_Paint(object sender, PaintEventArgs e)
        {
            m_graphList[1].Draw(e.Graphics, this.panel_graph2.Size);
        }

        private void panel_graph3_Paint(object sender, PaintEventArgs e)
        {
            m_graphList[2].Draw(e.Graphics, this.panel_graph3.Size);
        }

        //##############################################################################################

        private void timer_update_Tick(object sender, EventArgs e)
        {
            this.Invalidate(true);


            this.timer_update.Stop();
        }

        //##############################################################################################

        protected void ToggleGraphVisible(int index)
        {
            m_graphList[index].Visible = !m_graphList[index].Visible;

            this.Invalidate(true);
        }

        private void panel_graph1_Click(object sender, EventArgs e)
        {
            ToggleGraphVisible(0);
        }

        private void panel_graph2_Click(object sender, EventArgs e)
        {
            ToggleGraphVisible(1);
        }

        private void panel_graph3_Click(object sender, EventArgs e)
        {
            ToggleGraphVisible(2);
        }
    }
}
