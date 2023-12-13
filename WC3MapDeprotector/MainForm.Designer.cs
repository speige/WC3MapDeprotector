namespace WC3MapDeprotector
{
    partial class MainForm
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            tbInputFile = new TextBox();
            tbOutputFile = new TextBox();
            fdInputFile = new OpenFileDialog();
            fdOutputFile = new SaveFileDialog();
            btnRebuildMap = new Button();
            tbDebugLog = new TextBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            btnInputFileBrowse = new Button();
            btnOutputFileBrowse = new Button();
            cbTranspileToLua = new CheckBox();
            cbBruteForceUnknowns = new CheckBox();
            btnDonate = new Button();
            btnBugReport = new Button();
            label4 = new Label();
            tbWarningMessages = new TextBox();
            cbVisualTriggers = new CheckBox();
            SuspendLayout();
            // 
            // tbInputFile
            // 
            tbInputFile.Location = new Point(132, 27);
            tbInputFile.Margin = new Padding(5);
            tbInputFile.Name = "tbInputFile";
            tbInputFile.Size = new Size(1027, 32);
            tbInputFile.TabIndex = 0;
            tbInputFile.TextChanged += tbInputFile_TextChanged;
            // 
            // tbOutputFile
            // 
            tbOutputFile.Location = new Point(132, 93);
            tbOutputFile.Margin = new Padding(5);
            tbOutputFile.Name = "tbOutputFile";
            tbOutputFile.Size = new Size(1027, 32);
            tbOutputFile.TabIndex = 1;
            tbOutputFile.TextChanged += tbOutputFile_TextChanged;
            // 
            // fdInputFile
            // 
            fdInputFile.Filter = "\"Warcraft 3 Maps\"|*.w3m; *.w3x";
            // 
            // fdOutputFile
            // 
            fdOutputFile.Filter = "\"Warcraft 3 Maps\"|*.w3m,*.w3x";
            // 
            // btnRebuildMap
            // 
            btnRebuildMap.Location = new Point(1046, 225);
            btnRebuildMap.Margin = new Padding(5);
            btnRebuildMap.Name = "btnRebuildMap";
            btnRebuildMap.Size = new Size(190, 38);
            btnRebuildMap.TabIndex = 2;
            btnRebuildMap.Text = "Rebuild Map";
            btnRebuildMap.UseVisualStyleBackColor = true;
            btnRebuildMap.Click += btnRebuildMap_Click;
            // 
            // tbDebugLog
            // 
            tbDebugLog.Location = new Point(19, 552);
            tbDebugLog.Margin = new Padding(5);
            tbDebugLog.Multiline = true;
            tbDebugLog.Name = "tbDebugLog";
            tbDebugLog.ReadOnly = true;
            tbDebugLog.ScrollBars = ScrollBars.Both;
            tbDebugLog.Size = new Size(1217, 175);
            tbDebugLog.TabIndex = 3;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(19, 30);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(90, 25);
            label1.TabIndex = 4;
            label1.Text = "Input File";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(19, 98);
            label2.Margin = new Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new Size(105, 25);
            label2.TabIndex = 5;
            label2.Text = "Output File";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(19, 522);
            label3.Margin = new Padding(5, 0, 5, 0);
            label3.Name = "label3";
            label3.Size = new Size(104, 25);
            label3.TabIndex = 6;
            label3.Text = "Debug Log";
            // 
            // btnInputFileBrowse
            // 
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
            btnOutputFileBrowse.Image = (Image)resources.GetObject("btnOutputFileBrowse.Image");
            btnOutputFileBrowse.Location = new Point(1169, 83);
            btnOutputFileBrowse.Margin = new Padding(5);
            btnOutputFileBrowse.Name = "btnOutputFileBrowse";
            btnOutputFileBrowse.Size = new Size(67, 55);
            btnOutputFileBrowse.TabIndex = 8;
            btnOutputFileBrowse.UseVisualStyleBackColor = true;
            btnOutputFileBrowse.Click += btnOutputFileBrowse_Click;
            // 
            // cbTranspileToLua
            // 
            cbTranspileToLua.AutoSize = true;
            cbTranspileToLua.Location = new Point(132, 190);
            cbTranspileToLua.Name = "cbTranspileToLua";
            cbTranspileToLua.Size = new Size(329, 29);
            cbTranspileToLua.TabIndex = 9;
            cbTranspileToLua.Text = "Convert JASS to LUA (experimental)";
            cbTranspileToLua.UseVisualStyleBackColor = true;
            // 
            // cbBruteForceUnknowns
            // 
            cbBruteForceUnknowns.AutoSize = true;
            cbBruteForceUnknowns.Location = new Point(132, 225);
            cbBruteForceUnknowns.Name = "cbBruteForceUnknowns";
            cbBruteForceUnknowns.Size = new Size(407, 29);
            cbBruteForceUnknowns.TabIndex = 10;
            cbBruteForceUnknowns.Text = "Brute force unknown files (SLOW as in DAYS)";
            cbBruteForceUnknowns.UseVisualStyleBackColor = true;
            // 
            // btnDonate
            // 
            btnDonate.Image = (Image)resources.GetObject("btnDonate.Image");
            btnDonate.Location = new Point(1079, 754);
            btnDonate.Name = "btnDonate";
            btnDonate.Size = new Size(67, 55);
            btnDonate.TabIndex = 11;
            btnDonate.UseVisualStyleBackColor = true;
            btnDonate.Click += btnDonate_Click;
            // 
            // btnBugReport
            // 
            btnBugReport.Image = (Image)resources.GetObject("btnBugReport.Image");
            btnBugReport.Location = new Point(1170, 754);
            btnBugReport.Name = "btnBugReport";
            btnBugReport.Size = new Size(67, 55);
            btnBugReport.TabIndex = 12;
            btnBugReport.UseVisualStyleBackColor = true;
            btnBugReport.Click += btnBugReport_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(20, 273);
            label4.Margin = new Padding(5, 0, 5, 0);
            label4.Name = "label4";
            label4.Size = new Size(92, 25);
            label4.TabIndex = 14;
            label4.Text = "Warnings";
            // 
            // tbWarningMessages
            // 
            tbWarningMessages.Location = new Point(20, 303);
            tbWarningMessages.Margin = new Padding(5);
            tbWarningMessages.Multiline = true;
            tbWarningMessages.Name = "tbWarningMessages";
            tbWarningMessages.ReadOnly = true;
            tbWarningMessages.ScrollBars = ScrollBars.Both;
            tbWarningMessages.Size = new Size(1217, 175);
            tbWarningMessages.TabIndex = 13;
            // 
            // cbVisualTriggers
            // 
            cbVisualTriggers.AutoSize = true;
            cbVisualTriggers.Location = new Point(132, 155);
            cbVisualTriggers.Name = "cbVisualTriggers";
            cbVisualTriggers.Size = new Size(341, 29);
            cbVisualTriggers.TabIndex = 15;
            cbVisualTriggers.Text = "Create Visual Triggers (experimental)";
            cbVisualTriggers.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(11F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1257, 821);
            Controls.Add(cbVisualTriggers);
            Controls.Add(label4);
            Controls.Add(tbWarningMessages);
            Controls.Add(btnBugReport);
            Controls.Add(btnDonate);
            Controls.Add(cbBruteForceUnknowns);
            Controls.Add(cbTranspileToLua);
            Controls.Add(btnOutputFileBrowse);
            Controls.Add(btnInputFileBrowse);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(tbDebugLog);
            Controls.Add(btnRebuildMap);
            Controls.Add(tbOutputFile);
            Controls.Add(tbInputFile);
            Font = new Font("Segoe UI", 14F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5);
            Name = "MainForm";
            Text = "WC3MapDeprotector by http://www.youtube.com/@ai-gamer";
            Load += MainForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox tbInputFile;
        private TextBox tbOutputFile;
        private OpenFileDialog fdInputFile;
        private SaveFileDialog fdOutputFile;
        private Button btnRebuildMap;
        private TextBox tbDebugLog;
        private Label label1;
        private Label label2;
        private Label label3;
        private Button btnInputFileBrowse;
        private Button btnOutputFileBrowse;
        private CheckBox cbTranspileToLua;
        private CheckBox cbBruteForceUnknowns;
        private Button btnDonate;
        private Button btnBugReport;
        private Label label4;
        private TextBox tbWarningMessages;
        private CheckBox cbVisualTriggers;
    }
}
