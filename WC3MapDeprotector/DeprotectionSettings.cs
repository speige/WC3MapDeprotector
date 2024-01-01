namespace WC3MapDeprotector
{
    public class DeprotectionSettings
    {
        public bool TranspileJassToLUA { get; set; }
        public bool CreateVisualTriggers { get; set; }
        public bool BruteForceUnknowns { get; set; }
        public CancellationTokenSource BruteForceCancellationToken { get; } = new CancellationTokenSource();
    }
}