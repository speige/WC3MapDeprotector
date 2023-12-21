using static WC3MapDeprotector.StormLibrary;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Security;

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

        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        protected _TMPQHash GetFileMetaData(string fileName)
        {
            IntPtr hashPointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(_TMPQHash)));
            IntPtr fileHandle = IntPtr.Zero;
            try
            {
                if (StormLibrary.SFileOpenFileEx(_archiveHandle, fileName, 0, out fileHandle))
                {
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoHashEntry, hashPointer, (uint)Marshal.SizeOf(typeof(_TMPQHash)), out var _);
                    return Marshal.PtrToStructure<_TMPQHash>(hashPointer);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(hashPointer);
                if (fileHandle != IntPtr.Zero)
                {
                    StormLibrary.SFileCloseFile(fileHandle);
                }
            }

            return default;
        }

        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        public bool DiscoverFile(string archiveFileName)
        {
            var result = StormLibrary.SFileHasFile(_archiveHandle, archiveFileName);
            if (result)
            {
                try
                {
                    var metaData = GetFileMetaData(archiveFileName);
                    _hashToFileName[metaData.dwName1] = new FileNameEntry() { FileName = archiveFileName, IsUnknown = false };
                }
                catch
                {
                    return false;
                }
            }
            return result;
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
                    try
                    {
                        var fileName = MarshalByteArrayAsString(findData.cFileName);
                        var hashEntry = GetFileMetaData(fileName);
                        _hashToFileName[hashEntry.dwName1] = new FileNameEntry() { FileName = fileName, IsUnknown = true };
                    }
                    catch { }
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