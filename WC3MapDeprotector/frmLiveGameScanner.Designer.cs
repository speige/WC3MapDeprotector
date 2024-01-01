namespace WC3MapDeprotector
{
    partial class frmLiveGameScanner
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmLiveGameScanner));
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            btnQuit = new Button();
            tbFakeFileCount = new TextBox();
            tbUnknownFileCount = new TextBox();
            tbDiscoveredFileCount = new TextBox();
            btnRestartGame = new Button();
            tbWC3ExePath = new TextBox();
            label5 = new Label();
            btnWC3ExePathBrowse = new Button();
            fdOpen = new OpenFileDialog();
            SuspendLayout();
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label1.Location = new Point(14, 9);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(664, 269);
            label1.TabIndex = 0;
            label1.Text = resources.GetString("label1.Text");
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            label2.AutoSize = true;
            label2.Location = new Point(386, 425);
            label2.Name = "label2";
            label2.Size = new Size(186, 25);
            label2.TabIndex = 1;
            label2.Text = "Unknown File Count:";
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            label3.AutoSize = true;
            label3.Location = new Point(429, 487);
            label3.Name = "label3";
            label3.Size = new Size(143, 25);
            label3.TabIndex = 2;
            label3.Text = "Fake File Count:";
            // 
            // label4
            // 
            label4.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            label4.AutoSize = true;
            label4.Location = new Point(421, 549);
            label4.Name = "label4";
            label4.Size = new Size(151, 25);
            label4.TabIndex = 3;
            label4.Text = "Discovered Files:";
            // 
            // btnQuit
            // 
            btnQuit.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnQuit.Location = new Point(494, 617);
            btnQuit.Name = "btnQuit";
            btnQuit.Size = new Size(184, 41);
            btnQuit.TabIndex = 4;
            btnQuit.Text = "Quit Testing";
            btnQuit.UseVisualStyleBackColor = true;
            btnQuit.Click += btnQuit_Click;
            // 
            // tbFakeFileCount
            // 
            tbFakeFileCount.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            tbFakeFileCount.Location = new Point(578, 487);
            tbFakeFileCount.Name = "tbFakeFileCount";
            tbFakeFileCount.Size = new Size(100, 32);
            tbFakeFileCount.TabIndex = 5;
            // 
            // tbUnknownFileCount
            // 
            tbUnknownFileCount.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            tbUnknownFileCount.Location = new Point(578, 425);
            tbUnknownFileCount.Name = "tbUnknownFileCount";
            tbUnknownFileCount.Size = new Size(100, 32);
            tbUnknownFileCount.TabIndex = 6;
            // 
            // tbDiscoveredFileCount
            // 
            tbDiscoveredFileCount.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            tbDiscoveredFileCount.Location = new Point(578, 549);
            tbDiscoveredFileCount.Name = "tbDiscoveredFileCount";
            tbDiscoveredFileCount.Size = new Size(100, 32);
            tbDiscoveredFileCount.TabIndex = 7;
            // 
            // btnRestartGame
            // 
            btnRestartGame.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnRestartGame.Location = new Point(14, 617);
            btnRestartGame.Name = "btnRestartGame";
            btnRestartGame.Size = new Size(184, 41);
            btnRestartGame.TabIndex = 8;
            btnRestartGame.Text = "Restart Warcraft III";
            btnRestartGame.UseVisualStyleBackColor = true;
            btnRestartGame.Click += btnRestartGame_Click;
            // 
            // tbWC3ExePath
            // 
            tbWC3ExePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tbWC3ExePath.Location = new Point(178, 301);
            tbWC3ExePath.Name = "tbWC3ExePath";
            tbWC3ExePath.Size = new Size(425, 32);
            tbWC3ExePath.TabIndex = 9;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(14, 305);
            label5.Name = "label5";
            label5.Size = new Size(160, 25);
            label5.TabIndex = 10;
            label5.Text = "Warcraft Exe Path";
            // 
            // btnWC3ExePathBrowse
            // 
            btnWC3ExePathBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnWC3ExePathBrowse.Image = (Image)resources.GetObject("btnWC3ExePathBrowse.Image");
            btnWC3ExePathBrowse.Location = new Point(611, 289);
            btnWC3ExePathBrowse.Margin = new Padding(5);
            btnWC3ExePathBrowse.Name = "btnWC3ExePathBrowse";
            btnWC3ExePathBrowse.Size = new Size(67, 55);
            btnWC3ExePathBrowse.TabIndex = 11;
            btnWC3ExePathBrowse.UseVisualStyleBackColor = true;
            btnWC3ExePathBrowse.Click += btnWC3ExePathBrowse_Click;
            // 
            // frmLiveGameScanner
            // 
            AutoScaleDimensions = new SizeF(11F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(692, 670);
            Controls.Add(btnWC3ExePathBrowse);
            Controls.Add(label5);
            Controls.Add(tbWC3ExePath);
            Controls.Add(btnRestartGame);
            Controls.Add(tbDiscoveredFileCount);
            Controls.Add(tbUnknownFileCount);
            Controls.Add(tbFakeFileCount);
            Controls.Add(btnQuit);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Font = new Font("Segoe UI", 14F);
            Margin = new Padding(5);
            Name = "frmLiveGameScanner";
            Text = "Live Game Scanner";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Button btnQuit;
        private TextBox tbFakeFileCount;
        private TextBox tbUnknownFileCount;
        private TextBox tbDiscoveredFileCount;
        private Button btnRestartGame;
        private TextBox tbWC3ExePath;
        private Label label5;
        private Button btnWC3ExePathBrowse;
        private OpenFileDialog fdOpen;
    }
}