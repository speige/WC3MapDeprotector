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
            fdInputFile = new OpenFileDialog();
            fdOutputFile = new SaveFileDialog();
            tcMain = new TabControl();
            tpDeprotector = new TabPage();
            tcLog = new TabControl();
            tpWarnings = new TabPage();
            tbWarningMessages = new TextBox();
            tpDebugLog = new TabPage();
            tbDebugLog = new TextBox();
            label5 = new Label();
            cbOutputFormat = new ComboBox();
            btnOutputFileBrowse = new Button();
            btnInputFileBrowse = new Button();
            label2 = new Label();
            label1 = new Label();
            btnRebuildMap = new Button();
            tbOutputFile = new TextBox();
            tbInputFile = new TextBox();
            tpUtilities = new TabPage();
            btnJassToLua = new Button();
            btnDiscord = new Button();
            btnYoutube = new Button();
            btnHelp = new Button();
            btnBugReport = new Button();
            btnDonate = new Button();
            tcMain.SuspendLayout();
            tpDeprotector.SuspendLayout();
            tcLog.SuspendLayout();
            tpWarnings.SuspendLayout();
            tpDebugLog.SuspendLayout();
            tpUtilities.SuspendLayout();
            SuspendLayout();
            // 
            // fdInputFile
            // 
            fdInputFile.Filter = "\"Warcraft 3 Maps\"|*.w3m; *.w3x;*.w3n";
            // 
            // fdOutputFile
            // 
            fdOutputFile.Filter = "\"Warcraft 3 Maps\"|*.w3m,*.w3x";
            // 
            // tcMain
            // 
            tcMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tcMain.Controls.Add(tpDeprotector);
            tcMain.Controls.Add(tpUtilities);
            tcMain.Location = new Point(0, 0);
            tcMain.Name = "tcMain";
            tcMain.SelectedIndex = 0;
            tcMain.Size = new Size(785, 556);
            tcMain.TabIndex = 34;
            tcMain.SelectedIndexChanged += tcMain_SelectedIndexChanged;
            // 
            // tpDeprotector
            // 
            tpDeprotector.Controls.Add(tcLog);
            tpDeprotector.Controls.Add(label5);
            tpDeprotector.Controls.Add(cbOutputFormat);
            tpDeprotector.Controls.Add(btnOutputFileBrowse);
            tpDeprotector.Controls.Add(btnInputFileBrowse);
            tpDeprotector.Controls.Add(label2);
            tpDeprotector.Controls.Add(label1);
            tpDeprotector.Controls.Add(btnRebuildMap);
            tpDeprotector.Controls.Add(tbOutputFile);
            tpDeprotector.Controls.Add(tbInputFile);
            tpDeprotector.Location = new Point(4, 34);
            tpDeprotector.Name = "tpDeprotector";
            tpDeprotector.Padding = new Padding(3);
            tpDeprotector.Size = new Size(777, 518);
            tpDeprotector.TabIndex = 0;
            tpDeprotector.Text = "Deprotector";
            tpDeprotector.UseVisualStyleBackColor = true;
            // 
            // tcLog
            // 
            tcLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tcLog.Controls.Add(tpWarnings);
            tcLog.Controls.Add(tpDebugLog);
            tcLog.Location = new Point(18, 245);
            tcLog.Name = "tcLog";
            tcLog.SelectedIndex = 0;
            tcLog.Size = new Size(743, 271);
            tcLog.TabIndex = 48;
            // 
            // tpWarnings
            // 
            tpWarnings.Controls.Add(tbWarningMessages);
            tpWarnings.Location = new Point(4, 34);
            tpWarnings.Name = "tpWarnings";
            tpWarnings.Padding = new Padding(3);
            tpWarnings.Size = new Size(735, 233);
            tpWarnings.TabIndex = 1;
            tpWarnings.Text = "Warnings";
            tpWarnings.UseVisualStyleBackColor = true;
            // 
            // tbWarningMessages
            // 
            tbWarningMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tbWarningMessages.Location = new Point(0, 0);
            tbWarningMessages.Margin = new Padding(5);
            tbWarningMessages.MinimumSize = new Size(0, 100);
            tbWarningMessages.Multiline = true;
            tbWarningMessages.Name = "tbWarningMessages";
            tbWarningMessages.ReadOnly = true;
            tbWarningMessages.ScrollBars = ScrollBars.Both;
            tbWarningMessages.Size = new Size(738, 293);
            tbWarningMessages.TabIndex = 26;
            // 
            // tpDebugLog
            // 
            tpDebugLog.Controls.Add(tbDebugLog);
            tpDebugLog.Location = new Point(4, 24);
            tpDebugLog.Name = "tpDebugLog";
            tpDebugLog.Padding = new Padding(3);
            tpDebugLog.Size = new Size(735, 243);
            tpDebugLog.TabIndex = 0;
            tpDebugLog.Text = "Debug Log";
            tpDebugLog.UseVisualStyleBackColor = true;
            // 
            // tbDebugLog
            // 
            tbDebugLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tbDebugLog.Location = new Point(0, 0);
            tbDebugLog.Margin = new Padding(5);
            tbDebugLog.MinimumSize = new Size(0, 100);
            tbDebugLog.Multiline = true;
            tbDebugLog.Name = "tbDebugLog";
            tbDebugLog.ReadOnly = true;
            tbDebugLog.ScrollBars = ScrollBars.Both;
            tbDebugLog.Size = new Size(738, 243);
            tbDebugLog.TabIndex = 24;
            // 
            // label5
            // 
            label5.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label5.AutoSize = true;
            label5.Location = new Point(452, 151);
            label5.Name = "label5";
            label5.Size = new Size(135, 25);
            label5.TabIndex = 44;
            label5.Text = "Output Format";
            // 
            // cbOutputFormat
            // 
            cbOutputFormat.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cbOutputFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cbOutputFormat.FormattingEnabled = true;
            cbOutputFormat.Items.AddRange(new object[] { "Reforged", "Classic / 1.26a" });
            cbOutputFormat.Location = new Point(593, 148);
            cbOutputFormat.Name = "cbOutputFormat";
            cbOutputFormat.Size = new Size(167, 33);
            cbOutputFormat.TabIndex = 43;
            cbOutputFormat.SelectedValueChanged += cbOutputFormat_SelectedValueChanged;
            // 
            // btnOutputFileBrowse
            // 
            btnOutputFileBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOutputFileBrowse.Image = (Image)resources.GetObject("btnOutputFileBrowse.Image");
            btnOutputFileBrowse.Location = new Point(694, 76);
            btnOutputFileBrowse.Margin = new Padding(5);
            btnOutputFileBrowse.Name = "btnOutputFileBrowse";
            btnOutputFileBrowse.Size = new Size(67, 55);
            btnOutputFileBrowse.TabIndex = 40;
            btnOutputFileBrowse.UseVisualStyleBackColor = true;
            btnOutputFileBrowse.Click += btnOutputFileBrowse_Click;
            // 
            // btnInputFileBrowse
            // 
            btnInputFileBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnInputFileBrowse.Image = (Image)resources.GetObject("btnInputFileBrowse.Image");
            btnInputFileBrowse.Location = new Point(694, 8);
            btnInputFileBrowse.Margin = new Padding(5);
            btnInputFileBrowse.Name = "btnInputFileBrowse";
            btnInputFileBrowse.Size = new Size(67, 55);
            btnInputFileBrowse.TabIndex = 39;
            btnInputFileBrowse.UseVisualStyleBackColor = true;
            btnInputFileBrowse.Click += btnInputFileBrowse_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(17, 86);
            label2.Margin = new Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new Size(105, 25);
            label2.TabIndex = 38;
            label2.Text = "Output File";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(17, 20);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(90, 25);
            label1.TabIndex = 37;
            label1.Text = "Input File";
            // 
            // btnRebuildMap
            // 
            btnRebuildMap.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRebuildMap.Location = new Point(592, 208);
            btnRebuildMap.Margin = new Padding(5);
            btnRebuildMap.Name = "btnRebuildMap";
            btnRebuildMap.Size = new Size(168, 38);
            btnRebuildMap.TabIndex = 36;
            btnRebuildMap.Text = "Rebuild Map";
            btnRebuildMap.UseVisualStyleBackColor = true;
            btnRebuildMap.Click += btnRebuildMap_Click;
            // 
            // tbOutputFile
            // 
            tbOutputFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tbOutputFile.Location = new Point(130, 86);
            tbOutputFile.Margin = new Padding(5);
            tbOutputFile.Name = "tbOutputFile";
            tbOutputFile.Size = new Size(554, 32);
            tbOutputFile.TabIndex = 35;
            tbOutputFile.TextChanged += tbOutputFile_TextChanged;
            // 
            // tbInputFile
            // 
            tbInputFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tbInputFile.Location = new Point(130, 20);
            tbInputFile.Margin = new Padding(5);
            tbInputFile.Name = "tbInputFile";
            tbInputFile.Size = new Size(554, 32);
            tbInputFile.TabIndex = 34;
            tbInputFile.TextChanged += tbInputFile_TextChanged;
            // 
            // tpUtilities
            // 
            tpUtilities.Controls.Add(btnJassToLua);
            tpUtilities.Location = new Point(4, 34);
            tpUtilities.Name = "tpUtilities";
            tpUtilities.Padding = new Padding(3);
            tpUtilities.Size = new Size(777, 518);
            tpUtilities.TabIndex = 1;
            tpUtilities.Text = "Random Utilities";
            tpUtilities.UseVisualStyleBackColor = true;
            // 
            // btnJassToLua
            // 
            btnJassToLua.Location = new Point(8, 6);
            btnJassToLua.Name = "btnJassToLua";
            btnJassToLua.Size = new Size(161, 45);
            btnJassToLua.TabIndex = 0;
            btnJassToLua.Text = "Jass to Lua";
            btnJassToLua.UseVisualStyleBackColor = true;
            btnJassToLua.Click += btnJassToLua_Click;
            // 
            // btnDiscord
            // 
            btnDiscord.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDiscord.Image = (Image)resources.GetObject("btnDiscord.Image");
            btnDiscord.Location = new Point(282, 569);
            btnDiscord.Name = "btnDiscord";
            btnDiscord.Size = new Size(125, 80);
            btnDiscord.TabIndex = 52;
            btnDiscord.Text = "My Discord";
            btnDiscord.TextAlign = ContentAlignment.BottomCenter;
            btnDiscord.TextImageRelation = TextImageRelation.ImageAboveText;
            btnDiscord.UseVisualStyleBackColor = true;
            btnDiscord.Click += btnDiscord_Click;
            // 
            // btnYoutube
            // 
            btnYoutube.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnYoutube.Image = (Image)resources.GetObject("btnYoutube.Image");
            btnYoutube.Location = new Point(139, 569);
            btnYoutube.Name = "btnYoutube";
            btnYoutube.Size = new Size(125, 80);
            btnYoutube.TabIndex = 51;
            btnYoutube.Text = "My Channel";
            btnYoutube.TextAlign = ContentAlignment.BottomCenter;
            btnYoutube.TextImageRelation = TextImageRelation.ImageAboveText;
            btnYoutube.UseVisualStyleBackColor = true;
            btnYoutube.Click += btnYoutube_Click;
            // 
            // btnHelp
            // 
            btnHelp.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnHelp.Image = (Image)resources.GetObject("btnHelp.Image");
            btnHelp.Location = new Point(665, 569);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(100, 80);
            btnHelp.TabIndex = 50;
            btnHelp.Text = "Help";
            btnHelp.TextAlign = ContentAlignment.BottomCenter;
            btnHelp.TextImageRelation = TextImageRelation.ImageAboveText;
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += btnHelp_Click;
            // 
            // btnBugReport
            // 
            btnBugReport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnBugReport.Image = (Image)resources.GetObject("btnBugReport.Image");
            btnBugReport.Location = new Point(516, 569);
            btnBugReport.Name = "btnBugReport";
            btnBugReport.Size = new Size(125, 80);
            btnBugReport.TabIndex = 49;
            btnBugReport.Text = "Bug Report";
            btnBugReport.TextAlign = ContentAlignment.BottomCenter;
            btnBugReport.TextImageRelation = TextImageRelation.ImageAboveText;
            btnBugReport.UseVisualStyleBackColor = true;
            btnBugReport.Click += btnBugReport_Click;
            // 
            // btnDonate
            // 
            btnDonate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDonate.Image = (Image)resources.GetObject("btnDonate.Image");
            btnDonate.Location = new Point(21, 569);
            btnDonate.Name = "btnDonate";
            btnDonate.Size = new Size(100, 80);
            btnDonate.TabIndex = 48;
            btnDonate.Text = "Donate";
            btnDonate.TextAlign = ContentAlignment.BottomCenter;
            btnDonate.TextImageRelation = TextImageRelation.ImageAboveText;
            btnDonate.UseVisualStyleBackColor = true;
            btnDonate.Click += btnDonate_Click;
            // 
            // frmMain
            // 
            AutoScaleDimensions = new SizeF(11F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 661);
            Controls.Add(btnDiscord);
            Controls.Add(btnYoutube);
            Controls.Add(btnHelp);
            Controls.Add(btnBugReport);
            Controls.Add(btnDonate);
            Controls.Add(tcMain);
            Font = new Font("Segoe UI", 14F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5);
            MinimumSize = new Size(800, 700);
            Name = "frmMain";
            Load += MainForm_Load;
            tcMain.ResumeLayout(false);
            tpDeprotector.ResumeLayout(false);
            tpDeprotector.PerformLayout();
            tcLog.ResumeLayout(false);
            tpWarnings.ResumeLayout(false);
            tpWarnings.PerformLayout();
            tpDebugLog.ResumeLayout(false);
            tpDebugLog.PerformLayout();
            tpUtilities.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private OpenFileDialog fdInputFile;
        private SaveFileDialog fdOutputFile;
        private TabControl tcMain;
        private TabPage tpDeprotector;
        private TabPage tpUtilities;
        private TabControl tcLog;
        private TabPage tpWarnings;
        private TextBox tbWarningMessages;
        private TabPage tpDebugLog;
        private TextBox tbDebugLog;
        private Label label5;
        private ComboBox cbOutputFormat;
        private Button btnOutputFileBrowse;
        private Button btnInputFileBrowse;
        private Label label2;
        private Label label1;
        private Button btnRebuildMap;
        private TextBox tbOutputFile;
        private TextBox tbInputFile;
        private Button btnDiscord;
        private Button btnYoutube;
        private Button btnHelp;
        private Button btnBugReport;
        private Button btnDonate;
        private Button btnJassToLua;
    }
}
