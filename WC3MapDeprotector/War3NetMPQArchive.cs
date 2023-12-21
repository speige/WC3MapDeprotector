using ICSharpCode.Decompiler.Metadata;
using NuGet.Packaging;
using System.Reflection;
using War3Net.Build.Extensions;
using War3Net.Common.Extensions;
using War3Net.IO.Mpq;

namespace WC3MapDeprotector
{
    public class War3NetMPQArchive : IMPQArchive
    {
        protected MpqArchive _mpqArchive;

        public List<ulong> AllFileNameHashes
        {
            get
            {
                return _mpqArchive.GetMpqFiles().Select(x => x.Name).ToList();
            }
        }

        public Dictionary<ulong, string> DiscoveredFileNames
        {
            get
            {
                return _mpqArchive.GetMpqFiles().Where(x => x is MpqKnownFile).Cast<MpqKnownFile>().ToDictionary(x => x.Name,x => x.FileName);
            }
        }

        public List<ulong> UnknownFileNameHashes
        {
            get
            {
                return _mpqArchive.GetMpqFiles().Where(x => x is MpqUnknownFile unknown && unknown.MpqStream.FileSize > 0).Select(x => x.Name).ToList();
            }
        }

        public bool ExtractFile(ulong archiveFileHash, string extractedFileName)
        {
            var mpqFile = _mpqArchive.GetMpqFiles().Where(x => x.Name == archiveFileHash).FirstOrDefault();
            if (mpqFile == null || mpqFile.MpqStream.FileSize <= 0 || mpqFile is MpqOrphanedFile)
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(extractedFileName));
            using (var stream = new FileStream(extractedFileName, FileMode.OpenOrCreate))
            {
                mpqFile.MpqStream.CopyTo(stream);
            }
            return true;
        }

        public bool DiscoverFile(string archiveFileName)
        {
            return _mpqArchive.FileExists(archiveFileName);
        }

        public War3NetMPQArchive(string mpqFileName)
        {
            using (var reader = File.OpenRead(mpqFileName))
            using (var binaryReader = new BinaryReader(reader))
            {
                var parameters = new object[] { reader, null, null };
                War3NetExtensions.TryLocateMpqHeader_New(reader, out var headerOffset);
                reader.Seek(headerOffset + 12, SeekOrigin.Begin);

                var mpqVersion = binaryReader.ReadUInt16();

                // 0 is ONLY valid format for WC3
                if (mpqVersion != 0)
                {
                    mpqFileName = Path.GetTempFileName();
                    using (var writer = File.OpenWrite(mpqFileName))
                    using (var binaryWriter = new BinaryWriter(writer))
                    {
                        reader.Position = 0;
                        reader.CopyTo(writer, (int)headerOffset + 12, 1);

                        binaryWriter.Write(mpqVersion);
                        reader.Seek(writer.Position, SeekOrigin.Begin);
                        reader.CopyTo(writer);
                        //_logEvent("Corrected invalid MPQ Header Format Version");
                    }
                }
            }

            try
            {
                _mpqArchive = MpqArchive.Open(mpqFileName, true);
                _mpqArchive.DiscoverFileNames();
            }
            catch
            {
                _mpqArchive.Dispose();
                mpqFileName = Path.GetTempFileName();
                using (var stream = MpqArchive.Restore(mpqFileName))
                {
                    stream.CopyToFile(mpqFileName);
                }
                _mpqArchive = MpqArchive.Open(mpqFileName, true);
            }

            RemoveInvalidFiles();
        }

        protected class MpqArchiveReflectionData
        {
            public object HashTable;
            public object BlockTable;
            public uint HashTableSize;
            public MpqHash[] Hashes;
            public List<MpqEntry> BlockTableEntries;
            public List<MpqHash> AllHashes;
            public List<ulong> UnknownHashes;
        }

        protected MpqArchiveReflectionData GetMpqArchiveInternalData(MpqArchive archive)
        {
            var result = new MpqArchiveReflectionData();
            result.HashTable = typeof(MpqArchive).GetField("_hashTable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(archive);
            result.BlockTable = typeof(MpqArchive).GetField("_blockTable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(archive);
            result.HashTableSize = (uint)result.HashTable.GetType().GetProperty("Size", BindingFlags.Instance | BindingFlags.Public).GetValue(result.HashTable);
            result.Hashes = (MpqHash[])result.HashTable.GetType().GetField("_hashes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(result.HashTable);
            result.BlockTableEntries = (List<MpqEntry>)result.BlockTable.GetType().GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(result.BlockTable);
            result.AllHashes = Enumerable.Range(0, (int)result.HashTableSize).Select(x => result.Hashes[x]).Where(x => !x.IsEmpty && !x.IsDeleted).ToList();
            result.UnknownHashes = result.AllHashes.Where(x => ((int)x.BlockIndex) > 0 && ((int)x.BlockIndex) < result.BlockTableEntries.Count && result.BlockTableEntries[(int)x.BlockIndex].FileName == null).Select(x => x.Name).ToList();
            return result;
        }

        protected void RemoveInvalidFiles()
        {
            var reflectionData = GetMpqArchiveInternalData(_mpqArchive);
            for (var hashIndex = 0; hashIndex < reflectionData.HashTableSize; hashIndex++)
            {
                var mpqHash = reflectionData.Hashes[hashIndex];
                if (mpqHash.BlockIndex >= reflectionData.BlockTableEntries.Count)
                {
                    reflectionData.Hashes[hashIndex] = MpqHash.NULL;
                }
            }

            var emptyFiles = _mpqArchive.GetMpqFiles().Where(x => x.MpqStream.Length <= 0).ToList();
            foreach (var emptyFile in emptyFiles)
            {
                var hash = emptyFile.Name;
                for (var hashIndex = 0; hashIndex < reflectionData.HashTableSize; hashIndex++)
                {
                    var mpqHash = reflectionData.Hashes[hashIndex];
                    if (emptyFiles.Any(x => x.Name == mpqHash.Name))
                    {
                        reflectionData.Hashes[hashIndex] = MpqHash.NULL;
                    }
                }
            }
        }

        public void Dispose()
        {
            _mpqArchive.Dispose();
        }
    }
}