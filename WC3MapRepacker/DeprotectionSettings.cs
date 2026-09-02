namespace WC3MapRepacker
{
    public class DeprotectionSettings
    {
        public bool BruteForceUnknowns { get; set; }
        public CancellationTokenSource BruteForceCancellationToken { get; } = new CancellationTokenSource();
    }
}