using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace WC3MapDeprotector
{
    public class StormMPQArchive : IDisposable
    {
        const string UNKNOWN_FOLDER = "unknowns";
        protected IntPtr _archiveHandle;
        //protected uint _fileCount;
        protected string _extractFolder;
        protected readonly Action<string> _logEvent;

        //protected Dictionary<uint, StormMPQHashEntry> _fileTable = new Dictionary<uint, StormMPQHashEntry>();

        protected Dictionary<string, string> _md5ToPseudoFileName = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        protected Dictionary<string, string> _md5ToLocalDiskFileName = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        protected Dictionary<string, string> _md5ToPredictedExtension = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        protected Dictionary<string, string> _pseudoFileNamesWithoutExtensionToMD5 = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        //NOTE: Due to bug in StormLibrary we can't use the MPQFullHash as a unique key, so we're created our own by calculating the MD5 hash instead. https://github.com/ladislav-zezula/StormLib/issues/314
        //protected Dictionary<ulong, Tuple<uint, uint>> _fakeHashToPartialHashes = new Dictionary<ulong, Tuple<uint, uint>>();

        protected Dictionary<ulong, Tuple<uint, uint>> _fullHashToPartialHashes = new Dictionary<ulong, Tuple<uint, uint>>();
        protected Dictionary<string, string> _discoveredFileNamesToMD5 = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        protected Dictionary<ulong, string> _discoveredMPQFullHashesToMD5 = new Dictionary<ulong, string>();

        public string GetPredictedFileExtension(string md5Hash)
        {
            return _md5ToPredictedExtension.TryGetValue(md5Hash, out var result) ? result : null;
        }

        public bool IsPseudoFileName(string fileName)
        {
            return _pseudoFileNamesWithoutExtensionToMD5.ContainsKey(Path.GetFileNameWithoutExtension(fileName));
        }

        public List<uint> UnknownFileNameLeftHashes
        {
            get
            {
                return UnknownFileNameFullHashes.Select(x => _fullHashToPartialHashes[x].Item1).ToList();
            }
        }

        public List<uint> UnknownFileNameRightHashes
        {
            get
            {
                return UnknownFileNameFullHashes.Select(x => _fullHashToPartialHashes[x].Item2).ToList();
            }
        }

        public List<ulong> UnknownFileNameFullHashes
        {
            get
            {
                return _fullHashToPartialHashes.Keys.Where(x => !_discoveredMPQFullHashesToMD5.ContainsKey(x)).ToList();
            }
        }

        public List<string> DiscoveredFileNames
        {
            get
            {
                return _discoveredFileNamesToMD5.Keys.ToList();
            }
        }


        /*
        public List<uint> AllLeftFileNameHashes
        {
            get
            {
                return _fullHashToPartialHashes.Select(x => x.Value.Item1).ToList();
            }
        }

        public List<uint> AllRightFileNameHashes
        {
            get
            {
                return _fullHashToPartialHashes.Select(x => x.Value.Item2).ToList();
            }
        }

        public List<ulong> AllFullFileNameHashes
        {
            get
            {
                return _internalFileNameToFullHash.Values.ToList();
            }
        }

        public List<string> AllInternalFileNames
        {
            get
            {
                return _internalFileNameToFullHash.Keys.ToList();
            }
        }

        public string ConvertDiscoveredNameToInternalName(string discoveredFileName)
        {
            return _internalFileNameToDiscoveredName.FirstOrDefault(x => x.Value.Equals(discoveredFileName, StringComparison.InvariantCultureIgnoreCase)).Key;
        }

        public string ConvertInternalNameToDiscoveredName(string internalFileName)
        {
            if (_internalFileNameToDiscoveredName.TryGetValue(internalFileName, out var discoveredFileName))
            {
                return discoveredFileName;
            }

            return null;
        }

        public bool TryGetInternalMetaData(string internalFileName, out ulong fullHash, out uint leftPartialHash, out uint rightPartialHash)
        {
            if (!_internalFileNameToFullHash.TryGetValue(internalFileName, out fullHash))
            {
                leftPartialHash = 0;
                rightPartialHash = 0;
                return false;
            }

            var partialHash = _fullHashToPartialHashes[fullHash];
            leftPartialHash = partialHash.Item1;
            rightPartialHash = partialHash.Item2;
            return true;
        }
        */

        protected bool TryExtractFile(string archiveFileName, string localDiskFileName, out string md5Hash)
        {
            try
            {
                if (ExtractFile(archiveFileName, localDiskFileName))
                {
                    bool isEmpty = false;
                    using (var stream = File.OpenRead(localDiskFileName))
                    {
                        isEmpty = stream.Length == 0;
                        if (isEmpty)
                        {
                            File.Delete(localDiskFileName);
                            md5Hash = null;
                            return false;
                        }

                        md5Hash = CalculateMD5(stream);
                        stream.Position = 0;
                        var extension = StormMPQArchiveExtensions.PredictUnknownFileExtension(stream);
                        if (extension != null)
                        {
                            _md5ToPredictedExtension[md5Hash] = extension;
                        }
                    }

                    return true;
                }
            }
            catch { }

            md5Hash = null;
            return false;
        }

        protected bool ExtractFile(string archiveFileName, string extractedFileName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(extractedFileName));
            return StormLibrary.SFileExtractFile(_archiveHandle, archiveFileName, extractedFileName, 0);
        }

        protected string CalculatedPseudoFileName(uint fileIndex)
        {
            return $"File{fileIndex.ToString("00000000")}.xxx";
        }

        protected bool TryGetHashByFileIndex(uint fileIndex, out string pseudoFileName, out uint hashIndex, out uint leftHash, out uint rightHash, out ulong fullHash)
        {
            pseudoFileName = CalculatedPseudoFileName(fileIndex);
            return TryGetHashByFilename(pseudoFileName, out var _, out hashIndex, out leftHash, out rightHash, out fullHash);
        }

        protected bool TryGetHashByFilename(string fileName, out uint fileIndex, out uint hashIndex, out uint leftHash, out uint rightHash, out ulong fullHash)
        {
            if (StormLibrary.SFileOpenFileEx(_archiveHandle, fileName, 0, out var fileHandle))
            {
                IntPtr ptrFileIndex = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr ptrHashIndex = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr ptrRightHash = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                IntPtr ptrLeftHash = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
                try
                {
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoFileIndex, ptrFileIndex, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoHashIndex, ptrHashIndex, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoNameHash1, ptrRightHash, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                    StormLibrary.SFileGetFileInfo(fileHandle, StormLibrary.SFileInfoClass.SFileInfoNameHash2, ptrLeftHash, (uint)Marshal.SizeOf(typeof(uint)), out var _);

                    fileIndex = (uint)Marshal.ReadInt32(ptrFileIndex);
                    hashIndex = (uint)Marshal.ReadInt32(ptrHashIndex);
                    leftHash = (uint)Marshal.ReadInt32(ptrLeftHash);
                    rightHash = (uint)Marshal.ReadInt32(ptrRightHash);
                    fullHash = ((ulong)leftHash << 32) | rightHash;
                    return true;
                }
                finally
                {
                    StormLibrary.SFileCloseFile(fileHandle);
                    Marshal.FreeHGlobal(ptrFileIndex);
                    Marshal.FreeHGlobal(ptrHashIndex);
                    Marshal.FreeHGlobal(ptrLeftHash);
                    Marshal.FreeHGlobal(ptrRightHash);
                }
            }

            fileIndex = 0;
            hashIndex = 0;
            leftHash = 0;
            rightHash = 0;
            fullHash = 0;
            return false;
        }

        public bool DiscoverFile(string archiveFileName, out string md5Hash)
        {
            if (_discoveredFileNamesToMD5.TryGetValue(archiveFileName, out md5Hash))
            {
                return true;
            }

            if (_pseudoFileNamesWithoutExtensionToMD5.TryGetValue(Path.GetFileNameWithoutExtension(archiveFileName), out md5Hash))
            {
                return true;
            }

            if (StormLibrary.SFileHasFile(_archiveHandle, archiveFileName))
            {
                var extractedFileName = Path.Combine(_extractFolder, archiveFileName);
                if (TryExtractFile(archiveFileName, extractedFileName, out md5Hash))
                {
                    var mpqFullHash = MPQFullHash.Calculate(archiveFileName);
                    _discoveredFileNamesToMD5[archiveFileName] = md5Hash;
                    _discoveredMPQFullHashesToMD5[mpqFullHash] = md5Hash;
                    if (_md5ToLocalDiskFileName.TryGetValue(md5Hash, out var previousFileName))
                    {
                        if (!archiveFileName.Equals(previousFileName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            File.Delete(Path.Combine(Path.Combine(_extractFolder, previousFileName)));
                            _md5ToLocalDiskFileName[md5Hash] = archiveFileName;
                        }
                    }

                    _md5ToLocalDiskFileName[md5Hash] = archiveFileName;
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

        public unsafe StormMPQArchive(string mpqFileName, string extractFolder, Action<string> logEvent)
        {
            _extractFolder = extractFolder;
            _logEvent = logEvent;
            if (!StormLibrary.SFileOpenArchive(mpqFileName, 0, StormLibrary.SFileOpenArchiveFlags.AccessReadOnly, out _archiveHandle))
            {
                throw new Exception("Unable to open MPQ Archive");
            }

            /*
            IntPtr ptrFileCount = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint)));
            try
            {
                StormLibrary.SFileGetFileInfo(_archiveHandle, StormLibrary.SFileInfoClass.SFileMpqNumberOfFiles, ptrFileCount, (uint)Marshal.SizeOf(typeof(uint)), out var _);
                _fileCount = (uint)Marshal.ReadInt32(ptrFileCount);
            }
            finally
            {
                Marshal.FreeHGlobal(ptrFileCount);
            }
            */

            _logEvent("Opening MPQ Archive");
            var findFileHandle = StormLibrary.SFileFindFirstFile(_archiveHandle, "*", out var findData, null);
            try
            {
                _logEvent("Enumerating contents");
                do
                {
                    var fileName = MarshalByteArrayAsString(findData.cFileName);
                    var entry = new StormMPQHashEntry();
                    if (TryGetHashByFilename(fileName, out entry.FileIndex, out entry.HashIndex, out entry.LeftPartialHash, out entry.RightPartialHash, out entry.FullHash))
                    {
                        var correctPartialHashes = MPQFullHash.SplitValue(entry.FullHash);
                        var correctFullHash = MPQFullHash.GetValue(entry.LeftPartialHash, entry.RightPartialHash);
                        _fullHashToPartialHashes[entry.FullHash] = new Tuple<uint, uint>(entry.LeftPartialHash, entry.RightPartialHash);
                        //_fullHashToPartialHashes[entry.FullHash] = new Tuple<uint, uint>(correctPartialHashes.Item1, correctPartialHashes.Item2);
                        //_fullHashToPartialHashes[correctFullHash] = new Tuple<uint, uint>(entry.LeftPartialHash, entry.RightPartialHash);

                        if (entry.FullHash != correctFullHash)
                        {
                            DebugSettings.Warn("mismatched full hash");
                        }

                        if (entry.LeftPartialHash != correctPartialHashes.Item1 || entry.RightPartialHash != correctPartialHashes.Item2)
                        {
                            DebugSettings.Warn("mismatched partial hash");
                        }

                        entry.IsUnknown = MPQFullHash.Calculate(fileName) != entry.FullHash;
                        if (entry.IsUnknown)
                        {
                            entry.PseudoFileName = fileName;
                        }
                        else
                        {
                            entry.PseudoFileName = CalculatedPseudoFileName(entry.FileIndex);
                            entry.RealFileName = fileName;
                        }

                        var extractedFileName = Path.Combine(entry.IsUnknown ? UNKNOWN_FOLDER : "", entry.FileName);
                        var localDiskFileName = Path.Combine(_extractFolder, extractedFileName);
                        if (TryExtractFile(fileName, localDiskFileName, out entry.FileContentsMD5Hash))
                        {
                            //if (_fullHashToPartialHashes.ContainsKey(entry.FullHash) || _fullHashToPartialHashes.ContainsKey(correctFullHash))
                            //{
                            //    DebugSettings.Warn("correct hash found 2x");
                            //}

                            var isDuplicate = _md5ToLocalDiskFileName.TryGetValue(entry.FileContentsMD5Hash, out var oldFileName) && !extractedFileName.Equals(oldFileName, StringComparison.InvariantCultureIgnoreCase);
                            if (entry.IsUnknown)
                            {
                                if (isDuplicate)
                                {
                                    File.Delete(localDiskFileName);
                                    continue;
                                }

                                var extension = Path.GetExtension(entry.PseudoFileName);
                                if (extension.Equals(".xxx", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    extension = "";
                                }
                                _md5ToPredictedExtension[entry.FileContentsMD5Hash] = extension;
                            }
                            else
                            {
                                if (isDuplicate)
                                {
                                    File.Delete(Path.Combine(_extractFolder, oldFileName));
                                }

                                var mpqFullHash = MPQFullHash.Calculate(entry.RealFileName);
                                _fullHashToPartialHashes[mpqFullHash] = MPQFullHash.SplitValue(mpqFullHash);
                                _discoveredMPQFullHashesToMD5[mpqFullHash] = entry.FileContentsMD5Hash;
                                _discoveredFileNamesToMD5[entry.RealFileName] = entry.FileContentsMD5Hash;
                                _md5ToPredictedExtension[entry.FileContentsMD5Hash] = Path.GetExtension(entry.RealFileName);
                            }

                            //if (_fileTable.TryGetValue(entry.FileIndex, out var existingEntry) && !existingEntry.Equals(entry))
                            //{
                            //    DebugSettings.Warn("Conflicting File Entries!");
                            //}

                            //_fileTable[entry.FileIndex] = entry;
                            _md5ToLocalDiskFileName[entry.FileContentsMD5Hash] = extractedFileName;
                            _logEvent($"Extracted File: {extractedFileName}");
                        }
                        else
                        {
                            //_fakeHashToPartialHashes[entry.FullHash] = new Tuple<uint, uint>(entry.LeftPartialHash, entry.RightPartialHash);
                            //if (_fakeHashToPartialHashes.ContainsKey(entry.FullHash))
                            //{
                            //    DebugSettings.Warn("fake hash found 2x");
                            //}
                        }
                    }
                    else
                    {
                        DebugSettings.Warn("Unable to get hash for file from MPQ");
                    }
                } while (StormLibrary.SFileFindNextFile(findFileHandle, out findData));

                _logEvent("Extraction Completed");
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