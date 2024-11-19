namespace WC3MapDeprotector
{
    partial class frmDisclaimer
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmDisclaimer));
            rtbDisclaimer = new RichTextBox();
            btnOkay = new Button();
            btnCancel = new Button();
            btnEasterEgg = new Button();
            SuspendLayout();
            // 
            // rtbDisclaimer
            // 
            rtbDisclaimer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rtbDisclaimer.Location = new Point(19, 20);
            rtbDisclaimer.Margin = new Padding(5);
            rtbDisclaimer.Name = "rtbDisclaimer";
            rtbDisclaimer.ReadOnly = true;
            rtbDisclaimer.Size = new Size(1217, 558);
            rtbDisclaimer.TabIndex = 0;
            rtbDisclaimer.Text = "";
            // 
            // btnOkay
            // 
            btnOkay.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnOkay.DialogResult = DialogResult.OK;
            btnOkay.Location = new Point(19, 596);
            btnOkay.Margin = new Padding(5);
            btnOkay.Name = "btnOkay";
            btnOkay.Size = new Size(227, 38);
            btnOkay.TabIndex = 1;
            btnOkay.Text = "I promise to be good";
            btnOkay.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(942, 596);
            btnCancel.Margin = new Padding(5);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(301, 38);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "I will be toxic to the community";
            btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnEasterEgg
            // 
            btnEasterEgg.Anchor = AnchorStyles.Bottom;
            btnEasterEgg.Location = new Point(580, 603);
            btnEasterEgg.Margin = new Padding(0);
            btnEasterEgg.Name = "btnEasterEgg";
            btnEasterEgg.Size = new Size(26, 31);
            btnEasterEgg.TabIndex = 3;
            btnEasterEgg.Text = "π";
            btnEasterEgg.TextAlign = ContentAlignment.TopCenter;
            btnEasterEgg.UseVisualStyleBackColor = true;
            btnEasterEgg.Click += btnEasterEgg_Click;
            // 
            // frmDisclaimer
            // 
            AcceptButton = btnCancel;
            AutoScaleDimensions = new SizeF(11F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1257, 648);
            Controls.Add(btnEasterEgg);
            Controls.Add(btnCancel);
            Controls.Add(btnOkay);
            Controls.Add(rtbDisclaimer);
            Font = new Font("Segoe UI", 14F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5);
            MinimumSize = new Size(700, 400);
            Name = "frmDisclaimer";
            Text = "Disclaimer : Please Read";
            Load += frmDisclaimer_Load;
            ResumeLayout(false);
        }

        #endregion

        private RichTextBox rtbDisclaimer;
        private Button btnOkay;
        private Button btnCancel;
        private Button btnEasterEgg;
    }
}