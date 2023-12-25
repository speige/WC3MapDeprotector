using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;

namespace WC3MapDeprotector
{
    public partial class MainForm : Form
    {
        protected bool _running = false;
        protected bool _cancel = false;
        protected bool _disclaimerApproved = false;
        private CancellationTokenSource _bruteForceCancellationToken;

        public MainForm()
        {
            //todo: add gear icon for advanced settings?
            InitializeComponent();
            WindowUITracker.Tracker.Track(this);
            this.Text = $"WC3MapDeprotector {Assembly.GetEntryAssembly().GetName().Version} by http://www.youtube.com/@ai-gamer";
        }

        protected class GitHubReleaseInfo
        {
            public string name { get; set; }
        }

        protected async Task<GitHubReleaseInfo> DownloadGitHubReleaseInfo()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
                var json = await client.GetStringAsync("https://api.github.com/repos/speige/WC3MapDeprotector/releases/latest");
                return JsonConvert.DeserializeObject<GitHubReleaseInfo>(json);
            }
        }

        protected async void CheckForUpdates()
        {
            try
            {
                var currentVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();
                var githubVersion = await DownloadGitHubReleaseInfo();

                if (!string.Equals(githubVersion.name.Trim(), currentVersion.Trim(), StringComparison.InvariantCultureIgnoreCase))
                {
                    if (MessageBox.Show("There is a new version of the app available. Would you like to download it?", "Update Available", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        Process.Start("explorer", "https://github.com/speige/WC3MapDeprotector/releases/latest/download/WC3MapDeprotector.zip");
                    }
                }
            }
            catch { }
        }

        protected void EnableControls()
        {
            btnRebuildMap.Text = _running ? "Cancel" : "Rebuild Map";
            btnRebuildMap.Enabled = _running || File.Exists(tbInputFile.Text) && !string.IsNullOrWhiteSpace(tbOutputFile.Text);

            btnInputFileBrowse.Enabled = !_running;
            btnOutputFileBrowse.Enabled = !_running;
            tbInputFile.Enabled = !_running;
            tbOutputFile.Enabled = !_running;
            cbTranspileToLua.Enabled = !_running;
            cbBruteForceUnknowns.Enabled = !_running;
            cbVisualTriggers.Enabled = !_running;
        }

        protected void MainForm_Load(object sender, EventArgs e)
        {
            EnableControls();
            CheckForUpdates();
        }

        protected void tbInputFile_TextChanged(object sender, EventArgs e)
        {
            EnableControls();
        }

        protected void tbOutputFile_TextChanged(object sender, EventArgs e)
        {
            EnableControls();
        }

        protected void btnInputFileBrowse_Click(object sender, EventArgs e)
        {
            if (fdInputFile.ShowDialog() == DialogResult.OK)
            {
                tbInputFile.Text = fdInputFile.FileName;
                tbOutputFile.Text = Path.Combine(Path.GetDirectoryName(tbInputFile.Text), "deprotected", Path.GetFileNameWithoutExtension(tbInputFile.Text) + "_deprotected" + Path.GetExtension(tbInputFile.Text));
                EnableControls();
            }
        }

        protected void btnOutputFileBrowse_Click(object sender, EventArgs e)
        {
            if (fdOutputFile.ShowDialog() == DialogResult.OK)
            {
                tbOutputFile.Text = fdOutputFile.FileName;
                EnableControls();
            }
        }

        protected async void btnRebuildMap_Click(object sender, EventArgs e)
        {
            if (_running)
            {
                _cancel = true;
                btnRebuildMap.Enabled = false;
                if (_bruteForceCancellationToken != null)
                {
                    _bruteForceCancellationToken.Cancel();
                }
                return;
            }

            tbDebugLog.Clear();
            tbWarningMessages.Clear();

            if (!_disclaimerApproved && new frmDisclaimer().ShowDialog() != DialogResult.OK)
            {
                MessageBox.Show("Sorry, you can only deprotect a map if you promise not to be toxic to the community");
                return;
            }

            _disclaimerApproved = true;

            try
            {
                _cancel = false;
                _running = true;
                EnableControls();
                using (var deprotector = new Deprotector(tbInputFile.Text, tbOutputFile.Text, new DeprotectionSettings() { TranspileJassToLUA = cbTranspileToLua.Checked, CreateVisualTriggers = cbVisualTriggers.Checked, BruteForceUnknowns = cbBruteForceUnknowns.Checked }, log =>
                {
                    BeginInvoke(() =>
                    {
                        tbDebugLog.AppendText(log + Environment.NewLine);
                    });

                    Application.DoEvents();
                    if (_cancel)
                    {
                        throw new Exception("User Cancelled");
                    }
                }))
                {
                    _bruteForceCancellationToken = deprotector.Settings.BruteForceCancellationToken;
                    var deprotectionResult = await deprotector.Deprotect();

                    if (deprotectionResult.WarningMessages.Count > 0)
                    {
                        tbWarningMessages.Lines = deprotectionResult.WarningMessages.ToArray();
                    }
                    else
                    {
                        tbWarningMessages.Text = "None";
                    }

                    var statusMessage = "";
                    if (deprotectionResult.CriticalWarningCount == 0)
                    {
                        statusMessage = "Success!";
                    }
                    else
                    {
                        statusMessage = "Partial Success: Please read warnings log.";
                    }

                    if (deprotectionResult.UnknownFileCount > 0)
                    {
                        statusMessage += " (" + deprotectionResult.UnknownFileCount + " unknown files could not be recovered)";
                    }

                    if (deprotectionResult.CountOfProtectionsFound == 0)
                    {
                        statusMessage += " (Original file may not have been protected)";
                    }

                    ForceWindowToFront();
                    System.Media.SystemSounds.Hand.Play();
                    MessageBox.Show(statusMessage);
                }
            }
            catch (Exception ex)
            {
                ForceWindowToFront();
                System.Media.SystemSounds.Exclamation.Play();
                MessageBox.Show(ex.Message, "ERROR");
                tbDebugLog.AppendText(ex.Message);
                tbWarningMessages.AppendText(ex.Message);
            }
            finally
            {
                _running = false;
                EnableControls();
            }
        }

        protected void btnDonate_Click(object sender, EventArgs e)
        {
            Process.Start("explorer", "https://github.com/sponsors/speige");
        }

        protected void btnBugReport_Click(object sender, EventArgs e)
        {
            Process.Start("explorer", "https://github.com/speige/WC3MapDeprotector/issues");
        }

        protected void ForceWindowToFront()
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }

            Activate();
        }
    }
}
