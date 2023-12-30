using CSharpLua;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace WC3MapDeprotector
{
    public partial class StormMPQArchive : IDisposable
    {
        public const string UNKNOWN_FOLDER = "unknowns";
        public const string DISCOVERED_FOLDER = "discovered";
        protected IntPtr _archiveHandle;
        protected string _extractFolder;
        protected readonly Action<string> _logEvent;
        protected DeprotectionResult _deprotectionResult;

        //NOTE: Due to fake hash entries in some corrupted MPQs we can't use the MPQFullHash as a unique key, so we're created our own by calculating the MD5 hash of the extracted file instead
        protected Dictionary<uint, uint> _fileIndexToEncryptionKey = new Dictionary<uint, uint>();
        protected Dictionary<uint, string> _fileIndexToMd5 = new Dictionary<uint, string>();
        protected Dictionary<uint, HashSet<string>> _fileIndexToDiscoveredFileNames = new Dictionary<uint, HashSet<string>>();
        protected Dictionary<string, string> _md5ToPredictedExtension = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        protected Dictionary<ulong, Tuple<uint, uint>> _fullHashToPartialHashes = new Dictionary<ulong, Tuple<uint, uint>>();
        protected Dictionary<string, string> _discoveredFileNameToMD5 = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        protected Dictionary<ulong, string> _discoveredMPQFullHashToMD5 = new Dictionary<ulong, string>();

        //NOTE: If Map Maker legitimately put 2 identical files in archive, they will have the same MD5 hash, we only track the one we extracted most recently
        protected Dictionary<string, string> _md5ToLocalDiskFileName = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public bool HasFakeFiles { get; init; }

        public string GetPredictedFileExtension(string md5Hash)
        {
            return _md5ToPredictedExtension.TryGetValue(md5Hash, out var result) ? result : null;
        }

        [GeneratedRegex(@"^File([0-9]{8})$", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_PseudoFileName();
        protected bool TryParseFileIndexFromPseudoFileName(string pseudoFileName, out uint fileIndex)
        {
            if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(pseudoFileName)))
            {
                var match = Regex_PseudoFileName().Match(Path.GetFileNameWithoutExtension(pseudoFileName));
                if (match.Success && uint.TryParse(match.Groups[1].Value, out fileIndex))
                {
                    return true;
                }
            }

            fileIndex = 0;
            return false;
        }

        public bool IsPseudoFileName(string fileName)
        {
            return TryParseFileIndexFromPseudoFileName(fileName, out var fileIndex) && _fileIndexToMd5.ContainsKey(fileIndex);
        }

        public List<uint> MPQFileNameLeftHashes
        {
            get
            {
                return _fullHashToPartialHashes.Values.Select(x => x.Item1).ToList();
            }
        }

        public List<uint> MPQFileNameRightHashes
        {
            get
            {
                return _fullHashToPartialHashes.Values.Select(x => x.Item2).ToList();
            }
        }

        public List<ulong> MPQFileNameFullHashes
        {
            get
            {
                return _fullHashToPartialHashes.Keys.ToList();
            }
        }

        protected List<ulong> UnknownFileNameFullHashes
        {
            get
            {
                return _fullHashToPartialHashes.Keys.Where(x => !_discoveredMPQFullHashToMD5.ContainsKey(x)).ToList();
            }
        }

        public int FakeFileCount
        {
            get
            {
                return UnknownFileNameFullHashes.Count - UnknownFileCount;
            }
        }

        public int UnknownFileCount
        {
            get
            {
                return _fileIndexToMd5.Keys.Where(x => !_fileIndexToDiscoveredFileNames.ContainsKey(x)).Count();
            }
        }

        public bool HasUnknownHashes
        {
            get
            {
                return UnknownFileNameFullHashes.Count > 0;
            }
        }

        public List<string> DiscoveredFileNames
        {
            get
            {
                return _discoveredFileNameToMD5.Keys.ToList();
            }
        }

        protected bool TryExtractFile(string archiveFileName, string localDiskFileName, out string md5Hash, out uint encryptionKey)
        {
            md5Hash = null;

            var extractedPath = Path.Combine(_extractFolder, localDiskFileName);
            try
            {
                if (ExtractFile(archiveFileName, extractedPath, out encryptionKey))
                {
                    bool isEmpty = false;
                    string predictedExtension = "";
                    using (var stream = File.OpenRead(extractedPath))
                    {
                        isEmpty = stream.Length == 0;
                        if (!isEmpty)
                        {
                            md5Hash = CalculateMD5(stream);
                            _md5ToLocalDiskFileName[md5Hash] = localDiskFileName;
                            if (!_md5ToPredictedExtension.TryGetValue(md5Hash, out predictedExtension))
                            {
                                stream.Position = 0;
                                predictedExtension = StormMPQArchiveExtensions.PredictUnknownFileExtension(stream) ?? "";
                                if (string.IsNullOrWhiteSpace(predictedExtension) && !TryParseFileIndexFromPseudoFileName(archiveFileName, out var _))
                                {
                                    predictedExtension = Path.GetExtension(archiveFileName);
                                }
                                if (string.IsNullOrWhiteSpace(predictedExtension))
                                {
                                    predictedExtension = Path.GetExtension(localDiskFileName).Replace(".xxx", "", StringComparison.InvariantCultureIgnoreCase);
                                    if (!string.IsNullOrWhiteSpace(predictedExtension))
                                    {
                                        DebugSettings.Warn("Delete code which uses StormLib extension if this breakpoint is never hit");
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(predictedExtension) && (predictedExtension.Length - predictedExtension.Replace(".", "").Length) > 1)
                                {
                                    DebugSettings.Warn("Bug in PredictUnknownFileExtension");
                                }

                                _md5ToPredictedExtension[md5Hash] = predictedExtension;
                            }
                        }
                    }

                    if (!isEmpty)
                    {
                        var newFileName = Path.ChangeExtension(extractedPath, predictedExtension);
                        if (!extractedPath.Equals(newFileName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            _md5ToLocalDiskFileName[md5Hash] = newFileName;
                            File.Move(extractedPath, newFileName);
                        }

                        return true;
                    }
                }
            }
            catch { }

            File.Delete(extractedPath);
            encryptionKey = 0;
            return false;
        }

        protected unsafe bool ExtractFile(string archiveFileName, string extractedFileName, out uint encryptionKey)
        {
            //NOTE: can't call SFileExtractFile because it hides the encryption key
            if (StormLibrary.SFileOpenFileEx(_archiveHandle, archiveFileName, 0, out var fileHandle))
            {
                IntPtr ptrEncryptionKey = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                try
                {
                    var buffer = new byte[1];
                    fixed (byte* ptrBuffer = &buffer[1])
                    {
                        NativeOverlapped overlapped = default;
                        StormLibrary.SFileReadFile(fileHandle, new IntPtr(ptrBuffer), 1, out var bytesTransferred, ref overlapped);
                    }

                    //NOTE: encryptionKey is based on real file name. SFileOpenFileEx will have blank encryption key for psuedoFileName. SFileReadFile attempts to crack the encryption key, but it's only stored on the current filehandle. So, we have to read 1 byte to get key for later before calling normal SFileExtractFile
                    //NOTE: If encryptionKey is blank, extracted file will have garbage bytes. We can still use MD5 as unique key, but once we discover real filename we need to re-extract & update MD5s. If extracted with fake file name, extracted file will still be garbage.
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoEncryptionKey, ptrEncryptionKey, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    encryptionKey = (uint)Marshal.ReadInt32(ptrEncryptionKey);

                    Directory.CreateDirectory(Path.GetDirectoryName(extractedFileName));
                    return StormLibrary.SFileExtractFile(_archiveHandle, archiveFileName, extractedFileName, 0);
                }
                finally
                {
                    StormLibrary.SFileCloseFile(fileHandle);
                    Marshal.FreeHGlobal(ptrEncryptionKey);
                }
            }

            encryptionKey = 0;
            return false;
        }

        protected string CalculatePseudoFileName(uint fileIndex)
        {
            if (!_fileIndexToMd5.TryGetValue(fileIndex, out var md5Hash) || !_md5ToPredictedExtension.TryGetValue(md5Hash, out var extension))
            {
                extension = "";
            }

            return $"File{fileIndex.ToString("00000000")}{extension}";
        }

        protected bool TryGetHashByFileIndex(uint fileIndex, out string pseudoFileName, out uint hashIndex, out uint leftHash, out uint rightHash, out ulong fullHash)
        {
            pseudoFileName = CalculatePseudoFileName(fileIndex);
            return TryGetHashByFilename(pseudoFileName, out var _, out hashIndex, out leftHash, out rightHash, out fullHash, out var _);
        }

        protected bool TryGetFileIndexByFilename(string fileName, out uint fileIndex, out uint encryptionKey)
        {
            //NOTE: encryptionKey will only be available for a DiscoveredFile, not a PseudoFileName(FileIndex).
            if (StormLibrary.SFileOpenFileEx(_archiveHandle, fileName, 0, out var fileHandle))
            {
                IntPtr ptrFileIndex = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr ptrEncryptionKey = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                try
                {
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoFileIndex, ptrFileIndex, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoEncryptionKey, ptrEncryptionKey, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    fileIndex = (uint)Marshal.ReadInt32(ptrFileIndex);
                    encryptionKey = (uint)Marshal.ReadInt32(ptrEncryptionKey);
                    return true;

                }
                finally
                {
                    StormLibrary.SFileCloseFile(fileHandle);
                    Marshal.FreeHGlobal(ptrFileIndex);
                    Marshal.FreeHGlobal(ptrEncryptionKey);
                }
            }

            fileIndex = 0;
            encryptionKey = 0;
            return false;
        }

        protected bool TryGetHashByFilename(string fileName, out uint fileIndex, out uint hashIndex, out uint leftHash, out uint rightHash, out ulong fullHash, out uint encryptionKey)
        {
            if (StormLibrary.SFileOpenFileEx(_archiveHandle, fileName, 0, out var fileHandle))
            {
                IntPtr ptrFileIndex = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr ptrHashIndex = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr ptrRightHash = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr ptrLeftHash = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr ptrEncryptionKey = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                try
                {
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoFileIndex, ptrFileIndex, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoHashIndex, ptrHashIndex, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoNameHash1, ptrRightHash, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoNameHash2, ptrLeftHash, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoEncryptionKey, ptrEncryptionKey, (uint)Marshal.SizeOf(typeof(uint)), out var _);

                    fileIndex = (uint)Marshal.ReadInt32(ptrFileIndex);
                    hashIndex = (uint)Marshal.ReadInt32(ptrHashIndex);
                    leftHash = (uint)Marshal.ReadInt32(ptrLeftHash);
                    rightHash = (uint)Marshal.ReadInt32(ptrRightHash);
                    fullHash = ((ulong)leftHash << 32) | rightHash;
                    encryptionKey = (uint)Marshal.ReadInt32(ptrEncryptionKey);
                    return true;
                }
                finally
                {
                    StormLibrary.SFileCloseFile(fileHandle);
                    Marshal.FreeHGlobal(ptrFileIndex);
                    Marshal.FreeHGlobal(ptrHashIndex);
                    Marshal.FreeHGlobal(ptrLeftHash);
                    Marshal.FreeHGlobal(ptrRightHash);
                    Marshal.FreeHGlobal(ptrEncryptionKey);
                }
            }

            fileIndex = 0;
            hashIndex = 0;
            leftHash = 0;
            rightHash = 0;
            fullHash = 0;
            encryptionKey = 0;
            return false;
        }
        
        protected object _discoverFileLock = new object();
        public bool DiscoverFile(string archiveFileName, out string md5Hash)
        {
            if (_discoveredFileNameToMD5.TryGetValue(archiveFileName, out md5Hash))
            {
                return true;
            }

            if (TryParseFileIndexFromPseudoFileName(archiveFileName, out var fileIndex) && _fileIndexToMd5.TryGetValue(fileIndex, out md5Hash))
            {
                return true;
            }

            if (StormLibrary.SFileHasFile(_archiveHandle, archiveFileName) && TryGetFileIndexByFilename(archiveFileName, out fileIndex, out var encryptionKey))
            {                
                lock (_discoverFileLock)
                {
                    if (!_fileIndexToMd5.TryGetValue(fileIndex, out md5Hash))
                    {
                        _fullHashToPartialHashes.Remove(MPQFullHash.Calculate(archiveFileName), out var _);

                        //todo: test extraction to stream with predict extension to see if it's ever a real file
                        if (!HasFakeFiles)
                        {
                            DebugSettings.Warn("Fix FakeFile detection");
                        }

                        //fake file because it wasn't on initial enumerated list
                        return false;
                    }

                    var pseudoFileName = Path.Combine(UNKNOWN_FOLDER, CalculatePseudoFileName(fileIndex));
                    var pseudoFileFullPath = Path.Combine(_extractFolder, pseudoFileName);

                    if (_fileIndexToEncryptionKey[fileIndex] != encryptionKey)
                    {
                        //Original encryption key detected incorrectly
                        File.Delete(pseudoFileFullPath);
                        if (!TryExtractFile(archiveFileName, pseudoFileFullPath, out md5Hash, out var newEncryptionKey))
                        {
                            return false;
                        }

                        pseudoFileName = Path.Combine(UNKNOWN_FOLDER, CalculatePseudoFileName(fileIndex));
                        pseudoFileFullPath = Path.Combine(_extractFolder, pseudoFileName);
                        _fileIndexToEncryptionKey[fileIndex] = encryptionKey;
                        _fileIndexToMd5[fileIndex] = md5Hash;
                    }

                    var extractedPath = Path.Combine(_extractFolder, DISCOVERED_FOLDER, archiveFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(extractedPath));

                    var alreadyDiscovered = _fileIndexToDiscoveredFileNames.TryGetValue(fileIndex, out var alreadyDiscoveredFileNames);
                    if (!alreadyDiscovered)
                    {
                        _fileIndexToDiscoveredFileNames[fileIndex] = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    }

                    if (alreadyDiscovered && _md5ToPredictedExtension.TryGetValue(md5Hash, out var predictedExtension))
                    {
                        //Two hashes pointing to same real file. Most likely one is a fake filename.

                        //todo: document old/new file names then add breakpoints to initial file extraction in construction to find clues that would help me identify fake MPQFullHashes to exclude from list
                        if (!HasFakeFiles)
                        {
                            DebugSettings.Warn("Fix FakeFile detection");
                        }

                        alreadyDiscoveredFileNames.Add(archiveFileName);
                        var filesMatchingPredictedExtension = new HashSet<string>(alreadyDiscoveredFileNames.Where(x => predictedExtension.Equals(Path.GetExtension(x), StringComparison.InvariantCultureIgnoreCase)), StringComparer.InvariantCultureIgnoreCase);
                        var filesNotMatchingPredictedExtension = new HashSet<string>(alreadyDiscoveredFileNames.Except(filesMatchingPredictedExtension, StringComparer.InvariantCultureIgnoreCase), StringComparer.InvariantCultureIgnoreCase);

                        if (!filesMatchingPredictedExtension.Any())
                        {
                            //todo: keep this list in _deprotectionResult and wait til end to create warning messages, so we don't give false alarms if real name is discovered after continual deep searching.

                            //NOTE: since we're keeping both files, _fileIndexToDiscoveredFileName will only have the most recently extracted file name
                            _deprotectionResult.WarningMessages.Add($"Files {filesNotMatchingPredictedExtension.Aggregate((x, y) => $"{x}, {y}")} were all extracted from the same file index in the MPQ Archive. This probably means some of the file names are fake. Couldn't determine which one was real, so all were kept. You may want to do testing to determine the fake file names and delete them.");
                        }
                        else
                        {
                            var currentFileLocation = Path.Combine(_extractFolder, _md5ToLocalDiskFileName[md5Hash]);
                            if (filesNotMatchingPredictedExtension.Contains(archiveFileName))
                            {
                                _md5ToLocalDiskFileName[md5Hash] = Path.Combine(DISCOVERED_FOLDER, filesMatchingPredictedExtension.First());
                            }
                            else
                            {
                                if (filesMatchingPredictedExtension.Count == 1)
                                {
                                    File.Move(currentFileLocation, pseudoFileFullPath);
                                }
                                else
                                {
                                    File.Copy(currentFileLocation, pseudoFileFullPath);
                                }

                                _md5ToLocalDiskFileName[md5Hash] = pseudoFileName;
                            }

                            foreach (var fakeFileName in filesNotMatchingPredictedExtension)
                            {
                                _logEvent($"Multiple fileNames found for same file. {fakeFileName} had different extension than predicted {predictedExtension}, assuming it's a fake name and deleting it.");
                                var oldMPQHash = MPQFullHash.Calculate(fakeFileName);
                                _fullHashToPartialHashes.Remove(oldMPQHash, out var _);
                                _discoveredFileNameToMD5.Remove(fakeFileName, out var _);
                                _discoveredMPQFullHashToMD5.Remove(oldMPQHash, out var _);
                                File.Delete(Path.Combine(_extractFolder, DISCOVERED_FOLDER, fakeFileName));
                            }

                            if (filesNotMatchingPredictedExtension.Contains(archiveFileName))
                            {
                                return false;
                            }
                        }
                    }

                    _fileIndexToDiscoveredFileNames[fileIndex].Add(archiveFileName);
                    var mpqFullHash = MPQFullHash.Calculate(archiveFileName);
                    _discoveredFileNameToMD5[archiveFileName] = md5Hash;
                    _discoveredMPQFullHashToMD5[mpqFullHash] = md5Hash;

                    if (File.Exists(pseudoFileFullPath))
                    {
                        File.Move(pseudoFileFullPath, extractedPath);
                        _md5ToLocalDiskFileName[md5Hash] = Path.Combine(DISCOVERED_FOLDER, archiveFileName);
                    }
                    else
                    {
                        //NOTE: This happens when a fake MPQFullHash entry points to a legitimiate FileIndex and we can't determine which name is real & which is fake, so we duplicate the file
                        var existingFileName = _md5ToLocalDiskFileName[md5Hash];
                        File.Copy(Path.Combine(_extractFolder, existingFileName), extractedPath);
                    }

                    return true;
                }
            }

            md5Hash = null;
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

        protected string CalculateMD5(Stream stream)
        {
            using (var md5 = MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
            }
        }

        public static T[] MarshalUnmanagedArrayToStruct<T>(IntPtr unmanagedArray, int length)
        {
            var size = Marshal.SizeOf(typeof(T));
            var result = new T[length];

            for (var idx = 0; idx < length; idx++)
            {
                result[idx] = Marshal.PtrToStructure<T>(new IntPtr(unmanagedArray.ToInt64() + idx * size));
            }

            return result;
        }

        public unsafe StormMPQArchive(string mpqFileName, string extractFolder, Action<string> logEvent, DeprotectionResult deprotectionResult)
        {
            _extractFolder = extractFolder;
            _logEvent = logEvent;
            _deprotectionResult = deprotectionResult;
            if (!StormLibrary.SFileOpenArchive(mpqFileName, 0, StormLibrary.SFileOpenArchiveFlags.AccessReadOnly, out _archiveHandle))
            {
                throw new Exception("Unable to open MPQ Archive");
            }

            _logEvent("Opening MPQ Archive");
            var findFileHandle = StormLibrary.SFileFindFirstFile(_archiveHandle, "*", out var findData, null);
            try
            {
                _logEvent("Enumerating contents");
                do
                {
                    var fileName = MarshalByteArrayAsString(findData.cFileName);

                    if (TryGetHashByFilename(fileName, out var fileIndex, out var hashIndex, out var mpqLeftPartialHash, out var mpqRightPartialHash, out var mpqFullHash, out var _))
                    {
                        if (_fileIndexToMd5.ContainsKey(fileIndex))
                        {
                            continue;
                        }

                        string pseudoFileName;
                        string realFileName = null;

                        var isUnknown = MPQFullHash.Calculate(fileName) != mpqFullHash;
                        if (isUnknown)
                        {
                            pseudoFileName = fileName;
                        }
                        else
                        {
                            pseudoFileName = CalculatePseudoFileName(fileIndex);
                            realFileName = fileName;
                        }

                        var extractedFileName = Path.Combine(isUnknown ? UNKNOWN_FOLDER : DISCOVERED_FOLDER, realFileName ?? pseudoFileName);
                        if (TryExtractFile(fileName, extractedFileName, out var fileContentsMD5Hash, out var encryptionKey))
                        {
                            _fileIndexToEncryptionKey[fileIndex] = encryptionKey;

                            if (!isUnknown)
                            {
                                mpqFullHash = MPQFullHash.Calculate(realFileName);
                                _fileIndexToDiscoveredFileNames[fileIndex] = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { realFileName };
                                _fullHashToPartialHashes[mpqFullHash] = MPQFullHash.SplitValue(mpqFullHash);
                                _discoveredMPQFullHashToMD5[mpqFullHash] = fileContentsMD5Hash;
                                _discoveredFileNameToMD5[realFileName] = fileContentsMD5Hash;
                            }

                            _fileIndexToMd5[fileIndex] = fileContentsMD5Hash;
                            _logEvent($"Extracted File: {extractedFileName}");
                        }
                    }
                    else
                    {
                        DebugSettings.Warn("Unable to get hash for file from MPQ");
                    }
                } while (StormLibrary.SFileFindNextFile(findFileHandle, out findData));

                _logEvent("Extraction Completed");


                uint hashTableEntryCount;
                IntPtr ptrHashtableSize = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                try
                {
                    StormLibrary.SFileGetFileInfo(_archiveHandle, StormLibrary.SFileInfoClass.SFileMpqHashTableSize, ptrHashtableSize, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    hashTableEntryCount = (uint)Marshal.ReadInt32(ptrHashtableSize);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptrHashtableSize);
                }

                StormLibrary.TMPQHash[] hashTable;
                var hashTableMemorySize = (uint)(hashTableEntryCount * Marshal.SizeOf(typeof(StormLibrary.TMPQHash)));
                IntPtr ptrHashtable = Marshal.AllocHGlobal((int)hashTableMemorySize);
                try
                {
                    StormLibrary.SFileGetFileInfo(_archiveHandle, StormLibrary.SFileInfoClass.SFileMpqHashTable, ptrHashtable, hashTableMemorySize, out var _);
                    hashTable = MarshalUnmanagedArrayToStruct<StormLibrary.TMPQHash>(ptrHashtable, (int)hashTableEntryCount);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptrHashtable);
                }

                var validHashEntries = hashTable.Where(x => _fileIndexToMd5.ContainsKey(x.dwBlockIndex)).ToList();
                foreach (var hash in validHashEntries)
                {
                    _fullHashToPartialHashes[MPQFullHash.GetValue(hash.dwName2, hash.dwName1)] = new Tuple<uint, uint>(hash.dwName2, hash.dwName1);
                }
                HasFakeFiles = validHashEntries.GroupBy(x => x.dwBlockIndex).Any(x => x.Count() > 1);

                if (HasFakeFiles)
                {
                    _deprotectionResult.WarningMessages.Add("MPQ Archive has fake files, it's possible some names were detected incorrectly. Safest option is to run live game of map i nwarcraft3 with scanner attached which scans memory and local file access.");

                    if (!mpqFileName.Contains("lusin", StringComparison.InvariantCultureIgnoreCase))
                    {
                        DebugSettings.Warn("Found a new map with fake files. Do extra testing");
                    }
                }
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