namespace WC3MapDeprotector
{
    public class MPQArchiveWrapper : IMPQArchive
    {
        protected IMPQArchive _mpqArchive;
        public List<ulong> AllFileNameHashes => _mpqArchive.AllFileNameHashes;
        public Dictionary<ulong, string> DiscoveredFileNames => _mpqArchive.DiscoveredFileNames;
        public List<ulong> UnknownFileNameHashes => _mpqArchive.UnknownFileNameHashes;

        public MPQArchiveWrapper(string mpqFileName)
        {
            try
            {
                _mpqArchive = new StormMPQArchive(mpqFileName);
            }
            catch
            {
                _mpqArchive = new War3NetMPQArchive(mpqFileName);
            }
        }

        public void Dispose()
        {
            _mpqArchive.Dispose();
        }

        public bool ExtractFile(ulong archiveFileHash, string extractedFileName)
        {
            return _mpqArchive.ExtractFile(archiveFileHash, extractedFileName);
        }

        public bool DiscoverFile(string archiveFileName)
        {
            return _mpqArchive.DiscoverFile(archiveFileName);
        }
    }
}