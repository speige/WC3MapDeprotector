using System.Runtime.InteropServices;

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

        protected bool GetFileNameHash(string fileName, out ulong hash)
        {
            hash = 0;

            if (StormLibrary.SFileOpenFileEx(_archiveHandle, fileName, 0, out var fileHandle))
            {
                IntPtr right = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr left = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                try
                {
                    if (IsFakeFile(fileName))
                    {
                        return false;
                    }

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
            if (StormLibrary.SFileHasFile(_archiveHandle, archiveFileName) && GetFileNameHash(archiveFileName, out var hash))
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


        protected bool IsFakeFile(string archiveFileName)
        {
            // todo: avoid 2nd extraction later by returning TempFileName?
            var tempFileName = Path.GetTempFileName();
            StormLibrary.SFileExtractFile(_archiveHandle, archiveFileName, tempFileName, 0);
            using (var reader = File.OpenRead(tempFileName))
            {
                return reader.Length == 0;
            }
        }

        public unsafe StormMPQArchive(string mpqFileName)
        {
            if (!StormLibrary.SFileOpenArchive(mpqFileName, 0, StormLibrary.SFileOpenArchiveFlags.AccessReadOnly, out _archiveHandle))
            {
                throw new Exception("Unable to open MPQ Archive");
            }

            var fileNames = new List<string>();
            var findFileHandle = StormLibrary.SFileFindFirstFile(_archiveHandle, "*", out var findData, null);
            try
            {
                do
                {
                    try
                    {
                        var fileName = MarshalByteArrayAsString(findData.cFileName);
                        fileNames.Add(fileName);
                    }
                    catch { }
                } while (StormLibrary.SFileFindNextFile(findFileHandle, out findData));
            }
            finally
            {
                StormLibrary.SFileFindClose(findFileHandle);
            }

            foreach (var fileName in fileNames)
            {
                if (IsFakeFile(fileName))
                {
                    continue;
                }

                if (GetFileNameHash(fileName, out var hash))
                {
                    _hashToFileName[hash] = new FileNameEntry() { FileName = fileName, IsUnknown = true };
                }
            }
        }

        public void Dispose()
        {
            StormLibrary.SFileCloseArchive(_archiveHandle);
        }
    }
}