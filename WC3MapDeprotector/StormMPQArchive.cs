using System.Runtime.InteropServices;
using System.Security.Policy;

namespace WC3MapDeprotector
{
    public class StormMPQArchive : IMPQArchive
    {
        protected class FileNameEntry
        {
            public string FileName;
            public bool IsUnknown;
        }

        protected IntPtr _archiveHandle;
        protected Dictionary<ulong, FileNameEntry> _hashToFileName = new Dictionary<ulong, FileNameEntry>();

        public List<ulong> AllFileNameHashes
        {
            get
            {
                return _hashToFileName.Keys.ToList();
            }
        }
        public List<ulong> UnknownFileNameHashes
        {
            get
            {
                return _hashToFileName.Where(x => x.Value.IsUnknown).Select(x => x.Key).ToList();
            }
        }

        public Dictionary<ulong, string> DiscoveredFileNames
        {
            get
            {
                return _hashToFileName.Where(x => !x.Value.IsUnknown).ToDictionary(x => x.Key, x => x.Value.FileName);
            }
        }

        public bool ExtractFile(ulong archiveFileHash, string extractedFileName)
        {
            if (!_hashToFileName.TryGetValue(archiveFileHash, out var archiveFileName))
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(extractedFileName));
            return StormLibrary.SFileExtractFile(_archiveHandle, archiveFileName.FileName, extractedFileName, 0);
        }

        protected bool TryGetFileNameHash(string fileName, out ulong hash)
        {
            hash = 0;

            if (StormLibrary.SFileOpenFileEx(_archiveHandle, fileName, 0, out var fileHandle))
            {
                IntPtr right = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr left = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                try
                {
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoNameHash1, right, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoNameHash2, left, (uint)Marshal.SizeOf(typeof(uint)), out var _);

                    hash = (((ulong)Marshal.ReadInt32(left)) << 32) | ((uint)Marshal.ReadInt32(right));
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
            if (StormLibrary.SFileHasFile(_archiveHandle, archiveFileName) && TryGetFileNameHash(archiveFileName, out var hash))
            {
                _hashToFileName[hash] = new FileNameEntry() { FileName = archiveFileName, IsUnknown = false };
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
                    if (TryGetFileNameHash(fileName, out var hash))
                    {
                        _hashToFileName[hash] = new FileNameEntry() { FileName = fileName, IsUnknown = true };
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