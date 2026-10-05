namespace Avis.App
{
    partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pnl_SideBar = new Panel();
            btn_Faults = new Button();
            btn_VisualAid = new Button();
            btn_Station = new Button();
            btn_Exit = new Button();
            pnl_StatusBar = new FlowLayoutPanel();
            lbl_LinkScanner = new Label();
            lbl_LinkCamera = new Label();
            lbl_LinkIFactory = new Label();
            lbl_LinkLightGuide = new Label();
            pnl_Header = new Panel();
            lbl_Title = new Label();
            lbl_Mode = new Label();
            pic_Logo = new PictureBox();
            vv_IFactory = new Microsoft.Web.WebView2.WinForms.WebView2();
            pnl_IFactory = new Panel();
            lbl_IFactoryError = new Label();
            pnl_Right = new Panel();
            pnl_MainShow = new Panel();
            pnl_TopPanel = new Panel();
            lbl_YieldCaption = new Label();
            lbl_Yield = new Label();
            lbl_FailCaption = new Label();
            lbl_PassCaption = new Label();
            lbl_FailCount = new Label();
            lbl_PassCount = new Label();
            lbl_Station = new Label();
            lbl_StationCaption = new Label();
            lbl_Model = new Label();
            lbl_ModelCaption = new Label();
            tab_Main = new TabControl();
            tab_Production = new TabPage();
            tab_VisualAid = new TabPage();
            vv_VisualAid = new Microsoft.Web.WebView2.WinForms.WebView2();
            tab_Faults = new TabPage();
            pnl_SideBar.SuspendLayout();
            pnl_StatusBar.SuspendLayout();
            pnl_Header.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pic_Logo).BeginInit();
            ((System.ComponentModel.ISupportInitialize)vv_IFactory).BeginInit();
            pnl_IFactory.SuspendLayout();
            pnl_Right.SuspendLayout();
            pnl_TopPanel.SuspendLayout();
            tab_Main.SuspendLayout();
            tab_Production.SuspendLayout();
            tab_VisualAid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)vv_VisualAid).BeginInit();
            SuspendLayout();
            //
            // pnl_SideBar
            //
            pnl_SideBar.BackColor = Color.White;
            pnl_SideBar.Controls.Add(btn_Faults);
            pnl_SideBar.Controls.Add(btn_VisualAid);
            pnl_SideBar.Controls.Add(btn_Station);
            pnl_SideBar.Controls.Add(btn_Exit);
            pnl_SideBar.Dock = DockStyle.Left;
            pnl_SideBar.Location = new Point(0, 0);
            pnl_SideBar.Name = "pnl_SideBar";
            pnl_SideBar.Size = new Size(80, 866);
            pnl_SideBar.TabIndex = 0;
            //
            // btn_Faults
            //
            btn_Faults.BackgroundImageLayout = ImageLayout.Zoom;
            btn_Faults.Dock = DockStyle.Top;
            btn_Faults.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            btn_Faults.Location = new Point(0, 302);
            btn_Faults.Name = "btn_Faults";
            btn_Faults.Size = new Size(80, 70);
            btn_Faults.TabIndex = 3;
            btn_Faults.Text = "FAULTS";
            btn_Faults.UseVisualStyleBackColor = true;
            btn_Faults.Click += btn_Faults_Click;
            //
            // btn_VisualAid
            //
            btn_VisualAid.BackgroundImageLayout = ImageLayout.Zoom;
            btn_VisualAid.Dock = DockStyle.Top;
            btn_VisualAid.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            btn_VisualAid.Location = new Point(0, 232);
            btn_VisualAid.Name = "btn_VisualAid";
            btn_VisualAid.Size = new Size(80, 70);
            btn_VisualAid.TabIndex = 2;
            btn_VisualAid.Text = "VISUAL AID";
            btn_VisualAid.UseVisualStyleBackColor = true;
            btn_VisualAid.Click += btn_VisualAid_Click;
            //
            // btn_Station
            //
            btn_Station.BackgroundImageLayout = ImageLayout.Zoom;
            btn_Station.Dock = DockStyle.Top;
            btn_Station.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            btn_Station.Location = new Point(0, 162);
            btn_Station.Name = "btn_Station";
            btn_Station.Size = new Size(80, 70);
            btn_Station.TabIndex = 1;
            btn_Station.Text = "STATION";
            btn_Station.UseVisualStyleBackColor = true;
            btn_Station.Click += btn_Station_Click;
            //
            // btn_Exit
            //
            btn_Exit.BackColor = Color.White;
            btn_Exit.BackgroundImageLayout = ImageLayout.Zoom;
            btn_Exit.Dock = DockStyle.Top;
            btn_Exit.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            btn_Exit.Location = new Point(0, 0);
            btn_Exit.Name = "btn_Exit";
            btn_Exit.Size = new Size(80, 92);
            btn_Exit.TabIndex = 0;
            btn_Exit.Text = "CVG";
            btn_Exit.UseVisualStyleBackColor = false;
            btn_Exit.Click += btn_Exit_Click;
            //
            // pnl_StatusBar
            //
            pnl_StatusBar.BackColor = Color.FromArgb(0, 43, 73);
            pnl_StatusBar.Controls.Add(lbl_LinkScanner);
            pnl_StatusBar.Controls.Add(lbl_LinkCamera);
            pnl_StatusBar.Controls.Add(lbl_LinkIFactory);
            pnl_StatusBar.Controls.Add(lbl_LinkLightGuide);
            pnl_StatusBar.Dock = DockStyle.Bottom;
            pnl_StatusBar.Location = new Point(80, 833);
            pnl_StatusBar.Name = "pnl_StatusBar";
            pnl_StatusBar.Padding = new Padding(4, 3, 0, 0);
            pnl_StatusBar.Size = new Size(1743, 33);
            pnl_StatusBar.TabIndex = 1;
            //
            // lbl_LinkScanner
            //
            lbl_LinkScanner.AutoSize = true;
            lbl_LinkScanner.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            lbl_LinkScanner.ForeColor = Color.White;
            lbl_LinkScanner.Margin = new Padding(3, 3, 18, 0);
            lbl_LinkScanner.Name = "lbl_LinkScanner";
            lbl_LinkScanner.TabIndex = 0;
            lbl_LinkScanner.Text = "● BADGE SCANNER";
            //
            // lbl_LinkCamera
            //
            lbl_LinkCamera.AutoSize = true;
            lbl_LinkCamera.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            lbl_LinkCamera.ForeColor = Color.White;
            lbl_LinkCamera.Margin = new Padding(3, 3, 18, 0);
            lbl_LinkCamera.Name = "lbl_LinkCamera";
            lbl_LinkCamera.TabIndex = 1;
            lbl_LinkCamera.Text = "● JABILEYE";
            //
            // lbl_LinkIFactory
            //
            lbl_LinkIFactory.AutoSize = true;
            lbl_LinkIFactory.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            lbl_LinkIFactory.ForeColor = Color.White;
            lbl_LinkIFactory.Margin = new Padding(3, 3, 18, 0);
            lbl_LinkIFactory.Name = "lbl_LinkIFactory";
            lbl_LinkIFactory.TabIndex = 2;
            lbl_LinkIFactory.Text = "● IFACTORY";
            //
            // lbl_LinkLightGuide
            //
            lbl_LinkLightGuide.AutoSize = true;
            lbl_LinkLightGuide.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            lbl_LinkLightGuide.ForeColor = Color.White;
            lbl_LinkLightGuide.Margin = new Padding(3, 3, 18, 0);
            lbl_LinkLightGuide.Name = "lbl_LinkLightGuide";
            lbl_LinkLightGuide.TabIndex = 3;
            lbl_LinkLightGuide.Text = "● LIGHTGUIDE";
            //
            // pnl_Header
            //
            pnl_Header.BackColor = Color.White;
            pnl_Header.Controls.Add(lbl_Title);
            pnl_Header.Controls.Add(lbl_Mode);
            pnl_Header.Controls.Add(pic_Logo);
            pnl_Header.Dock = DockStyle.Top;
            pnl_Header.Location = new Point(80, 0);
            pnl_Header.Name = "pnl_Header";
            pnl_Header.Size = new Size(1743, 62);
            pnl_Header.TabIndex = 2;
            //
            // lbl_Title
            //
            lbl_Title.Dock = DockStyle.Fill;
            lbl_Title.Font = new Font("Segoe UI", 20.25F, FontStyle.Bold);
            lbl_Title.ForeColor = Color.FromArgb(0, 60, 107);
            lbl_Title.Location = new Point(160, 0);
            lbl_Title.Name = "lbl_Title";
            lbl_Title.Padding = new Padding(16, 0, 0, 0);
            lbl_Title.Size = new Size(805, 62);
            lbl_Title.TabIndex = 1;
            lbl_Title.Text = "AVIS";
            lbl_Title.TextAlign = ContentAlignment.MiddleLeft;
            //
            // lbl_Mode
            //
            lbl_Mode.Dock = DockStyle.Left;
            lbl_Mode.Font = new Font("Segoe UI", 15.75F, FontStyle.Bold);
            lbl_Mode.Location = new Point(0, 0);
            lbl_Mode.Name = "lbl_Mode";
            lbl_Mode.Size = new Size(160, 62);
            lbl_Mode.TabIndex = 0;
            lbl_Mode.Text = "MODE";
            lbl_Mode.TextAlign = ContentAlignment.MiddleCenter;
            //
            // pic_Logo
            //
            pic_Logo.BackColor = Color.White;
            pic_Logo.BackgroundImageLayout = ImageLayout.Zoom;
            pic_Logo.Dock = DockStyle.Right;
            pic_Logo.Location = new Point(965, 0);
            pic_Logo.Name = "pic_Logo";
            pic_Logo.Size = new Size(778, 62);
            pic_Logo.TabIndex = 2;
            pic_Logo.TabStop = false;
            //
            // vv_IFactory
            //
            vv_IFactory.AllowExternalDrop = false;
            vv_IFactory.CreationProperties = null;
            vv_IFactory.DefaultBackgroundColor = Color.White;
            vv_IFactory.Dock = DockStyle.Fill;
            vv_IFactory.Location = new Point(0, 0);
            vv_IFactory.Name = "vv_IFactory";
            vv_IFactory.Size = new Size(988, 737);
            vv_IFactory.TabIndex = 3;
            vv_IFactory.ZoomFactor = 1D;
            //
            // pnl_IFactory
            //
            pnl_IFactory.Controls.Add(vv_IFactory);
            pnl_IFactory.Controls.Add(lbl_IFactoryError);
            pnl_IFactory.Dock = DockStyle.Fill;
            pnl_IFactory.Location = new Point(3, 3);
            pnl_IFactory.Name = "pnl_IFactory";
            pnl_IFactory.Size = new Size(988, 737);
            pnl_IFactory.TabIndex = 4;
            //
            // lbl_IFactoryError
            //
            lbl_IFactoryError.BackColor = Color.FromArgb(189, 40, 50);
            lbl_IFactoryError.Dock = DockStyle.Top;
            lbl_IFactoryError.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold);
            lbl_IFactoryError.ForeColor = Color.White;
            lbl_IFactoryError.Location = new Point(0, 0);
            lbl_IFactoryError.Name = "lbl_IFactoryError";
            lbl_IFactoryError.Size = new Size(988, 40);
            lbl_IFactoryError.TabIndex = 4;
            lbl_IFactoryError.TextAlign = ContentAlignment.MiddleCenter;
            lbl_IFactoryError.Visible = false;
            //
            // pnl_Right
            //
            pnl_Right.Controls.Add(pnl_MainShow);
            pnl_Right.Controls.Add(pnl_TopPanel);
            pnl_Right.Dock = DockStyle.Right;
            pnl_Right.Location = new Point(991, 3);
            pnl_Right.Name = "pnl_Right";
            pnl_Right.Size = new Size(741, 737);
            pnl_Right.TabIndex = 5;
            //
            // pnl_MainShow
            //
            pnl_MainShow.Dock = DockStyle.Fill;
            pnl_MainShow.Location = new Point(0, 100);
            pnl_MainShow.Name = "pnl_MainShow";
            pnl_MainShow.Size = new Size(741, 637);
            pnl_MainShow.TabIndex = 1;
            //
            // pnl_TopPanel
            //
            pnl_TopPanel.BackColor = Color.White;
            pnl_TopPanel.Controls.Add(lbl_YieldCaption);
            pnl_TopPanel.Controls.Add(lbl_Yield);
            pnl_TopPanel.Controls.Add(lbl_FailCaption);
            pnl_TopPanel.Controls.Add(lbl_PassCaption);
            pnl_TopPanel.Controls.Add(lbl_FailCount);
            pnl_TopPanel.Controls.Add(lbl_PassCount);
            pnl_TopPanel.Controls.Add(lbl_Station);
            pnl_TopPanel.Controls.Add(lbl_StationCaption);
            pnl_TopPanel.Controls.Add(lbl_Model);
            pnl_TopPanel.Controls.Add(lbl_ModelCaption);
            pnl_TopPanel.Dock = DockStyle.Top;
            pnl_TopPanel.Location = new Point(0, 0);
            pnl_TopPanel.Name = "pnl_TopPanel";
            pnl_TopPanel.Size = new Size(741, 100);
            pnl_TopPanel.TabIndex = 0;
            //
            // lbl_YieldCaption
            //
            lbl_YieldCaption.AutoSize = true;
            lbl_YieldCaption.Font = new Font("Segoe UI", 20.25F, FontStyle.Bold);
            lbl_YieldCaption.Location = new Point(400, 50);
            lbl_YieldCaption.Name = "lbl_YieldCaption";
            lbl_YieldCaption.Size = new Size(90, 37);
            lbl_YieldCaption.TabIndex = 9;
            lbl_YieldCaption.Text = "YIELD";
            //
            // lbl_Yield
            //
            lbl_Yield.AutoSize = true;
            lbl_Yield.Font = new Font("Segoe UI", 20.25F, FontStyle.Bold);
            lbl_Yield.Location = new Point(496, 50);
            lbl_Yield.Name = "lbl_Yield";
            lbl_Yield.Size = new Size(39, 37);
            lbl_Yield.TabIndex = 8;
            lbl_Yield.Text = "-";
            //
            // lbl_FailCaption
            //
            lbl_FailCaption.AutoSize = true;
            lbl_FailCaption.Font = new Font("Segoe UI", 20.25F, FontStyle.Bold);
            lbl_FailCaption.ForeColor = Color.Red;
            lbl_FailCaption.Location = new Point(200, 50);
            lbl_FailCaption.Name = "lbl_FailCaption";
            lbl_FailCaption.Size = new Size(72, 37);
            lbl_FailCaption.TabIndex = 7;
            lbl_FailCaption.Text = "FAIL";
            //
            // lbl_PassCaption
            //
            lbl_PassCaption.AutoSize = true;
            lbl_PassCaption.Font = new Font("Segoe UI", 20.25F, FontStyle.Bold);
            lbl_PassCaption.ForeColor = Color.FromArgb(0, 192, 0);
            lbl_PassCaption.Location = new Point(6, 50);
            lbl_PassCaption.Name = "lbl_PassCaption";
            lbl_PassCaption.Size = new Size(81, 37);
            lbl_PassCaption.TabIndex = 5;
            lbl_PassCaption.Text = "PASS";
            //
            // lbl_FailCount
            //
            lbl_FailCount.AutoSize = true;
            lbl_FailCount.Font = new Font("Segoe UI", 20.25F, FontStyle.Bold);
            lbl_FailCount.ForeColor = Color.Red;
            lbl_FailCount.Location = new Point(278, 50);
            lbl_FailCount.Name = "lbl_FailCount";
            lbl_FailCount.Size = new Size(33, 37);
            lbl_FailCount.TabIndex = 6;
            lbl_FailCount.Text = "0";
            //
            // lbl_PassCount
            //
            lbl_PassCount.AutoSize = true;
            lbl_PassCount.Font = new Font("Segoe UI", 20.25F, FontStyle.Bold);
            lbl_PassCount.ForeColor = Color.FromArgb(0, 192, 0);
            lbl_PassCount.Location = new Point(93, 50);
            lbl_PassCount.Name = "lbl_PassCount";
            lbl_PassCount.Size = new Size(33, 37);
            lbl_PassCount.TabIndex = 4;
            lbl_PassCount.Text = "0";
            //
            // lbl_Station
            //
            lbl_Station.AutoSize = true;
            lbl_Station.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            lbl_Station.Location = new Point(138, 26);
            lbl_Station.Name = "lbl_Station";
            lbl_Station.Size = new Size(14, 17);
            lbl_Station.TabIndex = 3;
            lbl_Station.Text = "-";
            //
            // lbl_StationCaption
            //
            lbl_StationCaption.AutoSize = true;
            lbl_StationCaption.Font = new Font("Segoe UI", 9.75F);
            lbl_StationCaption.Location = new Point(6, 26);
            lbl_StationCaption.Name = "lbl_StationCaption";
            lbl_StationCaption.Size = new Size(110, 17);
            lbl_StationCaption.TabIndex = 2;
            lbl_StationCaption.Text = "STATION NAME : ";
            //
            // lbl_Model
            //
            lbl_Model.AutoSize = true;
            lbl_Model.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            lbl_Model.Location = new Point(138, 5);
            lbl_Model.Name = "lbl_Model";
            lbl_Model.Size = new Size(14, 17);
            lbl_Model.TabIndex = 1;
            lbl_Model.Text = "-";
            //
            // lbl_ModelCaption
            //
            lbl_ModelCaption.AutoSize = true;
            lbl_ModelCaption.Font = new Font("Segoe UI", 9.75F);
            lbl_ModelCaption.Location = new Point(6, 5);
            lbl_ModelCaption.Name = "lbl_ModelCaption";
            lbl_ModelCaption.Size = new Size(126, 17);
            lbl_ModelCaption.TabIndex = 0;
            lbl_ModelCaption.Text = "MODEL RUNNING : ";
            //
            // tab_Main
            //
            tab_Main.Controls.Add(tab_Production);
            tab_Main.Controls.Add(tab_VisualAid);
            tab_Main.Controls.Add(tab_Faults);
            tab_Main.Dock = DockStyle.Fill;
            tab_Main.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            tab_Main.Location = new Point(80, 62);
            tab_Main.Name = "tab_Main";
            tab_Main.SelectedIndex = 0;
            tab_Main.Size = new Size(1743, 771);
            tab_Main.TabIndex = 6;
            //
            // tab_Production
            //
            tab_Production.Controls.Add(pnl_IFactory);
            tab_Production.Controls.Add(pnl_Right);
            tab_Production.Location = new Point(4, 26);
            tab_Production.Name = "tab_Production";
            tab_Production.Padding = new Padding(3);
            tab_Production.Size = new Size(1735, 741);
            tab_Production.TabIndex = 0;
            tab_Production.Text = "PRODUCTION";
            tab_Production.UseVisualStyleBackColor = true;
            //
            // tab_VisualAid
            //
            tab_VisualAid.Controls.Add(vv_VisualAid);
            tab_VisualAid.Location = new Point(4, 26);
            tab_VisualAid.Name = "tab_VisualAid";
            tab_VisualAid.Padding = new Padding(3);
            tab_VisualAid.Size = new Size(1735, 741);
            tab_VisualAid.TabIndex = 1;
            tab_VisualAid.Text = "VISUAL AID";
            tab_VisualAid.UseVisualStyleBackColor = true;
            //
            // vv_VisualAid
            //
            vv_VisualAid.AllowExternalDrop = false;
            vv_VisualAid.CreationProperties = null;
            vv_VisualAid.DefaultBackgroundColor = Color.White;
            vv_VisualAid.Dock = DockStyle.Fill;
            vv_VisualAid.Location = new Point(3, 3);
            vv_VisualAid.Name = "vv_VisualAid";
            vv_VisualAid.Size = new Size(1729, 735);
            vv_VisualAid.TabIndex = 0;
            vv_VisualAid.ZoomFactor = 1D;
            //
            // tab_Faults
            //
            tab_Faults.Location = new Point(4, 26);
            tab_Faults.Name = "tab_Faults";
            tab_Faults.Padding = new Padding(3);
            tab_Faults.Size = new Size(1735, 741);
            tab_Faults.TabIndex = 2;
            tab_Faults.Text = "FAULT FEED";
            tab_Faults.UseVisualStyleBackColor = true;
            //
            // Main
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1823, 866);
            Controls.Add(tab_Main);
            Controls.Add(pnl_Header);
            Controls.Add(pnl_StatusBar);
            Controls.Add(pnl_SideBar);
            FormBorderStyle = FormBorderStyle.None;
            Name = "Main";
            Text = "AVIS";
            WindowState = FormWindowState.Maximized;
            pnl_SideBar.ResumeLayout(false);
            pnl_StatusBar.ResumeLayout(false);
            pnl_StatusBar.PerformLayout();
            pnl_Header.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pic_Logo).EndInit();
            ((System.ComponentModel.ISupportInitialize)vv_IFactory).EndInit();
            pnl_IFactory.ResumeLayout(false);
            pnl_Right.ResumeLayout(false);
            pnl_TopPanel.ResumeLayout(false);
            pnl_TopPanel.PerformLayout();
            tab_Main.ResumeLayout(false);
            tab_Production.ResumeLayout(false);
            tab_VisualAid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)vv_VisualAid).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnl_SideBar;
        private Button btn_Exit;
        private Button btn_Station;
        private Button btn_VisualAid;
        private Button btn_Faults;
        private FlowLayoutPanel pnl_StatusBar;
        private Label lbl_LinkScanner;
        private Label lbl_LinkCamera;
        private Label lbl_LinkIFactory;
        private Label lbl_LinkLightGuide;
        private Panel pnl_Header;
        private Label lbl_Title;
        private Label lbl_Mode;
        private PictureBox pic_Logo;
        private Microsoft.Web.WebView2.WinForms.WebView2 vv_IFactory;
        private Panel pnl_IFactory;
        private Label lbl_IFactoryError;
        private Panel pnl_Right;
        private Panel pnl_MainShow;
        private Panel pnl_TopPanel;
        private Label lbl_ModelCaption;
        private Label lbl_Model;
        private Label lbl_StationCaption;
        private Label lbl_Station;
        private Label lbl_PassCaption;
        private Label lbl_PassCount;
        private Label lbl_FailCaption;
        private Label lbl_FailCount;
        private Label lbl_YieldCaption;
        private Label lbl_Yield;
        private TabControl tab_Main;
        private TabPage tab_Production;
        private TabPage tab_VisualAid;
        private Microsoft.Web.WebView2.WinForms.WebView2 vv_VisualAid;
        private TabPage tab_Faults;
    }
}
