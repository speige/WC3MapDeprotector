namespace WC3MapDeprotector
{
    public interface IMPQArchive : IDisposable
    {
        public List<ulong> AllFileNameHashes { get; }
        public List<ulong> UnknownFileNameHashes { get; }
        public Dictionary<ulong, string> DiscoveredFileNames { get; }
        bool DiscoverFile(string archiveFileName);
        bool ExtractFile(ulong archiveFileHash, string extractedFileName);
    }
}