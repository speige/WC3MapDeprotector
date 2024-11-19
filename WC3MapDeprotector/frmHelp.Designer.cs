namespace WC3MapDeprotector
{
    partial class frmHelp
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmHelp));
            rtbExplanation = new RichTextBox();
            SuspendLayout();
            // 
            // rtbExplanation
            // 
            rtbExplanation.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rtbExplanation.Location = new Point(19, 20);
            rtbExplanation.Margin = new Padding(5);
            rtbExplanation.Name = "rtbExplanation";
            rtbExplanation.Size = new Size(1217, 890);
            rtbExplanation.TabIndex = 0;
            rtbExplanation.Text = "";
            // 
            // frmHelp
            // 
            AutoScaleDimensions = new SizeF(11F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1257, 980);
            Controls.Add(rtbExplanation);
            Font = new Font("Segoe UI", 14F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5);
            MinimumSize = new Size(700, 400);
            Name = "frmHelp";
            Text = "Help";
            Load += frmDisclaimer_Load;
            ResumeLayout(false);
        }

        #endregion

        private RichTextBox rtbExplanation;
    }
}