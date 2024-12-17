using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;

namespace WC3MapDeprotector
{
    public partial class frmMain : Form
    {
        protected bool _deprotectRunning = false;
        protected bool _cancel = false;
        protected bool _disclaimerApproved = false;
        private CancellationTokenSource _bruteForceCancellationToken;

        public frmMain()
        {
            //todo: add gear icon for advanced settings?
            InitializeComponent();
            WindowUITracker.Tracker.Track(this);
            this.Text = $"WC3MapDeprotector {Assembly.GetEntryAssembly().GetName().Version} by http://www.youtube.com/@ai-gamer";
        }

        private void BulkDeprotect()
        {
            string logFilePath = @"c:\temp\WC3MapDeprotector_bulkDeprotectLog.txt";
            string directory = null;
            Debugger.Break();
            foreach (var fileName in Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    //todo: copy debug/output logs to file
                    tbInputFile.Text = fileName;
                    tbInputFile_TextChanged(this, new EventArgs());

                    if (File.Exists(tbOutputFile.Text))
                    {
                        continue;
                    }

                    try
                    {
                        File.AppendAllText(logFilePath, "\r\rMapFileName: " + fileName + "\r");
                    }
                    catch { }

                    btnRebuildMap_Click(this, new EventArgs());
                    var task = Task.Run(async () =>
                    {
                        while (_deprotectRunning)
                        {
                            Thread.Sleep((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
                        }
                    });
                    task.Wait();

                    try
                    {
                        File.AppendAllText(logFilePath, tbDebugLog.Text);
                        File.AppendAllText(logFilePath, tbWarningMessages.Text);
                    }
                    catch { }
                }
                catch (Exception e)
                {
                    //todo: log to file
                    //swallow exception
                }
            }
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
            btnRebuildMap.Text = _deprotectRunning ? "Cancel" : "Rebuild Map";
            btnRebuildMap.Enabled = _deprotectRunning || File.Exists(tbInputFile.Text) && !string.IsNullOrWhiteSpace(tbOutputFile.Text);

            btnInputFileBrowse.Enabled = !_deprotectRunning;
            btnOutputFileBrowse.Enabled = !_deprotectRunning;
            tbInputFile.Enabled = !_deprotectRunning;
            tbOutputFile.Enabled = !_deprotectRunning;
            btnRebuildMap.TabStop = !_deprotectRunning;

            if (_deprotectRunning)
            {
                btnDonate.Focus();
            }
        }

        protected string PromptUserForFileInstallPath(string fileName, string defaultPath)
        {
            var result = defaultPath;
            while (!File.Exists(result))
            {
                using (OpenFileDialog fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = $"{fileName} not found. Please select install location.";
                    fileDialog.Filter = "Exe Files (*.exe)|*.exe";
                    fileDialog.FilterIndex = 1;
                    fileDialog.Multiselect = false;
                    fileDialog.InitialDirectory = Path.GetDirectoryName(defaultPath);
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;
                    fileDialog.FileName = fileName;

                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        result = fileDialog.FileName;
                    }
                }
            }

            return result;
        }

        protected void MainForm_Load(object sender, EventArgs e)
        {
            EnableControls();
            CheckForUpdates();

            UserSettings.WC3ExePath = PromptUserForFileInstallPath("Warcraft III.exe", UserSettings.WC3ExePath);
            UserSettings.WorldEditExePath = PromptUserForFileInstallPath("World Editor.exe", UserSettings.WorldEditExePath);
            cbOutputFormat.SelectedItem = "Reforged";

            if (DebugSettings.BulkDeprotect)
            {
                BeginInvoke(() =>
                {
                    BulkDeprotect();
                });
            }
        }

        protected void tbInputFile_TextChanged(object sender, EventArgs e)
        {
            EnableControls();
            try
            {
                tbOutputFile.Text = File.Exists(tbInputFile.Text) ? Path.Combine(Path.GetDirectoryName(tbInputFile.Text), "deprotected", Path.GetFileNameWithoutExtension(tbInputFile.Text) + "_deprotected" + Path.GetExtension(tbInputFile.Text)) : "";
            }
            catch
            {
                tbOutputFile.Text = "";
            }
        }

        protected void tbOutputFile_TextChanged(object sender, EventArgs e)
        {
            EnableControls();
        }

        protected void btnInputFileBrowse_Click(object sender, EventArgs e)
        {
            if (fdInputFile.ShowDialog() == DialogResult.OK)
            {
                if (fdInputFile.FileName.EndsWith(".w3n", StringComparison.InvariantCultureIgnoreCase))
                {
                    tbInputFile.Text = "";
                    MessageBox.Show("w3n file format is a custom campaign file with a separate embedded map file for each mission. You must split the files into separate w3x files first before running this tool. Please see \"Campaigns\" in Help document");
                    btnHelp_Click(sender, e);
                    return;
                }

                tbInputFile.Text = fdInputFile.FileName;
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
            if (_deprotectRunning)
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
            tcLog.SelectedIndex = 1;

            using (var form = new frmDisclaimer())
            {
                if (!_disclaimerApproved && form.ShowDialog() != DialogResult.OK)
                {
                    MessageBox.Show("Sorry, you can only deprotect a map if you promise not to be toxic to the community");
                    return;
                }
            }

            _disclaimerApproved = true;

            try
            {
                _cancel = false;
                _deprotectRunning = true;
                EnableControls();
                using (var deprotector = new Deprotector(tbInputFile.Text, tbOutputFile.Text, new DeprotectionSettings() { }, log =>
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
                    tcLog.SelectedIndex = 0;

                    var statusMessage = "";
                    if (deprotectionResult.CriticalWarningCount == 0 && deprotectionResult.UnknownFileCount == 0)
                    {
                        statusMessage = "Success!";

                        if (deprotectionResult.CountOfProtectionsFound == 0)
                        {
                            statusMessage += " (Original file may not have been protected)";
                        }
                    }
                    else
                    {
                        statusMessage = "Partial Success: Please read warnings log.";
                    }

                    if (DebugSettings.BenchmarkUnknownRecovery)
                    {
                        statusMessage = "Benchmark-only mode. No output map created.";
                    }

                    ForceWindowToFront();
                    System.Media.SystemSounds.Hand.Play();
                    if (!DebugSettings.BulkDeprotect)
                    {
                        MessageBox.Show(statusMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                ForceWindowToFront();
                System.Media.SystemSounds.Exclamation.Play();
                if (!DebugSettings.BulkDeprotect)
                {
                    MessageBox.Show(ex.Message, "ERROR");
                }
                tbDebugLog.AppendText(ex.Message);
                tbWarningMessages.AppendText(ex.Message);
            }
            finally
            {
                _deprotectRunning = false;
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

        private void cbOutputFormat_SelectedValueChanged(object sender, EventArgs e)
        {
            if (cbOutputFormat.SelectedItem != "Reforged")
            {
                using (var form = new frmGameVersionCompatability())
                {
                    form.ShowDialog();
                }
                cbOutputFormat.SelectedItem = "Reforged";
            }
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            using (var form = new frmHelp())
            {
                form.ShowDialog();
            }
        }

        private void btnYoutube_Click(object sender, EventArgs e)
        {
            Process.Start("explorer", "http://www.youtube.com/@ai-gamer");
        }

        private void btnDiscord_Click(object sender, EventArgs e)
        {
            using (var discord = new frmDiscord())
            {
                discord.ShowDialog();
            }
        }

        private void tcMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_deprotectRunning)
            {
                tcMain.SelectedIndex = 0;
            }
        }
    }
}
