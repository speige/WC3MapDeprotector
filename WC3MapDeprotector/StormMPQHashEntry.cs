namespace WC3MapDeprotector
{
    public struct StormMPQHashEntry
    {
        public uint FileIndex;
        public uint HashIndex;
        public ulong FullHash;
        public uint LeftPartialHash;
        public uint RightPartialHash;
        public string PseudoFileName;
        public string RealFileName;
        public string FileContentsMD5Hash;
        public bool IsUnknown;

        public string FileName
        {
            get
            {
                return RealFileName ?? PseudoFileName;
            }
        }
    }
}