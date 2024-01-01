using CSharpLua;
using NAudio.Wave;
using NuGet.Packaging;

namespace WC3MapDeprotector
{
    public static class StormMPQArchiveExtensions
    {
        private static Lazy<HashSet<string>> _defaultListFile = new Lazy<HashSet<string>>(() =>
        {
            var defaultListFile = new ConcurrentHashSet<string>(StringComparer.InvariantCultureIgnoreCase) {
                "(attributes)",
                "(listfile)",
                "(signature)",
                "(user data)"
            };

            var extensions = new List<string>() { "blp", "b00", "doo", "imp", "j", "json", "lua", "mmp", "shd", "slk", "tga", "txt", "wai", "wct", "wpm", "wtg", "wts" };
            extensions.AddRange(Enumerable.Range((int)'a', 26).Select(x => "w3" + (char)x));
            var filePrefixes = new List<string>() { "blizzard", "common", "conversation", "doodads", "eaxdefs", "Terrain", "war3campaign", "war3campaignSkin", "war3map", "war3mapExtra", "war3mapMap", "war3mapMisc", "war3mapPath", "war3mapPreview", "war3mapSkin", "war3mapUnits", "water", "weather" };

            foreach (var prefix1 in new string[] { "Campaign", "Common", "Human", "Item", "Neutral", "NightElf", "Orc", "Undead", "NotUsed_" })
            {
                foreach (var prefix2 in new string[] { "Unit", "Upgrade", "Ability" })
                {
                    foreach (var prefix3 in new string[] { "Data", "Func", "Strings", "UI" })
                    {
                        filePrefixes.Add($"{prefix1}{prefix2}{prefix3}");
                    }
                }
            }

            foreach (var prefix1 in new string[] { "Ambience", "Ambient", "Ability", "Anim", "Cliff", "Destructable", "Dialog", "Doodad", "Environment", "Item", "Lightning", "Misc", "MIDI", "Portrait", "Skin", "Spawn", "Splat", "UberSplat", "Unit", "UnitAck", "UnitCombat", "Upgrade", "UpgradeEffect", "UI" })
            {
                foreach (var prefix2 in new string[] { "Abilities", "Anims", "Balance", "BuffData", "BuffMetaData", "Data", "Lookups", "MetaData", "Music", "Sounds", "Types", "UI", "Weapons" })
                {
                    filePrefixes.Add($"{prefix1}{prefix2}");
                }
            }

            var folders = new List<string>() { "", "doodads", "scripts", "splats", "terrainart", "ui", @"ui\soundinfo", "units" };

            Parallel.ForEach(folders.Select(x => x.Trim('\\')), folder =>
            {
                foreach (var filePrefix in filePrefixes)
                {
                    foreach (var extension in extensions.Select(x => x.Trim('.')))
                    {
                        var directoryPrefix = string.IsNullOrWhiteSpace(folder) ? "" : $"{folder}\\";
                        defaultListFile.Add($"{directoryPrefix}{filePrefix}.{extension}");
                    }
                }
            });

            return defaultListFile.ToHashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        });

        public static bool IsInDefaultListFile(string fileName)
        {
            return _defaultListFile.Value.Contains(fileName);
        }

        public static List<string> ProcessDefaultListFile(this StormMPQArchive archive)
        {
            return archive.ProcessListFile(_defaultListFile.Value);
        }

        private static HashSet<string> _extraSearchDirectories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "", "scripts", "Fonts", "ReplaceableTextures\\CommandButtons", "ReplaceableTextures\\CommandButtonsDisabled", "ReplaceableTextures\\PassiveButtons", "war3mapImported" };
        public static List<string> DeepScan_GetDirectories(this StormMPQArchive archive, List<string> scannedFileNames)
        {
            var directories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            directories.AddRange(_extraSearchDirectories);
            directories.AddRange(archive.GetDiscoveredFileNames().Select(x => Path.GetDirectoryName(x)).Select(x => x.Trim('\\')).Distinct(StringComparer.InvariantCultureIgnoreCase));
            directories.AddRange(scannedFileNames.Select(x => Path.GetDirectoryName(x.Replace("/", "\\").Trim('\\'))).Where(x => x != null));
            foreach (var directory in directories.ToList())
            {
                var split = directory.Split('\\');
                for (int i = 1; i <= split.Length; i++)
                {
                    directories.Add(split.Take(i).Aggregate((x, y) => $"{x}\\{y}"));
                }
            }

            return directories.ToList();
        }

        public static void DiscoverUnknownFileNames_DeepScan(this StormMPQArchive archive, List<string> baseFileNames, List<string> directories, Action<string> _logEvent = null)
        {
            _logEvent = _logEvent ?? (x=>{});
            _logEvent("Performing deep scan for unknown files ...");

            //todo: Disable deep scanning of directories unless Benchmark is enabled. Add separate deep scan of File Extensions if Benchmark is enabled. Delete each one if they don't produce any better results than when it's disabled.
            //todo: If directory name is blank, do check for each shorter version of file.ext (ile.ext le.ext l.ext e.ext .ext)
            var originalUnknownCount = archive.UnknownFileCount;
            var foundFileLock = new object();
            var finishedSearching = false;
            Parallel.ForEach(directories, directory =>
            {
                _logEvent($"Deep scanning - {directory}");

                var hashPrefix = !string.IsNullOrWhiteSpace(directory) ? $"{directory}\\" : "";
                var directoryHash = new MPQPartialHash(MPQPartialHash.LEFT_OFFSET);
                if (!directoryHash.TryAddString(hashPrefix))
                {
                    return;
                }

                foreach (var fileName in baseFileNames)
                {
                    if (finishedSearching)
                    {
                        return;
                    }

                    var fileHash = directoryHash;
                    if (fileHash.TryAddString(fileName) && archive.LeftPartialHashExists(fileHash.Value))
                    {
                        var fullFileName = $"{hashPrefix}{fileName}";

                        if (MPQPartialHash.TryCalculate(fullFileName, MPQPartialHash.RIGHT_OFFSET, out var rightHash) && archive.RightPartialHashExists(rightHash))
                        {
                            lock (foundFileLock)
                            {
                                if (archive.DiscoverFile(fullFileName, out var _))
                                {
                                    //NOTE: If it has fake files, we keep scanning until entire file list is exhausted, because we may find a better name for an already resolved file
                                    if (!archive.ShouldKeepScanningForUnknowns)
                                    {
                                        finishedSearching = true;
                                    }
                                }
                            }
                        }
                    }
                }
            });

            var filesFound = originalUnknownCount - archive.UnknownFileCount;
            _logEvent($"Deep Scan completed: {filesFound} filenames found");
        }
    }
}