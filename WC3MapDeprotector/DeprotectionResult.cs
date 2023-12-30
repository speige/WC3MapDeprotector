namespace WC3MapDeprotector
{
    public class DeprotectionResult
    {
        public int CriticalWarningCount { get; set; }
        public int UnknownFileCount { get; set; }
        public int CountOfProtectionsFound { get; set; }
        public List<string> WarningMessages { get; set; }

        public DeprotectionResult()
        {
            WarningMessages = new List<string>();
        }
    }
}