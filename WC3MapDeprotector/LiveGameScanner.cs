using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WC3MapDeprotector
{
    public partial class frmLiveGameScanner : Form
    {
        protected const int WM_SCANDONE = 0x8000 + 2;

        protected CheatEngineLibrary _ce;
        protected bool _scanRunning = false;
        protected List<string> _scannedAddresses = new List<string>();

        protected readonly Action<string> _fileNameDiscoveredCallback;
        protected readonly List<string> _searchSuffixes;

        public frmLiveGameScanner(Action<string> fileNameDiscoveredCallback, Process process, List<string> searchSuffixes)
        {
            InitializeComponent();
            _ce = new CheatEngineLibrary();

            _fileNameDiscoveredCallback = fileNameDiscoveredCallback;
            _searchSuffixes = searchSuffixes;
            FormClosed += FrmLiveGameScanner_FormClosed;

            string processId = process.Id.ToString("X");
            _ce.iOpenProcess(processId);
            _ce.iInitMemoryScanner(Handle.ToInt32());
            _ce.iConfigScanner(Tscanregionpreference.scanInclude, Tscanregionpreference.scanExclude, Tscanregionpreference.scanExclude);
            //todo: design form, which gives progress & allows them to cancel

            var thread = new Thread(() =>
            {
                var scannedFileNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                while (true)
                {
                    foreach (var suffix in searchSuffixes)
                    {
                        //_logEvent($"Live Scanning for {extension}");
                        _scannedAddresses.Clear();
                        _scanRunning = true;
                        Invoke(() =>
                        {
                            //note: must be in "Invoke" so it can run on main UI thread, otherwise the incoming message can't be processed & app freezes
                            _ce.iNewScan();
                            _ce.iFirstScan(TScanOption.soExactValue, TVariableType.vtString, TRoundingType.rtRounded, suffix, "", "$0000000000000000", "$7FFFFFFFFFFFFFFF", false, false, false, false, TFastScanMethod.fsmAligned, "4");
                        });

                        while (_scanRunning)
                        {
                            Thread.Sleep(1000);
                        }

                        List<string> strings = new List<string>();
                        foreach (var address in _scannedAddresses)
                        {
                            var suffixAddress = ulong.Parse(address, System.Globalization.NumberStyles.HexNumber);
                            _ce.iProcessAddress(suffixAddress.ToString("X"), TVariableType.vtString, false, false, 1000, out var oldValue);
                            if (oldValue == null)
                            {
                                continue;
                            }

                            while (true)
                            {
                                suffixAddress--;
                                _ce.iProcessAddress(suffixAddress.ToString("X"), TVariableType.vtString, false, false, 1000, out var newValue);
                                if (newValue == null || !newValue.EndsWith(oldValue) || newValue.Length > 1000)
                                {
                                    break;
                                }

                                oldValue = newValue;
                            }

                            strings.Add(oldValue);
                        }
                        var result = strings.Distinct(StringComparer.InvariantCultureIgnoreCase).SelectMany(x => Regex.Matches(x, @".*\" + suffix, RegexOptions.IgnoreCase)).Select(x => x.Value).Distinct(StringComparer.InvariantCultureIgnoreCase).Where(x => !scannedFileNames.Contains(x)).ToList();
                        foreach (var fileName in result)
                        {
                            var scannedFileName = fileName;
                            var invalidFileNameChars = Regex.Match(fileName, @".*[""<>:|?*](.*)\" + suffix, RegexOptions.IgnoreCase);
                            if (invalidFileNameChars.Success)
                            {
                                scannedFileName = invalidFileNameChars.Groups[1].Value;
                            }
                            scannedFileNames.Add(scannedFileName);
                            _fileNameDiscoveredCallback(scannedFileName);
                        }
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void FrmLiveGameScanner_FormClosed(object sender, FormClosedEventArgs e)
        {
            _ce.Dispose();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SCANDONE)
            {
                int size = _ce.iGetBinarySize();
                _ce.iInitFoundList(TVariableType.vtString, size / 8, false, false, false, false);
                var itemCount = _ce.iCountAddressesFound();
                for (var i = 0; i < itemCount; i++)
                {
                    _ce.iGetAddress(i, out var address, out var value);
                    _scannedAddresses.Add(address);
                }

                _scanRunning = false;
            }
            else
            {
                base.WndProc(ref m);
            }
        }
    }
}
