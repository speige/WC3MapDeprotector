using System.Diagnostics;

namespace WC3MapDeprotector
{
    public partial class frmGameVersionCompatability : Form
    {
        public frmGameVersionCompatability()
        {
            InitializeComponent();
            WindowUITracker.Tracker.Track(this);
        }

        protected void frmDisclaimer_Load(object sender, EventArgs e)
        {
            rtbExplanation.LoadFile(Path.Combine(Directory.GetCurrentDirectory(), "GameVersionCompatability.rtf"));
        }

        private void rtbExplanation_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Process.Start("explorer", e.LinkText);
        }
    }
}
