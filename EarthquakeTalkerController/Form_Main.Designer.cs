namespace EarthquakeTalkerController
{
    partial class Form_Main
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panel_graph1 = new System.Windows.Forms.Panel();
            this.panel_graph2 = new System.Windows.Forms.Panel();
            this.panel_graph3 = new System.Windows.Forms.Panel();
            this.timer_update = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // panel_graph1
            // 
            this.panel_graph1.BackColor = System.Drawing.SystemColors.Control;
            this.panel_graph1.Location = new System.Drawing.Point(12, 12);
            this.panel_graph1.Name = "panel_graph1";
            this.panel_graph1.Size = new System.Drawing.Size(857, 186);
            this.panel_graph1.TabIndex = 0;
            this.panel_graph1.Click += new System.EventHandler(this.panel_graph1_Click);
            this.panel_graph1.Paint += new System.Windows.Forms.PaintEventHandler(this.panel_graph1_Paint);
            // 
            // panel_graph2
            // 
            this.panel_graph2.BackColor = System.Drawing.SystemColors.Control;
            this.panel_graph2.Location = new System.Drawing.Point(12, 204);
            this.panel_graph2.Name = "panel_graph2";
            this.panel_graph2.Size = new System.Drawing.Size(857, 186);
            this.panel_graph2.TabIndex = 1;
            this.panel_graph2.Click += new System.EventHandler(this.panel_graph2_Click);
            this.panel_graph2.Paint += new System.Windows.Forms.PaintEventHandler(this.panel_graph2_Paint);
            // 
            // panel_graph3
            // 
            this.panel_graph3.BackColor = System.Drawing.SystemColors.Control;
            this.panel_graph3.Location = new System.Drawing.Point(12, 396);
            this.panel_graph3.Name = "panel_graph3";
            this.panel_graph3.Size = new System.Drawing.Size(857, 186);
            this.panel_graph3.TabIndex = 1;
            this.panel_graph3.Click += new System.EventHandler(this.panel_graph3_Click);
            this.panel_graph3.Paint += new System.Windows.Forms.PaintEventHandler(this.panel_graph3_Paint);
            // 
            // timer_update
            // 
            this.timer_update.Interval = 1500;
            this.timer_update.Tick += new System.EventHandler(this.timer_update_Tick);
            // 
            // Form_Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.ClientSize = new System.Drawing.Size(881, 594);
            this.Controls.Add(this.panel_graph3);
            this.Controls.Add(this.panel_graph2);
            this.Controls.Add(this.panel_graph1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form_Main";
            this.Text = "Controller";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Main_FormClosing);
            this.Load += new System.EventHandler(this.Form_Main_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel_graph1;
        private System.Windows.Forms.Panel panel_graph2;
        private System.Windows.Forms.Panel panel_graph3;
        private System.Windows.Forms.Timer timer_update;
    }
}

