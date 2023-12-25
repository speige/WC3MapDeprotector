using System.Runtime.InteropServices;
using System.Security.Policy;

namespace WC3MapDeprotector
{
    public class StormMPQArchive : IDisposable
    {
        protected class FileNameEntry
        {
            public string FileName;
            public bool IsUnknown;
        }

        protected IntPtr _archiveHandle;
        protected Dictionary<uint, FileNameEntry> _leftHashToFileName = new Dictionary<uint, FileNameEntry>();
        protected Dictionary<uint, FileNameEntry> _rightHashToFileName = new Dictionary<uint, FileNameEntry>();
        protected Dictionary<ulong, FileNameEntry> _fullHashToFileName = new Dictionary<ulong, FileNameEntry>();

        public List<uint> AllLeftFileNameHashes
        {
            get
            {
                return _leftHashToFileName.Keys.ToList();
            }
        }

        public List<uint> AllRightFileNameHashes
        {
            get
            {
                return _rightHashToFileName.Keys.ToList();
            }
        }

        public List<ulong> AllFullFileNameHashes
        {
            get
            {
                return _fullHashToFileName.Keys.ToList();
            }
        }

        public List<uint> UnknownFileNameLeftHashes
        {
            get
            {
                return _leftHashToFileName.Where(x => x.Value.IsUnknown).Select(x => x.Key).ToList();
            }
        }

        public List<uint> UnknownFileNameRightHashes
        {
            get
            {
                return _rightHashToFileName.Where(x => x.Value.IsUnknown).Select(x => x.Key).ToList();
            }
        }

        public List<ulong> UnknownFileNameFullHashes
        {
            get
            {
                return _fullHashToFileName.Where(x => x.Value.IsUnknown).Select(x => x.Key).ToList();
            }
        }

        public Dictionary<ulong, string> DiscoveredFileNames
        {
            get
            {
                return _fullHashToFileName.Where(x => !x.Value.IsUnknown).ToDictionary(x => x.Key, x => x.Value.FileName);
            }
        }

        public bool ExtractFile(ulong archiveFileHash, string extractedFileName)
        {
            if (!_fullHashToFileName.TryGetValue(archiveFileHash, out var archiveFileName))
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(extractedFileName));
            return StormLibrary.SFileExtractFile(_archiveHandle, archiveFileName.FileName, extractedFileName, 0);
        }

        protected bool TryGetFileNameHash(string fileName, out uint leftHash, out uint rightHash, out ulong fullHash)
        {
            leftHash = 0;
            rightHash = 0;
            fullHash = 0;

            if (StormLibrary.SFileOpenFileEx(_archiveHandle, fileName, 0, out var fileHandle))
            {
                IntPtr right = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr left = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                try
                {
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoNameHash1, right, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoNameHash2, left, (uint)Marshal.SizeOf(typeof(uint)), out var _);

                    leftHash = (uint)Marshal.ReadInt32(left);
                    rightHash = (uint)Marshal.ReadInt32(right);
                    fullHash = ((ulong)leftHash << 32) | rightHash;
                    return true;
                }
                finally
                {
                    StormLibrary.SFileCloseFile(fileHandle);
                    Marshal.FreeHGlobal(left);
                    Marshal.FreeHGlobal(right);
                }
            }

            return false;
        }

        public bool DiscoverFile(string archiveFileName)
        {
            if (StormLibrary.SFileHasFile(_archiveHandle, archiveFileName) && TryGetFileNameHash(archiveFileName, out var leftHash, out var rightHash, out var fullHash))
            {
                _leftHashToFileName[leftHash] = new FileNameEntry() { FileName = archiveFileName, IsUnknown = false };
                _rightHashToFileName[rightHash] = new FileNameEntry() { FileName = archiveFileName, IsUnknown = false };
                _fullHashToFileName[fullHash] = new FileNameEntry() { FileName = archiveFileName, IsUnknown = false };
                return true;
            }

            return false;
        }

        protected unsafe string MarshalByteArrayAsString(byte* unsafeCString)
        {
            return Marshal.PtrToStringUTF8((nint)unsafeCString);
        }

        protected unsafe string MarshalByteArrayAsString(IntPtr ptr)
        {
            return MarshalByteArrayAsString((byte*)ptr.ToPointer());
        }


        public unsafe StormMPQArchive(string mpqFileName)
        {
            if (!StormLibrary.SFileOpenArchive(mpqFileName, 0, StormLibrary.SFileOpenArchiveFlags.AccessReadOnly, out _archiveHandle))
            {
                throw new Exception("Unable to open MPQ Archive");
            }

            var findFileHandle = StormLibrary.SFileFindFirstFile(_archiveHandle, "*", out var findData, null);
            try
            {
                do
                {
                    var fileName = MarshalByteArrayAsString(findData.cFileName);
                    if (TryGetFileNameHash(fileName, out var leftHash, out var rightHash, out var fullHash))
                    {
                        _leftHashToFileName[leftHash] = new FileNameEntry() { FileName = fileName, IsUnknown = true };
                        _rightHashToFileName[rightHash] = new FileNameEntry() { FileName = fileName, IsUnknown = true };
                        _fullHashToFileName[fullHash] = new FileNameEntry() { FileName = fileName, IsUnknown = true };
                    }
                } while (StormLibrary.SFileFindNextFile(findFileHandle, out findData));
            }
            finally
            {
                StormLibrary.SFileFindClose(findFileHandle);
            }
        }

        public void Dispose()
        {
            StormLibrary.SFileCloseArchive(_archiveHandle);
        }
    }
}