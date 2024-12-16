namespace WC3MapDeprotector
{
    public partial class frmLiveGameScanner : Form
    {
        public delegate void Delegate();
        public event Delegate RestartGameRequested;

        public string WC3ExePath
        {
            get
            {
                return tbWC3ExePath.Text;
            }
            set
            {
                tbWC3ExePath.Text = value;
            }
        }

        public frmLiveGameScanner()
        {
            InitializeComponent();
        }

        public void UpdateLabels(StormMPQArchive archive)
        {
            tbUnknownFileCount.Text = archive.UnknownFileCount.ToString();
            tbFakeFileCount.Text = archive.FakeFileCount.ToString();
            tbDiscoveredFileCount.Text = archive.GetDiscoveredFileNames().Count.ToString();
        }

        public void RequestGameRestart()
        {
            if (RestartGameRequested != null)
            {
                try
                {
                    RestartGameRequested();
                }
                catch (Exception e)
                {
                    MessageBox.Show("Unable to start WC3 process: " + e.Message);
                }
            }
        }

        private void btnQuit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnRestartGame_Click(object sender, EventArgs e)
        {
            RequestGameRestart();
        }

        private void btnWC3ExePathBrowse_Click(object sender, EventArgs e)
        {
            fdOpen.FileName = WC3ExePath;
            if (fdOpen.ShowDialog() == DialogResult.OK && File.Exists(fdOpen.FileName))
            {
                WC3ExePath = fdOpen.FileName;
            }
        }
    }
}