using System.Diagnostics;

namespace WC3MapDeprotector
{
    public partial class frmDisclaimer : Form
    {
        public frmDisclaimer()
        {
            InitializeComponent();
            WindowUITracker.Tracker.Track(this);
        }

        protected void frmDisclaimer_Load(object sender, EventArgs e)
        {
            rtbDisclaimer.LoadFile(Path.Combine(Directory.GetCurrentDirectory(), "Disclaimer.rtf"));
        }

        protected void btnEasterEgg_Click(object sender, EventArgs e)
        {
            Process.Start("explorer", "https://www.youtube.com/@ai-gamer");
        }
    }
}
