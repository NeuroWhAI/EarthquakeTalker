﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
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
            m_graphList.Add(new Graph());

            foreach (var graph in m_graphList)
            {
                graph.SavePath = Path.Combine(Application.StartupPath, "Seismograph");
            }
        }

        //##############################################################################################

        protected Task m_inputTask = null;
        protected List<Graph> m_graphList = new List<Graph>();
        protected bool m_onRun = false;

        protected bool[] m_drawFlag = { false, false, false, false };
        protected readonly object[] m_lockDrawFlag = { new object(), new object(), new object(), new object() };

        //##############################################################################################

        private void Form_Main_Load(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.Assert(m_graphList.Count == m_drawFlag.Length
                && m_graphList.Count == m_lockDrawFlag.Length);


            foreach (var graph in m_graphList)
            {
                var args = Console.ReadLine().Split('|');
                graph.Name = args[0];
                graph.Gain = double.Parse(args[1]);
                graph.DangerValue = double.Parse(args[2]);
            }

            m_onRun = true;
            m_inputTask = Task.Factory.StartNew(delegate ()
            {
                int graphCount = m_graphList.Count;

                try
                {
                    while (m_onRun)
                    {
                        var args = Console.ReadLine().Split('|');

                        int index = -1;
                        int data = 0;

                        if (int.TryParse(args[0], out index) && int.TryParse(args[1], out data))
                        {
                            if (index >= 0 && index < graphCount)
                            {
                                m_graphList[index].PushData(data);


                                lock (m_lockDrawFlag[index])
                                {
                                    m_drawFlag[index] = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception exp)
                {
                    MessageBox.Show(exp.Message + "\n\n" + exp.StackTrace, "Error!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);

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

        private void panel_graph4_Paint(object sender, PaintEventArgs e)
        {
            m_graphList[3].Draw(e.Graphics, this.panel_graph4.Size);
        }

        //##############################################################################################

        private void timer_update_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < m_lockDrawFlag.Length; ++i)
            {
                if (m_drawFlag[i])
                {
                    this.Invalidate(true);


                    lock (m_lockDrawFlag[i])
                    {
                        m_drawFlag[i] = false;
                    }
                }
            }
        }

        //##############################################################################################

        protected void ToggleGraphVisible(int index)
        {
            m_graphList[index].Visible = !m_graphList[index].Visible;

            if (m_graphList[index].Visible == false)
            {
                m_graphList[index].ResetTempMax();
            }

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

        private void panel_graph4_Click(object sender, EventArgs e)
        {
            ToggleGraphVisible(3);
        }
    }
}
