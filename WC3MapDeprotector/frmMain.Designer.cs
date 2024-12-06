namespace WC3MapDeprotector
{
    partial class frmMain
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            tbInputFile = new TextBox();
            tbOutputFile = new TextBox();
            fdInputFile = new OpenFileDialog();
            fdOutputFile = new SaveFileDialog();
            btnRebuildMap = new Button();
            label1 = new Label();
            label2 = new Label();
            btnInputFileBrowse = new Button();
            btnOutputFileBrowse = new Button();
            btnDonate = new Button();
            btnBugReport = new Button();
            label4 = new Label();
            tbWarningMessages = new TextBox();
            label3 = new Label();
            tbDebugLog = new TextBox();
            groupBox1 = new GroupBox();
            cbVisualTriggers = new CheckBox();
            cbTranspileToLua = new CheckBox();
            cbOutputFormat = new ComboBox();
            label5 = new Label();
            btnHelp = new Button();
            btnYoutube = new Button();
            btnDiscord = new Button();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // tbInputFile
            // 
            tbInputFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tbInputFile.Location = new Point(132, 27);
            tbInputFile.Margin = new Padding(5);
            tbInputFile.Name = "tbInputFile";
            tbInputFile.Size = new Size(1027, 32);
            tbInputFile.TabIndex = 0;
            tbInputFile.TextChanged += tbInputFile_TextChanged;
            // 
            // tbOutputFile
            // 
            tbOutputFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tbOutputFile.Location = new Point(132, 93);
            tbOutputFile.Margin = new Padding(5);
            tbOutputFile.Name = "tbOutputFile";
            tbOutputFile.Size = new Size(1027, 32);
            tbOutputFile.TabIndex = 1;
            tbOutputFile.TextChanged += tbOutputFile_TextChanged;
            // 
            // fdInputFile
            // 
            fdInputFile.Filter = "\"Warcraft 3 Maps\"|*.w3m; *.w3x;*.w3n";
            // 
            // fdOutputFile
            // 
            fdOutputFile.Filter = "\"Warcraft 3 Maps\"|*.w3m,*.w3x";
            // 
            // btnRebuildMap
            // 
            btnRebuildMap.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRebuildMap.Location = new Point(1046, 225);
            btnRebuildMap.Margin = new Padding(5);
            btnRebuildMap.Name = "btnRebuildMap";
            btnRebuildMap.Size = new Size(190, 38);
            btnRebuildMap.TabIndex = 2;
            btnRebuildMap.Text = "Rebuild Map";
            btnRebuildMap.UseVisualStyleBackColor = true;
            btnRebuildMap.Click += btnRebuildMap_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(19, 27);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(90, 25);
            label1.TabIndex = 4;
            label1.Text = "Input File";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(19, 93);
            label2.Margin = new Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new Size(105, 25);
            label2.TabIndex = 5;
            label2.Text = "Output File";
            // 
            // btnInputFileBrowse
            // 
            btnInputFileBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnInputFileBrowse.Image = (Image)resources.GetObject("btnInputFileBrowse.Image");
            btnInputFileBrowse.Location = new Point(1169, 15);
            btnInputFileBrowse.Margin = new Padding(5);
            btnInputFileBrowse.Name = "btnInputFileBrowse";
            btnInputFileBrowse.Size = new Size(67, 55);
            btnInputFileBrowse.TabIndex = 7;
            btnInputFileBrowse.UseVisualStyleBackColor = true;
            btnInputFileBrowse.Click += btnInputFileBrowse_Click;
            // 
            // btnOutputFileBrowse
            // 
            btnOutputFileBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOutputFileBrowse.Image = (Image)resources.GetObject("btnOutputFileBrowse.Image");
            btnOutputFileBrowse.Location = new Point(1169, 83);
            btnOutputFileBrowse.Margin = new Padding(5);
            btnOutputFileBrowse.Name = "btnOutputFileBrowse";
            btnOutputFileBrowse.Size = new Size(67, 55);
            btnOutputFileBrowse.TabIndex = 8;
            btnOutputFileBrowse.UseVisualStyleBackColor = true;
            btnOutputFileBrowse.Click += btnOutputFileBrowse_Click;
            // 
            // btnDonate
            // 
            btnDonate.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnDonate.Image = (Image)resources.GetObject("btnDonate.Image");
            btnDonate.Location = new Point(18, 729);
            btnDonate.Name = "btnDonate";
            btnDonate.Size = new Size(100, 80);
            btnDonate.TabIndex = 11;
            btnDonate.Text = "Donate";
            btnDonate.TextAlign = ContentAlignment.BottomCenter;
            btnDonate.TextImageRelation = TextImageRelation.ImageAboveText;
            btnDonate.UseVisualStyleBackColor = true;
            btnDonate.Click += btnDonate_Click;
            // 
            // btnBugReport
            // 
            btnBugReport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnBugReport.Image = (Image)resources.GetObject("btnBugReport.Image");
            btnBugReport.Location = new Point(987, 729);
            btnBugReport.Name = "btnBugReport";
            btnBugReport.Size = new Size(125, 80);
            btnBugReport.TabIndex = 12;
            btnBugReport.Text = "Bug Report";
            btnBugReport.TextAlign = ContentAlignment.BottomCenter;
            btnBugReport.TextImageRelation = TextImageRelation.ImageAboveText;
            btnBugReport.UseVisualStyleBackColor = true;
            btnBugReport.Click += btnBugReport_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(19, 262);
            label4.Margin = new Padding(5, 0, 5, 0);
            label4.Name = "label4";
            label4.Size = new Size(92, 25);
            label4.TabIndex = 26;
            label4.Text = "Warnings";
            // 
            // tbWarningMessages
            // 
            tbWarningMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tbWarningMessages.Location = new Point(19, 292);
            tbWarningMessages.Margin = new Padding(5);
            tbWarningMessages.MinimumSize = new Size(0, 100);
            tbWarningMessages.Multiline = true;
            tbWarningMessages.Name = "tbWarningMessages";
            tbWarningMessages.ReadOnly = true;
            tbWarningMessages.ScrollBars = ScrollBars.Both;
            tbWarningMessages.Size = new Size(1217, 175);
            tbWarningMessages.TabIndex = 25;
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            label3.AutoSize = true;
            label3.Location = new Point(18, 481);
            label3.Margin = new Padding(5, 0, 5, 0);
            label3.Name = "label3";
            label3.Size = new Size(104, 25);
            label3.TabIndex = 24;
            label3.Text = "Debug Log";
            // 
            // tbDebugLog
            // 
            tbDebugLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tbDebugLog.Location = new Point(18, 511);
            tbDebugLog.Margin = new Padding(5);
            tbDebugLog.MinimumSize = new Size(0, 100);
            tbDebugLog.Multiline = true;
            tbDebugLog.Name = "tbDebugLog";
            tbDebugLog.ReadOnly = true;
            tbDebugLog.ScrollBars = ScrollBars.Both;
            tbDebugLog.Size = new Size(1217, 201);
            tbDebugLog.TabIndex = 23;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(cbVisualTriggers);
            groupBox1.Controls.Add(cbTranspileToLua);
            groupBox1.Location = new Point(19, 145);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(482, 114);
            groupBox1.TabIndex = 27;
            groupBox1.TabStop = false;
            groupBox1.Text = "Experimental options (these don't work very well yet)";
            // 
            // cbVisualTriggers
            // 
            cbVisualTriggers.AutoSize = true;
            cbVisualTriggers.Location = new Point(40, 31);
            cbVisualTriggers.Name = "cbVisualTriggers";
            cbVisualTriggers.Size = new Size(369, 29);
            cbVisualTriggers.TabIndex = 17;
            cbVisualTriggers.Text = "Convert Jass code to Visual/GUI Triggers";
            cbVisualTriggers.UseVisualStyleBackColor = true;
            // 
            // cbTranspileToLua
            // 
            cbTranspileToLua.AutoSize = true;
            cbTranspileToLua.Location = new Point(40, 66);
            cbTranspileToLua.Name = "cbTranspileToLua";
            cbTranspileToLua.Size = new Size(238, 29);
            cbTranspileToLua.TabIndex = 16;
            cbTranspileToLua.Text = "Convert Jass code to Lua";
            cbTranspileToLua.UseVisualStyleBackColor = true;
            // 
            // cbOutputFormat
            // 
            cbOutputFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cbOutputFormat.FormattingEnabled = true;
            cbOutputFormat.Items.AddRange(new object[] { "Reforged", "Classic / 1.26a" });
            cbOutputFormat.Location = new Point(1068, 155);
            cbOutputFormat.Name = "cbOutputFormat";
            cbOutputFormat.Size = new Size(167, 33);
            cbOutputFormat.TabIndex = 28;
            cbOutputFormat.SelectedValueChanged += cbOutputFormat_SelectedValueChanged;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(927, 158);
            label5.Name = "label5";
            label5.Size = new Size(135, 25);
            label5.TabIndex = 29;
            label5.Text = "Output Format";
            // 
            // btnHelp
            // 
            btnHelp.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnHelp.Image = (Image)resources.GetObject("btnHelp.Image");
            btnHelp.Location = new Point(1136, 729);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(100, 80);
            btnHelp.TabIndex = 30;
            btnHelp.Text = "Help";
            btnHelp.TextAlign = ContentAlignment.BottomCenter;
            btnHelp.TextImageRelation = TextImageRelation.ImageAboveText;
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += btnHelp_Click;
            // 
            // btnYoutube
            // 
            btnYoutube.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnYoutube.Image = (Image)resources.GetObject("btnYoutube.Image");
            btnYoutube.Location = new Point(136, 729);
            btnYoutube.Name = "btnYoutube";
            btnYoutube.Size = new Size(125, 80);
            btnYoutube.TabIndex = 31;
            btnYoutube.Text = "My Channel";
            btnYoutube.TextAlign = ContentAlignment.BottomCenter;
            btnYoutube.TextImageRelation = TextImageRelation.ImageAboveText;
            btnYoutube.UseVisualStyleBackColor = true;
            btnYoutube.Click += btnYoutube_Click;
            // 
            // btnDiscord
            // 
            btnDiscord.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnDiscord.Image = (Image)resources.GetObject("btnDiscord.Image");
            btnDiscord.Location = new Point(279, 729);
            btnDiscord.Name = "btnDiscord";
            btnDiscord.Size = new Size(125, 80);
            btnDiscord.TabIndex = 32;
            btnDiscord.Text = "My Discord";
            btnDiscord.TextAlign = ContentAlignment.BottomCenter;
            btnDiscord.TextImageRelation = TextImageRelation.ImageAboveText;
            btnDiscord.UseVisualStyleBackColor = true;
            btnDiscord.Click += btnDiscord_Click;
            // 
            // frmMain
            // 
            AutoScaleDimensions = new SizeF(11F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1257, 821);
            Controls.Add(btnDiscord);
            Controls.Add(btnYoutube);
            Controls.Add(btnHelp);
            Controls.Add(label5);
            Controls.Add(cbOutputFormat);
            Controls.Add(groupBox1);
            Controls.Add(label4);
            Controls.Add(tbWarningMessages);
            Controls.Add(label3);
            Controls.Add(tbDebugLog);
            Controls.Add(btnBugReport);
            Controls.Add(btnDonate);
            Controls.Add(btnOutputFileBrowse);
            Controls.Add(btnInputFileBrowse);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(btnRebuildMap);
            Controls.Add(tbOutputFile);
            Controls.Add(tbInputFile);
            Font = new Font("Segoe UI", 14F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5);
            MinimumSize = new Size(800, 775);
            Name = "frmMain";
            Load += MainForm_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox tbInputFile;
        private TextBox tbOutputFile;
        private OpenFileDialog fdInputFile;
        private SaveFileDialog fdOutputFile;
        private Button btnRebuildMap;
        private Label label1;
        private Label label2;
        private Button btnInputFileBrowse;
        private Button btnOutputFileBrowse;
        private Button btnDonate;
        private Button btnBugReport;
        private Label label4;
        private TextBox tbWarningMessages;
        private Label label3;
        private TextBox tbDebugLog;
        private GroupBox groupBox1;
        private CheckBox cbVisualTriggers;
        private CheckBox cbTranspileToLua;
        private ComboBox cbOutputFormat;
        private Label label5;
        private Button btnHelp;
        private Button btnYoutube;
        private Button btnDiscord;
    }
}
