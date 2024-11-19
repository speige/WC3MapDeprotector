using System.Diagnostics;

namespace WC3MapDeprotector
{
    public partial class frmHelp : Form
    {
        public frmHelp()
        {
            InitializeComponent();
            WindowUITracker.Tracker.Track(this);
        }

        protected void frmDisclaimer_Load(object sender, EventArgs e)
        {
            rtbExplanation.LoadFile(Path.Combine(Directory.GetCurrentDirectory(), "Help.rtf"), RichTextBoxStreamType.RichText);
            rtbExplanation.ReadOnly = true;
        }
    }
}
