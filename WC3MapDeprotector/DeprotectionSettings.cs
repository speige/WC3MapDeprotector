namespace WC3MapDeprotector
{
    public class DeprotectionSettings
    {
        public bool TranspileJassToLUA { get; set; }
        public bool CreateVisualTriggers { get; set; }
        public bool BruteForceUnknowns { get; set; }
        public CancellationTokenSource BruteForceCancellationToken { get; } = new CancellationTokenSource();
        public string WC3ExePath { get; set; } = @"c:\Program Files (x86)\Warcraft III\_retail_\x86_64\Warcraft III.exe";
    }
}