using CSharpLua;
using ICSharpCode.Decompiler.Util;
using IniParser;
using IniParser.Model.Configuration;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using NuGet.Packaging;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using War3Net.Build;
using War3Net.Build.Audio;
using War3Net.Build.Environment;
using War3Net.Build.Extensions;
using War3Net.Build.Info;
using War3Net.Build.Script;
using War3Net.Build.Widget;
using War3Net.CodeAnalysis.Decompilers;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.CodeAnalysis.Transpilers;
using War3Net.Common.Extensions;
using War3Net.IO.Mpq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using FastMDX;

namespace WC3MapDeprotector
{
    // useful links
    // https://github.com/Luashine/wc3-file-formats
    // https://github.com/ChiefOfGxBxL/WC3MapSpecification
    // https://github.com/stijnherfst/HiveWE/wiki/war3map(skin).w3*-Modifications
    // https://github.com/WaterKnight/Warcraft3-Formats-ANTLR
    // https://www.hiveworkshop.com/threads/warcraft-3-trigger-format-specification-wtg.294491/
    // https://867380699.github.io/blog/2019/05/09/W3X_Files_Format
    // https://world-editor-tutorials.thehelper.net/cat_usersubmit.php?view=42787

    public partial class Deprotector : IDisposable
    {
        protected readonly List<string> _nativeEditorFunctions = new List<string>() { "config", "main", "CreateAllUnits", "CreateAllItems", "CreateNeutralPassiveBuildings", "CreatePlayerBuildings", "CreatePlayerUnits", "InitCustomPlayerSlots", "InitGlobals", "InitCustomTriggers", "RunInitializationTriggers", "CreateRegions", "CreateCameras", "InitSounds", "InitCustomTeams", "InitAllyPriorities", "CreateNeutralPassive", "CreateNeutralHostile" };

        protected const string ATTRIB = "Map deprotected by WC3MapDeprotector https://github.com/speige/WC3MapDeprotector\r\n\r\n";
        protected readonly HashSet<string> _commonFileExtensions = new HashSet<string>((new [] { "pcx", "gif", "cel", "dc6", "cl2", "ogg", "smk", "bik", "avi", "lua", "ai", "asi", "ax", "blp", "ccd", "clh", "css", "dds", "dll", "dls", "doo", "exe", "exp", "fdf", "flt", "gid", "html", "ifl", "imp", "ini", "j", "jpg", "js", "log", "m3d", "mdl", "mdx", "mid", "mmp", "mp3", "mpq", "mrf", "pld", "png", "shd", "slk", "tga", "toc", "ttf", "otf", "woff", "txt", "url", "w3a", "w3b", "w3c", "w3d", "w3e", "w3g", "w3h", "w3i", "w3m", "w3n", "w3f", "w3v", "w3z", "w3q", "w3r", "w3s", "w3t", "w3u", "w3x", "wai", "wav", "wct", "wpm", "wpp", "wtg", "wts", "mgv", "mg", "sav" }).Select(x => $".{x.Trim('.')}"), StringComparer.InvariantCultureIgnoreCase);

        protected string _inMapFile;
        protected readonly string _outMapFile;
        public DeprotectionSettings Settings { get; private set; }
        protected readonly Action<string> _logEvent;
        protected ConcurrentHashSet<string> _extractedMapFiles;
        protected DeprotectionResult _deprotectionResult;


        protected void AddToGlobalListFile(string fileName)
        {
            AddToGlobalListFile(new List<string>() { fileName });
        }

        protected void AddToGlobalListFile(List<string> fileNames)
        {
            try
            {
                File.AppendAllLines(WorkingListFileName, fileNames);
            }
            catch
            {
                //can throw due to race condition if multiple instances deprotecting simultaneously
            }

            foreach (var fileName in fileNames)
            {
                if (MPQFullHash.TryCalculate(fileName, out var hash))
                {
                    globalListFileRainbowTable.Value[hash] = fileName;
                }
                _logEvent($"added to global listfile: {fileName}");
            }
        }

        protected static Lazy<Dictionary<ulong, string>> globalListFileRainbowTable = new Lazy<Dictionary<ulong, string>>(() =>
        {
            if (File.Exists(InstallerListFileName))
            {
                var extractedListFileFolder = Path.Combine(Path.GetTempPath(), "WC3MapDeprotector");
                Directory.CreateDirectory(extractedListFileFolder);
                ZipFile.ExtractToDirectory(InstallerListFileName, extractedListFileFolder, true);

                var extractedListFileEntries = new string[0];
                var extractedListFileName = Path.Combine(extractedListFileFolder, "listfile.txt");
                if (File.Exists(extractedListFileName))
                {
                    extractedListFileEntries = File.ReadAllLines(extractedListFileName);
                }

                var existingListFileEntries = new string[0];
                if (File.Exists(WorkingListFileName))
                {
                    existingListFileEntries = File.ReadAllLines(WorkingListFileName);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(WorkingListFileName));
                File.WriteAllLines(WorkingListFileName, extractedListFileEntries.Concat(existingListFileEntries).OrderBy(x => x).Distinct(StringComparer.InvariantCultureIgnoreCase));
                File.Delete(InstallerListFileName);
                File.Delete(Path.Combine(extractedListFileFolder, "listfile.txt"));
            }

            var originalListFile = File.ReadAllLines(WorkingListFileName);
            var listFile = new List<string>(originalListFile.OrderBy(x => x).Distinct(StringComparer.InvariantCultureIgnoreCase));
            if (originalListFile.Length != listFile.Count)
            {
                File.WriteAllLines(WorkingListFileName, listFile);
            }

            return StormMPQArchiveExtensions.ConvertListFileToRainbowTable(listFile);
        });

        public Deprotector(string inMapFile, string outMapFile, DeprotectionSettings settings, Action<string> logEvent)
        {
            _inMapFile = inMapFile;
            _outMapFile = outMapFile;
            Settings = settings;
            _logEvent = logEvent;
        }

        protected static string ExeFolderPath
        {
            get
            {
                return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            }
        }

        protected static string BaseMapFilesPath
        {
            get
            {
                return Path.Combine(ExeFolderPath, "BaseMapFiles");
            }
        }

        protected string SLKRecoverPath
        {
            get
            {
                return Path.Combine(WorkingFolderPath, "SilkObjectOptimizer");
            }
        }


        protected static string InstallerListFileName
        {
            get
            {

                return Path.Combine(ExeFolderPath, "listfile.zip");
            }
        }

        protected static string WorkingListFileName
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WC3MapDeprotector", "listfile.txt");
            }
        }

        protected string SLKRecoverEXE
        {
            get
            {
                return Path.Combine(SLKRecoverPath, "Silk Object Optimizer.exe");
            }
        }
        
        protected string WorkingFolderUniquePath = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
        protected string WorkingFolderPath
        {
            get
            {
                return Path.Combine(TempFolderPath, WorkingFolderUniquePath);
            }
        }

        protected string TempFolderPath
        {
            get
            {
                var benchmarkSuffix = DebugSettings.BenchmarkUnknownRecovery ? "_benchmark" : "";
                return Path.Combine(Path.GetTempPath(), "WC3MapDeprotector", $"{Path.GetFileName(_inMapFile)}.work{benchmarkSuffix}");
            }
        }

        protected string ExtractedFilesPath
        {
            get
            {
                return Path.Combine(WorkingFolderPath, "files");
            }
        }

        protected string DiscoveredFilesPath
        {
            get
            {
                return Path.Combine(ExtractedFilesPath, StormMPQArchive.DISCOVERED_FOLDER);
            }
        }


        protected string UnknownFilesPath
        {
            get
            {
                return Path.Combine(ExtractedFilesPath, StormMPQArchive.UNKNOWN_FOLDER);
            }
        }

        protected string MapBaseName
        {
            get
            {
                return Path.GetFileName(_inMapFile);
            }
        }

        protected class IndexedJassCompilationUnitSyntax
        {
            public JassCompilationUnitSyntax CompilationUnit { get; }
            public Dictionary<string, JassFunctionDeclarationSyntax> IndexedFunctions { get; }

            public IndexedJassCompilationUnitSyntax(JassCompilationUnitSyntax compilationUnit)
            {
                CompilationUnit = compilationUnit;
                IndexedFunctions = CompilationUnit.Declarations.Where(x => x is JassFunctionDeclarationSyntax).Cast<JassFunctionDeclarationSyntax>().GroupBy(x => x.FunctionDeclarator.IdentifierName.Name).ToDictionary(x => x.Key, x => x.First());
            }
        }

        protected class ScriptMetaData
        {
            private MapSounds sounds;

            public MapInfo Info { get; set; }
            public MapSounds Sounds
            {
                get
                {
                    var result = sounds ?? new MapSounds(MapSoundsFormatVersion.v3);
                    if (!result.Sounds.Any(x => x.FilePath == "\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76"))
                    {
                        result.Sounds.Add(new Sound() { FilePath = "\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76", Name = "\x67\x67\x5F\x73\x6E\x64\x5F\x77\x61\x72\x33\x6D\x61\x70" });
                    }
                    return result;
                }
                set => sounds = value;
            }
            public MapCameras Cameras { get; set; }
            public MapRegions Regions { get; set; }
            public MapTriggers Triggers { get; set; }
            public TriggerStrings TriggerStrings { get; set; }
            public MapUnits Units { get; set; }
            public Dictionary<UnitData, string> UnitsDecompiledFromVariableName { get; set; }

            public List<MpqKnownFile> ConvertToFiles()
            {
                var map = new Map() { Info = Info, Units = Units, Sounds = Sounds, Cameras = Cameras, Regions = Regions, Triggers = Triggers, TriggerStrings = TriggerStrings };
                try
                {
                    return map.GetAllFiles();
                }
                catch
                {
                    return new List<MpqKnownFile>();
                }
            }
        }

        protected void WaitForProcessToExit(Process process, CancellationToken cancellationToken = default)
        {
            while (!process.HasExited)
            {
                Thread.Sleep(1000);

                if (cancellationToken.IsCancellationRequested)
                {
                    process.Kill();
                }
            }
        }

        protected Process ExecuteCommand(string exePath, string arguments)
        {
            var process = new Process();
            process.StartInfo.FileName = exePath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.RedirectStandardInput = false;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
            process.Start();
            return process;
        }

        protected string decode(string encoded)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }

        protected async Task LiveGameScanForUnknownFiles(StormMPQArchive archive)
        {
            var process = ExecuteCommand(Settings.WC3ExePath, $"-launch -loadfile \"{_inMapFile}\" -mapdiff 1 -testmapprofile WorldEdit -fixedseed 1");
            //Thread.Sleep(15 * 1000); // wait for WC3 to load

            var directories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            directories.AddRange(_extractedMapFiles.Select(x => Path.GetDirectoryName(x.Replace("/", "\\"))).Where(x => x != null));
            var originalDirectories = directories.ToList();
            foreach (var directory in originalDirectories)
            {
                var split = directory.Split('\\');
                for (int i = 1; i <= split.Length; i++)
                {
                    directories.Add(split.Take(i).Aggregate((x, y) => $"{x}\\{y}"));
                }
            }

            //todo: if we can hook the FileAccess API from WC3 & enable LocalFiles, we can just search every file it accesses, regardless of extension
            var extensions = GetPredictedUnknownFileExtensions();
            var cheatEngineForm = new frmLiveGameScanner(scannedFileName =>
            {
                var filesToTest = new List<string>() { scannedFileName };
                var baseFileName = Path.GetFileName(scannedFileName);
                foreach (var directory in directories)
                {
                    filesToTest.Add(directory + "\\" + baseFileName);
                }

                foreach (var fileName in filesToTest)
                {
                    if (!_extractedMapFiles.Contains(fileName) && ExtractFileFromArchive(archive, fileName))
                    {
                        _extractedMapFiles.Add(fileName);

                        _deprotectionResult.NewListFileEntriesFound++;
                        AddToGlobalListFile(fileName);

                        if (!archive.HasUnknownHashes)
                        {
                            process.Kill();
                            return;
                        }
                    }
                }
            }, process, extensions);
            cheatEngineForm.ShowDialog();
        }

        protected void CleanTemp()
        {
            if (DebugSettings.DontCleanTemp)
            {
                return;
            }

            var tempDir = TempFolderPath;
            if (Directory.Exists(tempDir))
            {
                _logEvent("Deleting temporary files...");
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception e)
                {
                    _logEvent($"Unable to delete temporary files: {e.Message}");
                }
            }
        }

        public async Task<DeprotectionResult> Deprotect()
        {
            _logEvent($"Processing map: {MapBaseName}");

            CleanTemp();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_outMapFile));
                File.Delete(_outMapFile);
            }
            catch
            {
                throw new Exception($"Output Map File is locked. Please close any MPQ programs, WC3 Game, & WorldEditor and try again. File: {_outMapFile}");
            }

            CleanTemp();

            Directory.CreateDirectory(Path.GetDirectoryName(_outMapFile));

            _extractedMapFiles = new ConcurrentHashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            _deprotectionResult = new DeprotectionResult();
            _deprotectionResult.WarningMessages.Add($"NOTE: This tool is a work in progress. Deprotection does not work perfectly on every map. If objects are missing or script has compilation errors, you will need to fix these by hand. You can get help from my YouTube channel or report defects by clicking the bug icon.");

            var baseMapFilesZip = Path.Combine(ExeFolderPath, "BaseMapFiles.zip");
            if (!Directory.Exists(BaseMapFilesPath) && File.Exists(baseMapFilesZip))
            {
                ZipFile.ExtractToDirectory(baseMapFilesZip, BaseMapFilesPath, true);
            }

            if (!File.Exists(_inMapFile))
            {
                throw new FileNotFoundException($"Cannot find source map file: {_inMapFile}");
            }

            if (!Directory.Exists(WorkingFolderPath))
            {
                Directory.CreateDirectory(WorkingFolderPath);
            }
            if (!Directory.Exists(ExtractedFilesPath))
            {
                Directory.CreateDirectory(ExtractedFilesPath);
            }
            if (!Directory.Exists(UnknownFilesPath))
            {
                Directory.CreateDirectory(UnknownFilesPath);
            }
            if (!Directory.Exists(DiscoveredFilesPath))
            {
                Directory.CreateDirectory(DiscoveredFilesPath);
            }

            using (var inMPQArchive = new StormMPQArchive(_inMapFile, ExtractedFilesPath, _logEvent, _deprotectionResult))
            {
                if (inMPQArchive.HasFakeFiles)
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                }

                if (!DebugSettings.BenchmarkUnknownRecovery && ExtractFileFromArchive(inMPQArchive, "(listfile)"))
                {
                    //will be slower for small list files, but safer in case of a giant embedded listfile
                    var rainbowTable = StormMPQArchiveExtensions.ConvertListFileToRainbowTable(File.ReadAllLines(Path.Combine(DiscoveredFilesPath, "(listfile)")).ToList());
                    inMPQArchive.ProcessListFile(rainbowTable);

                    if (inMPQArchive.HasUnknownHashes)
                    {
                        _deprotectionResult.CountOfProtectionsFound++;
                    }
                }

                if (inMPQArchive.HasUnknownHashes || inMPQArchive.HasFakeFiles)
                {
                    inMPQArchive.ProcessDefaultListFile();
                }

                if ((inMPQArchive.HasUnknownHashes || inMPQArchive.HasFakeFiles) && !DebugSettings.BenchmarkUnknownRecovery)
                {
                    var discoveredFiles = inMPQArchive.ProcessListFile(globalListFileRainbowTable.Value);
                    foreach (var file in discoveredFiles)
                    {
                        VerifyActualAndPredictedExtensionsMatch(inMPQArchive, file);
                    }
                }

                DeleteAttributeListSignatureFiles();

                foreach (var fileName in inMPQArchive.DiscoveredFileNames)
                {
                    _extractedMapFiles.Add(fileName);
                }

                _logEvent($"Unknown file count: {inMPQArchive.UnknownFileCount}");

                var slkFiles = Directory.GetFiles(ExtractedFilesPath, "*.slk", SearchOption.AllDirectories);
                if (slkFiles.Length > 0)
                {
                    var silkObjectOptimizerZip = Path.Combine(ExeFolderPath, "SilkObjectOptimizer.zip");
                    if (!File.Exists(SLKRecoverEXE) && File.Exists(silkObjectOptimizerZip))
                    {
                        ZipFile.ExtractToDirectory(silkObjectOptimizerZip, SLKRecoverPath, true);
                    }

                    _logEvent("Generating temporary map for SLK Recover: slk.w3x");
                    var slkMpqArchive = Path.Combine(SLKRecoverPath, "slk.w3x");
                    var excludedFileNames = new string[] { "war3map.w3a", "war3map.w3b", "war3map.w3d" }; // can't be in slk.w3x or it will crash
                    var minimumExtraRequiredFiles = Directory.GetFiles(DiscoveredFilesPath, "war3map*", SearchOption.AllDirectories).Union(Directory.GetFiles(DiscoveredFilesPath, "war3campaign*", SearchOption.AllDirectories)).Where(x => !excludedFileNames.Contains(Path.GetFileName(x))).ToList();

                    BuildW3X(slkMpqArchive, ExtractedFilesPath, slkFiles.Union(minimumExtraRequiredFiles).ToList());
                    _logEvent("slk.w3x generated");

                    _logEvent("Running SilkObjectOptimizer");
                    var slkRecoverOutPath = Path.Combine(SLKRecoverPath, "OUT");
                    new DirectoryInfo(slkRecoverOutPath).Delete(true);
                    Directory.CreateDirectory(slkRecoverOutPath);
                    WaitForProcessToExit(ExecuteCommand(SLKRecoverEXE, ""));

                    _logEvent("SilkObjectOptimizer completed");

                    var slkRecoveredFiles = new List<string>() { "war3map.w3a", "war3map.w3c", "war3map.w3h", "war3map.w3q", "war3map.w3r", "war3map.w3t", "war3map.w3u" };
                    foreach (var fileName in slkRecoveredFiles)
                    {
                        _logEvent($"Replacing {fileName} with SLKRecover version");
                        var recoveredFile = Path.Combine(slkRecoverOutPath, fileName);
                        if (File.Exists(recoveredFile))
                        {
                            if (!File.Exists(Path.Combine(DiscoveredFilesPath, fileName)))
                            {
                                _deprotectionResult.CountOfProtectionsFound++;
                            }

                            if (File.Exists(Path.Combine(DiscoveredFilesPath, fileName)))
                            {
                                DebugSettings.Warn("Verify if original file had anything valueable that was erased by SLKRecover");
                                _logEvent($"Overwriting {fileName} with SLKRecover version");
                            }
                            File.Copy(recoveredFile, Path.Combine(DiscoveredFilesPath, fileName), true);
                        }
                    }
                }

                foreach (var scriptFile in Directory.GetFiles(DiscoveredFilesPath, "war3map.j", SearchOption.AllDirectories))
                {
                    if (Path.GetDirectoryName(scriptFile) != DiscoveredFilesPath)
                    {
                        File.Move(scriptFile, Path.Combine(DiscoveredFilesPath, "war3map.j"), true);
                    }
                }
                foreach (var scriptFile in Directory.GetFiles(DiscoveredFilesPath, "war3map.lua", SearchOption.AllDirectories))
                {
                    if (Path.GetDirectoryName(scriptFile) != DiscoveredFilesPath)
                    {
                        File.Move(scriptFile, Path.Combine(DiscoveredFilesPath, "war3map.lua"), true);
                    }
                }

                var unknownFiles = GetUnknownFileNames();
                if (!File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.j")))
                {
                    var unknownFile = unknownFiles.FirstOrDefault(x => string.Equals(Path.GetExtension(x), ".j", StringComparison.InvariantCultureIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(unknownFile))
                    {
                        File.Copy(Path.Combine(UnknownFilesPath, unknownFile), Path.Combine(DiscoveredFilesPath, "war3map.j"), true);
                    }
                }

                if (!File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.lua")))
                {
                    var unknownFile = unknownFiles.FirstOrDefault(x => string.Equals(Path.GetExtension(x), ".lua", StringComparison.InvariantCultureIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(unknownFile))
                    {
                        File.Copy(Path.Combine(UnknownFilesPath, unknownFile), Path.Combine(DiscoveredFilesPath, "war3map.lua"), true);
                    }
                }

                if (inMPQArchive.HasUnknownHashes || inMPQArchive.HasFakeFiles)
                {
                    var discoveredFileNamesBackup = inMPQArchive.DiscoveredFileNames.ToList();

                    _logEvent("Scanning for possible filenames...");
                    var map = DecompileMap();
                    var unknownFileExtensions = _commonFileExtensions.ToList();
                    var possibleFileNames_parsed = ParseFilesToDetectPossibleFileNames(map, unknownFileExtensions);
                    AddAlternateUnknownRecoveryFileNames(possibleFileNames_parsed);
                    DiscoverUnknownFileNames(inMPQArchive, possibleFileNames_parsed.SelectMany(x => x.Value).ToList());

                    var possibleFileNames_regex = new ConcurrentDictionary<string, List<string>>();
                    if (inMPQArchive.HasUnknownHashes || inMPQArchive.HasFakeFiles)
                    {
                        var discoveredFileNamesBackup_regex = inMPQArchive.DiscoveredFileNames.ToList();

                        var allExtractedFiles = Directory.GetFiles(ExtractedFilesPath, "*", SearchOption.AllDirectories).ToList();
                        possibleFileNames_regex = DeepScanFilesForPossibleFileNames_Regex(allExtractedFiles, unknownFileExtensions);
                        AddAlternateUnknownRecoveryFileNames(possibleFileNames_regex);
                        var allExternalReferencedFiles = possibleFileNames_parsed.SelectMany(x => x.Value).Concat(possibleFileNames_regex.SelectMany(x => x.Value)).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
                        DiscoverUnknownFileNames(inMPQArchive, allExternalReferencedFiles);
                        var recoveredFileNames_regex = inMPQArchive.DiscoveredFileNames.Except(discoveredFileNamesBackup_regex, StringComparer.InvariantCultureIgnoreCase).ToList();
                        if (recoveredFileNames_regex.Any())
                        {
                            DebugSettings.Warn("Delete DeepScanFilesForPossibleFileNames_Regex if this breakpoint is never hit");
                        }
                    }

                    var recoveredFileNames = discoveredFileNamesBackup.Except(inMPQArchive.DiscoveredFileNames, StringComparer.InvariantCultureIgnoreCase).ToList();
                    AddToGlobalListFile(recoveredFileNames);
                    _deprotectionResult.NewListFileEntriesFound += recoveredFileNames.Count;


                    if (DebugSettings.BenchmarkUnknownRecovery)
                    {
                        var scannedFileNameMappedToSources = possibleFileNames_parsed.Concat(possibleFileNames_regex).SelectMany(x => x.Value.Select(y => new KeyValuePair<string, string>(y, x.Key))).GroupBy(x => Path.GetFileName(x.Key), StringComparer.InvariantCultureIgnoreCase).ToDictionary(x => x.Key, x => x.Select(y => y.Value).ToList(), StringComparer.InvariantCultureIgnoreCase);
                        var usefulSources = recoveredFileNames.SelectMany(x => scannedFileNameMappedToSources.TryGetValue(Path.GetFileName(x), out var sources) ? sources : new List<string>()).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
                        var allSources = possibleFileNames_parsed.Concat(possibleFileNames_regex).Select(x => x.Key).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
                        var uselessSources = allSources.Except(usefulSources).ToList();
                        File.WriteAllLines(Path.Combine(WorkingFolderPath, "unknownScanning_usefulSources.log"), usefulSources);
                        File.WriteAllLines(Path.Combine(WorkingFolderPath, "unknownScanning_uselessSources.log"), uselessSources);
                    }
                }

                var beforeGlobalListFileCount = inMPQArchive.UnknownFileCount;
                if (DebugSettings.BenchmarkUnknownRecovery)
                {
                    string globalListFileBenchmarkMessage = "";
                    if (inMPQArchive.HasUnknownHashes || inMPQArchive.HasFakeFiles)
                    {
                        var discoveredFileNamesBackup_globalListFile = inMPQArchive.DiscoveredFileNames.ToList();
                        var discoveredFiles = inMPQArchive.ProcessListFile(globalListFileRainbowTable.Value);
                        foreach (var file in discoveredFiles)
                        {
                            VerifyActualAndPredictedExtensionsMatch(inMPQArchive, file);
                        }
                        var recoveredFileNames_globalListFile = inMPQArchive.DiscoveredFileNames.Except(discoveredFileNamesBackup_globalListFile, StringComparer.InvariantCultureIgnoreCase).ToList();
                        if (recoveredFileNames_globalListFile.Any())
                        {
                            globalListFileBenchmarkMessage = $" KnownInGlobalListFile: {recoveredFileNames_globalListFile.Count}";

                            DebugSettings.Warn("Research how to get these files!");
                        }
                    }

                    _deprotectionResult.WarningMessages.Add("Done benchmarking. Unknowns left: " + beforeGlobalListFileCount + globalListFileBenchmarkMessage);
                    return _deprotectionResult;
                }

                if (inMPQArchive.HasUnknownHashes || inMPQArchive.HasFakeFiles)
                {
                    //NOTE: If LiveGameScan finds a new filename under an MD5 hash that already has a known filename, keep copies of both files, because it might actually be a legitimately duplicate file since the game isn't going to look for fake file names.
                    //todo: add checkbox to disable this option?
                    //var cancellationToken = new CancellationToken();
                    //await LiveGameScanForUnknownFiles(inMPQArchive, _deprotectionResult);
                    //WaitForProcessToExit(process, cancellationToken);
                }

                if (Settings.BruteForceUnknowns && inMPQArchive.HasUnknownHashes)
                {
                    BruteForceUnknownFileNames(inMPQArchive);
                }

                if (inMPQArchive.FakeFileCount > 0)
                {
                    _deprotectionResult.WarningMessages.Add($"WARNING: MPQ Archive had some fake files and/or fake filenames. Some legimitimate files may have failed to extract or been extracted with the wrong file names.");
                }
                _deprotectionResult.UnknownFileCount = inMPQArchive.UnknownFileCount;
                if (_deprotectionResult.UnknownFileCount > 0)
                {
                    _deprotectionResult.WarningMessages.Add($"WARNING: {_deprotectionResult.UnknownFileCount} files have unresolved names");
                    _deprotectionResult.WarningMessages.Add("These files will be lost and deprotected map may be incomplete or even unusable!");
                    _deprotectionResult.WarningMessages.Add("You can try fixing by searching online for listfile.txt, using 'Brute force unknown files (SLOW)' option, or by using 'W3X Name Scanner' tool in MPQEditor.exe");
                }
            }

            PatchW3I();

            //These are probably protected, but the only way way to verify if they aren't is to parse the script (which is probably obfuscated), but if we can sucessfully parse, then we can just re-generate them to be safe.
            File.Delete(Path.Combine(DiscoveredFilesPath, "war3mapunits.doo"));
            File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.wct"));
            File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.wtg"));
            File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.w3r"));
            //todo: keep a copy of these & diff with decompiled versions from war3map.j so we can update _deprotectionResult.CountOfProtectionsFound

            var skinPath = Path.Combine(DiscoveredFilesPath, "war3mapSkin.txt");
            if (!File.Exists(skinPath))
            {
                File.WriteAllText(skinPath, "");
            }
            var parser = new FileIniDataParser(new IniParser.Parser.IniDataParser(new IniParserConfiguration() { SkipInvalidLines = true, AllowDuplicateKeys = true, AllowDuplicateSections = true, AllowKeysWithoutSection = true }));
            var ini = parser.ReadFile(skinPath);
            ini.Configuration.AssigmentSpacer = "";
            ini[decode("RnJhbWVEZWY=")][decode("VVBLRUVQX0hJR0g=")] = decode("REVQUk9URUNURUQ=");
            ini[decode("RnJhbWVEZWY=")][decode("VVBLRUVQX0xPVw==")] = decode("REVQUk9URUNURUQ=");
            ini[decode("RnJhbWVEZWY=")][decode("VVBLRUVQX05PTkU=")] = decode("REVQUk9URUNURUQ=");
            parser.WriteFile(skinPath, ini);

            if (!File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.j")) && !File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.lua")))
            {
                _deprotectionResult.CountOfProtectionsFound++;
            }

            var replace = "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61";
            while (File.Exists(Path.Combine(DiscoveredFilesPath, replace)))
            {
                replace = replace.Insert(replace.Length - 4, "_old");
            }
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61")))
            {
                File.Move(Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61"), Path.Combine(DiscoveredFilesPath, replace), true);
            }
            File.Copy(Path.Combine(BaseMapFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x33\x77\x69"), Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61"), true);
            var replace2 = "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78";
            while (File.Exists(Path.Combine(DiscoveredFilesPath, replace2)))
            {
                replace2 = replace2.Insert(replace2.Length - 4, "_old");
            }
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78")))
            {
                File.Move(Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78"), Path.Combine(DiscoveredFilesPath, replace2), true);
            }
            File.Copy(Path.Combine(BaseMapFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x33\x77\x6D"), Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78"), true);

            var scriptFiles = Directory.GetFiles(DiscoveredFilesPath, "war3map.j", SearchOption.AllDirectories).Union(Directory.GetFiles(DiscoveredFilesPath, "war3map.lua", SearchOption.AllDirectories)).ToList();
            foreach (var scriptFile in scriptFiles)
            {
                var basePathScriptFileName = Path.Combine(DiscoveredFilesPath, Path.GetFileName(scriptFile));
                if (File.Exists(basePathScriptFileName) && !string.Equals(scriptFile, basePathScriptFileName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (File.ReadAllText(scriptFile) != File.ReadAllText(basePathScriptFileName))
                    {
                        _deprotectionResult.CriticalWarningCount++;
                        _deprotectionResult.WarningMessages.Add("WARNING: Multiple possible script files found. Please review TempFiles to see which one is correct and copy/paste directly into trigger editor or use MPQ tool to replace war3map.j or war3map.lua file");
                        _deprotectionResult.WarningMessages.Add($"TempFilePath: {WorkingFolderPath}");
                    }
                }
                File.Move(scriptFile, basePathScriptFileName, true);
                _logEvent($"Moving '{scriptFile}' to '{basePathScriptFileName}'");
            }

            File.Copy(Path.Combine(BaseMapFilesPath, "war3map.3ws"), Path.Combine(DiscoveredFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76"), true);

            string jassScript = null;
            string luaScript = null;
            ScriptMetaData scriptMetaData = null;
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.j")))
            {
                jassScript = $"// {ATTRIB}{File.ReadAllText(Path.Combine(DiscoveredFilesPath, "war3map.j"))}";
                try
                {
                    jassScript = DeObfuscateJassScript(jassScript);
                    scriptMetaData = DecompileJassScriptMetaData(jassScript);
                }
                catch { }
            }

            if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.lua")))
            {
                _deprotectionResult.WarningMessages.Add("WARNING: This map was built using Lua instead of Jass. Deprotection of Lua maps is not fully supported yet. It will open in the editor, but the render screen will be missing units/items/regions/cameras/sounds.");
                luaScript = DeObfuscateLuaScript($"-- {ATTRIB}{File.ReadAllText(Path.Combine(DiscoveredFilesPath, "war3map.lua"))}");
                var temporaryScriptMetaData = DecompileLuaScriptMetaData(luaScript);
                if (scriptMetaData == null)
                {
                    scriptMetaData = temporaryScriptMetaData;
                }
                else
                {
                    scriptMetaData.Info ??= temporaryScriptMetaData.Info;
                    scriptMetaData.Sounds ??= temporaryScriptMetaData.Sounds;
                    scriptMetaData.Cameras ??= temporaryScriptMetaData.Cameras;
                    scriptMetaData.Regions ??= temporaryScriptMetaData.Regions;
                    scriptMetaData.Triggers ??= temporaryScriptMetaData.Triggers;
                    scriptMetaData.Units ??= temporaryScriptMetaData.Units;
                }
            }

            if (scriptMetaData != null)
            {
                var decompiledFiles = scriptMetaData.ConvertToFiles().ToList();
                foreach (var file in decompiledFiles)
                {
                    if (!File.Exists(Path.Combine(DiscoveredFilesPath, file.FileName)))
                    {
                        _deprotectionResult.CountOfProtectionsFound++;
                    }

                    SaveDecompiledArchiveFile(file);
                }

                if (Settings.CreateVisualTriggers) //todo: Remove setting & checkbox from form & always run from same instance & output 2 files _TextTriggers & _GUITriggers - Don't call Deprotect() 2x, actually output both versions from same method call to avoid duplicate processing)
                {
                    if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.wtg")))
                    {
                        _deprotectionResult.WarningMessages.Add("Note: Visual trigger recovery is experimental. If world editor crashes, or you have too many compiler errors when saving in WorldEditor, try disabling this feature.");
                    }
                    else
                    {
                        _deprotectionResult.WarningMessages.Add("Note: Visual triggers could not be recovered. Using custom script instead.");
                    }
                }
                else
                {
                    File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.wct"));
                    File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.wtg"));
                }
            }

            if (string.IsNullOrWhiteSpace(luaScript) && Settings.TranspileJassToLUA && !string.IsNullOrWhiteSpace(jassScript))
            {
                _logEvent("Transpiling JASS to LUA");

                luaScript = ConvertJassToLua(jassScript);
                if (!string.IsNullOrWhiteSpace(luaScript))
                {
                    File.WriteAllText(Path.Combine(DiscoveredFilesPath, "war3map.lua"), luaScript);
                    File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.j"));
                    _logEvent("Created war3map.lua");

                    try
                    {
                        var map = DecompileMap(x => {
                            if (x.Info != null)
                            {
                                x.Info.ScriptLanguage = ScriptLanguage.Lua;
                            }
                        });
                        var mapFiles = map.GetAllFiles();

                        var fileExtensionsToReplace = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".w3i", ".wts", ".wtg", ".wct" };
                        var filesToReplace = mapFiles.Where(x => fileExtensionsToReplace.Contains(Path.GetExtension(x.FileName))).ToList();
                        foreach (var file in filesToReplace)
                        {
                            SaveDecompiledArchiveFile(file);
                        }

                        if (Settings.CreateVisualTriggers)
                        {
                            if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.wtg")))
                            {
                                _deprotectionResult.WarningMessages.Add("Note: Visual trigger recovery is experimental. If world editor crashes, or you have too many compiler errors when saving in WorldEditor, try disabling this feature.");
                            }
                            else
                            {
                                _deprotectionResult.WarningMessages.Add("Note: Visual triggers could not be recovered. Using custom script instead.");
                            }
                        }
                        else
                        {
                            File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.wct"));
                            File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.wtg"));
                        }

                        File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.j"));
                    }
                    catch { }
                }
            }

            if (luaScript != null)
            {
                if (!File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.wtg")))
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                    CreateLuaCustomTextVisualTriggerFile(luaScript);
                }
            }
            else if (jassScript != null)
            {
                if (!File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.wtg")))
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                    WriteWtg_PlainText_Jass(jassScript);
                    //CreateJassCustomTextVisualTriggerFile(jassScript); // unfinished & buggy [causing compiler errors]
                }
            }

            if (!File.Exists(Path.Combine(DiscoveredFilesPath, "war3mapunits.doo")))
            {
                _deprotectionResult.CountOfProtectionsFound++;
                File.Copy(Path.Combine(BaseMapFilesPath, "war3mapunits.doo"), Path.Combine(DiscoveredFilesPath, "war3mapunits.doo"), true);
                _deprotectionResult.CriticalWarningCount++;
                _deprotectionResult.WarningMessages.Add("WARNING: war3mapunits.doo could not be recovered. Map will still open in WorldEditor & run, but units will not be visible in WorldEditor rendering and saving in world editor will corrupt your war3map.j or war3map.lua script file.");
            }

            if (!File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.wtg")))
            {
                _deprotectionResult.CountOfProtectionsFound++;
                File.Copy(Path.Combine(BaseMapFilesPath, "war3map.wtg"), Path.Combine(DiscoveredFilesPath, "war3map.wtg"), true);
                File.Delete(Path.Combine(DiscoveredFilesPath, "war3map.wct"));
                _deprotectionResult.CriticalWarningCount++;
                _deprotectionResult.WarningMessages.Add("WARNING: triggers could not be recovered. Map will still open in WorldEditor & run, but saving in world editor will corrupt your war3map.j or war3map.lua script file.");
            }

            BuildImportList();

            var finalFiles = Directory.GetFiles(DiscoveredFilesPath, "*", SearchOption.AllDirectories).ToList();

            BuildW3X(_outMapFile, DiscoveredFilesPath, finalFiles);

            if (_deprotectionResult.NewListFileEntriesFound > 0)
            {
                _logEvent($"{_deprotectionResult.NewListFileEntriesFound} new List File Entries Found! File stored at: {WorkingListFileName}");
            }

            _deprotectionResult.WarningMessages.Add("NOTE: You may need to fix script compiler errors before saving in world editor.");
            _deprotectionResult.WarningMessages.Add("NOTE: Objects added directly to editor render screen, like units/doodads/items/etc are stored in an editor-only file called war3mapunits.doo and converted to script code on save. Protection deletes the editor file and obfuscates the script code to make it harder to recover. Decompiling the war3map script file back into war3mapunits.doo is not 100% perfect for most maps. Please do extensive testing to ensure everything still behaves correctly, you may have to do many manual bug fixes in world editor after deprotection.");
            _deprotectionResult.WarningMessages.Add("NOTE: If deprotected map works correctly, but becomes corrupted after saving in world editor, it is due to editor-generated code in your war3map.j or war3map.lua. You should delete WorldEditor triggers, keep a backup of the war3map.j or war3map.lua script file, edit with visual studio code, and add it with MPQ tool after saving in WorldEditor. The downside to this approach is any objects you manually add to the rendering screen will get saved to the broken script file, so you will need to use a tool like WinMerge to diff the old/new script file and copy any editor-generated changes to your backup script file.");
            _deprotectionResult.WarningMessages.Add("NOTE: Editor-generated functions in trigger window have been renamed with a suffix of _old. If saving in world editor causes game to become corrupted, check the _old functions to find code that may need to be moved to an initialization script.");

            _deprotectionResult.WarningMessages = _deprotectionResult.WarningMessages.Distinct().ToList();

            return _deprotectionResult;
        }

        protected string RecoverNativeEditorFunctionsLua(string luaScript)
        {
            //todo: code this!

            var ast = ParseLuaScript(luaScript);
            var config = ast.Body.Where(x => x.Type == LuaASTType.FunctionDeclaration && x.Name == "config").FirstOrDefault();
            var main = ast.Body.Where(x => x.Type == LuaASTType.FunctionDeclaration && x.Name == "main").FirstOrDefault();

            return luaScript;
        }

        protected string ConvertJassToLua(string jassScript)
        {
            try
            {
                var transpiler = new JassToLuaTranspiler();
                transpiler.IgnoreComments = true;
                transpiler.IgnoreEmptyDeclarations = true;
                transpiler.IgnoreEmptyStatements = true;
                transpiler.KeepFunctionsSeparated = true;

                transpiler.RegisterJassFile(JassSyntaxFactory.ParseCompilationUnit(File.ReadAllText(Path.Combine(ExeFolderPath, "common.j"))));
                transpiler.RegisterJassFile(JassSyntaxFactory.ParseCompilationUnit(File.ReadAllText(Path.Combine(ExeFolderPath, "blizzard.j"))));
                var jassParsed = JassSyntaxFactory.ParseCompilationUnit(jassScript);

                var luaCompilationUnit = transpiler.Transpile(jassParsed);
                var result = new StringBuilder();
                using (var writer = new StringWriter(result))
                {
                    var luaRendererOptions = new LuaSyntaxGenerator.SettingInfo
                    {
                        Indent = 2
                    };

                    var luaRenderer = new LuaRenderer(luaRendererOptions, writer);
                    luaRenderer.RenderCompilationUnit(luaCompilationUnit);
                }

                return result.ToString();
            }
            catch
            {
                return null;
            }
        }
        [GeneratedRegex(@"\s+call\s+InitBlizzard\s*\(\s*\)\s*", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_JassScriptInitBlizzard();

        protected void BuildW3X(string fileName, string baseFolder, List<string> filesToInclude)
        {
            _logEvent("Building map archive...");
            var tempmpqfile = Path.Combine(WorkingFolderPath, "out.mpq");
            if (File.Exists(tempmpqfile))
            {
                File.Delete(tempmpqfile);
            }

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            if (File.Exists(Path.Combine(baseFolder, "war3map.j")))
            {
                var script = File.ReadAllText(Path.Combine(baseFolder, "war3map.j"));
                var blz = Regex_JassScriptInitBlizzard().Match(script);
                if (blz.Success)
                {
                    var bits = new byte[] { 0b_00001101, 0b_00001010, 0b_01100011, 0b_01100001, 0b_01101100, 0b_01101100, 0b_00100000, 0b_01000100, 0b_01101001, 0b_01110011, 0b_01110000, 0b_01101100, 0b_01100001, 0b_01111001, 0b_01010100, 0b_01100101, 0b_01111000, 0b_01110100, 0b_01010100, 0b_01101111, 0b_01000110, 0b_01101111, 0b_01110010, 0b_01100011, 0b_01100101, 0b_00101000, 0b_01000111, 0b_01100101, 0b_01110100, 0b_01010000, 0b_01101100, 0b_01100001, 0b_01111001, 0b_01100101, 0b_01110010, 0b_01110011, 0b_01000001, 0b_01101100, 0b_01101100, 0b_00101000, 0b_00101001, 0b_00101100, 0b_00100000, 0b_00100010, 0b_01100100, 0b_00110011, 0b_01110000, 0b_01110010, 0b_00110000, 0b_01110100, 0b_00110011, 0b_01100011, 0b_01110100, 0b_00110011, 0b_01100100, 0b_00100010, 0b_00101001, 0b_00001101, 0b_00001010 };
                    for (var idx = 0; idx < bits.Length; ++idx)
                    {
                        script = script.Insert(blz.Index + blz.Length + idx, ((char)bits[idx]).ToString());
                    }
                }

                File.WriteAllText(Path.Combine(baseFolder, "war3map.j"), script);
            }
            else if (File.Exists(Path.Combine(baseFolder, "war3map.lua")))
            {
                //todo
            }

            var mpqArchive = new MpqArchiveBuilder();
            foreach (var file in filesToInclude)
            {
                var shortFileName = file.Replace($"{baseFolder}\\", "", StringComparison.InvariantCultureIgnoreCase);
                var mpqFile = MpqFile.New(File.OpenRead(file), shortFileName);
                mpqFile.CompressionType = MpqCompressionType.ZLib;

                /*
                // not supported by War3Net yet [use StormLib?]
                if (string.Equals(Path.GetExtension(shortFileName), ".wav", StringComparison.InvariantCultureIgnoreCase))
                {
                    mpqFile.CompressionType = MpqCompressionType.Huffman;
                }
                */
                
                mpqArchive.AddFile(mpqFile);

                _logEvent($"Added to MPQ: {file}");
            }

            mpqArchive.SaveTo(tempmpqfile);
            _logEvent($"Created MPQ with {filesToInclude.Count} files");

            if (!File.Exists(tempmpqfile))
            {
                throw new Exception("Failed to create output MPQ archive");
            }

            var header = new byte[512];
            using (var srcmap = File.OpenRead(_inMapFile))
            {
                srcmap.Read(header, 0, 512);
            }
            if (header[0] == 'H' && header[1] == 'M' && header[2] == '3' && header[3] == 'W')
            {
                _logEvent("Copying HM3W header");
                using (var outmap = File.OpenWrite(fileName))
                {
                    outmap.Write(header, 0, 512);
                    using (var mpq = File.OpenRead(tempmpqfile))
                    {
                        mpq.CopyTo(outmap);
                    }
                }
            }
            else
            {
                File.Copy(tempmpqfile, fileName, true);
            }
            _logEvent($"Created map '{fileName}'");
        }

        protected List<War3Net.Build.Environment.Region> ReadRegions(string regionsFileName, out bool wasProtected)
        {
            wasProtected = false;
            var result = new List<War3Net.Build.Environment.Region>();

            using (var reader = new BinaryReader(File.OpenRead(regionsFileName)))
            {
                var formatVersion = (MapRegionsFormatVersion)reader.ReadInt32();
                var regionCount = reader.ReadInt32();
                for (var i = 0; i < regionCount; i++)
                {
                    try
                    {
                        result.Add(reader.ReadRegion(formatVersion));
                    }
                    catch
                    {
                        wasProtected = true;
                        break;
                    }
                }
            }

            return result;
        }

        protected bool SaveDecompiledArchiveFile(MpqKnownFile mpqFile)
        {
            var fileName = Path.Combine(DiscoveredFilesPath, mpqFile.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            using (var stream = new FileStream(fileName, FileMode.OpenOrCreate))
            {
                mpqFile.MpqStream.CopyTo(stream);
            }

            _logEvent($"Reconstructed by parsing other map files: {fileName}");

            return true;
        }

        protected bool ExtractFileFromArchive(StormMPQArchive archive, string archiveFileName)
        {
            if (!archive.DiscoverFile(archiveFileName, out var md5Hash))
            {
                return false;
            }

            _logEvent($"Extracted from MPQ: {archiveFileName}");

            VerifyActualAndPredictedExtensionsMatch(archive, archiveFileName);
            return true;
        }

        protected void VerifyActualAndPredictedExtensionsMatch(StormMPQArchive archive, string archiveFileName)
        {
            if (!DebugSettings.BenchmarkUnknownRecovery || archive.HasFakeFiles)
            {
                return;
            }

            if (!archive.DiscoverFile(archiveFileName, out var md5Hash))
            {
                return;
            }

            var isPseudoFileName = archive.IsPseudoFileName(archiveFileName);
            var realExtension = Path.GetExtension(archiveFileName);
            var predictedExtension = archive.GetPredictedFileExtension(md5Hash);

            if (!isPseudoFileName && !realExtension.Equals(predictedExtension, StringComparison.InvariantCultureIgnoreCase) && !StormMPQArchiveExtensions.IsInDefaultListFile(archiveFileName))
            {
                //NOTE: sometimes there are multiple valid extensions for a file. Sometimes a map maker accidentally names something wrong. These formats give a lot of false positives during testing and the file detection code for these types has already been tested to be correct.
                var ignoreMistakenPrediction = (string.Equals(realExtension, ".ini") && string.Equals(predictedExtension, ".txt")) ||
                    (string.Equals(predictedExtension, ".ini") && string.Equals(realExtension, ".txt")) ||
                    (string.Equals(realExtension, ".wav") && string.Equals(predictedExtension, ".mp3")) ||
                    (string.Equals(predictedExtension, ".wav") && string.Equals(realExtension, ".mp3")) ||
                    (string.Equals(realExtension, ".mdx") && string.Equals(predictedExtension, ".blp")) ||
                    (string.Equals(predictedExtension, ".mdx") && string.Equals(realExtension, ".blp"));
                if (!ignoreMistakenPrediction)
                {
                    DebugSettings.Warn("Possible bug in PredictUnknownFileExtension");
                }
            }
        }

        protected List<string> GetPredictedUnknownFileExtensions()
        {
            var extensions = new HashSet<string>(GetUnknownFileNames().Select(x => Path.GetExtension(x)), StringComparer.InvariantCultureIgnoreCase);
            if (extensions.Contains(""))
            {
                extensions.Remove("");
                extensions.AddRange(_commonFileExtensions);
            }
            return extensions.ToList();
        }

        protected List<string> GetUnknownFileNames()
        {
            Directory.CreateDirectory(UnknownFilesPath);
            return Directory.GetFiles(UnknownFilesPath, "*", SearchOption.TopDirectoryOnly).Select(x => x.Replace($"{UnknownFilesPath}\\", "", StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        protected void BruteForceUnknownFileNames(StormMPQArchive archive)
        {
            //todo: Convert to Vector<> variables to support SIMD architecture speed up
            var unknownFileCount = archive.UnknownFileCount;
            _logEvent($"unknown files remaining: {unknownFileCount}");

            var directories = archive.DiscoveredFileNames.Select(x => Path.GetDirectoryName(x).ToUpper()).Select(x => x.Trim('\\')).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            var extensions = GetPredictedUnknownFileExtensions();

            const int maxFileNameLength = 75;
            _logEvent($"Brute forcing filenames from length 1 to {maxFileNameLength}");

            var possibleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_!&()' .".ToUpper().ToCharArray();

            var leftHashes = new HashSet<uint>(archive.MPQFileNameLeftHashes);
            var rightHashes = new HashSet<uint>(archive.MPQFileNameRightHashes);

            var foundFileLock = new object();
            var foundFileCount = 0;
            try
            {
                var simdLength = Vector<int>.Count;
                Parallel.ForEach(directories, new ParallelOptions() { CancellationToken = Settings.BruteForceCancellationToken.Token }, directoryName =>
                {
                    var directoryHash = new MPQPartialHash(MPQPartialHash.LEFT_OFFSET);
                    if (!directoryHash.TryAddString(directoryName + "\\"))
                    {
                        return;
                    }

                    var testCallback = (string bruteText) =>
                    {
                        //todo: refactor to save file prefix hash so it only needs to update based on the most recent character changed
                        var bruteTextHash = directoryHash;
                        bruteTextHash.AddString(bruteText);

                        foreach (var fileExtension in extensions)
                        {
                            var fileHash = bruteTextHash;
                            fileHash.AddString(fileExtension);
                            var hash = fileHash.Value;
                            if (leftHashes.Contains(hash))
                            {
                                var fileName = Path.Combine(directoryName, $"{bruteText}{fileExtension}");
                                if (MPQPartialHash.TryCalculate(fileName, MPQPartialHash.RIGHT_OFFSET, out var rightHash) && rightHashes.Contains(rightHash))
                                {
                                    lock (foundFileLock)
                                    {
                                        if (!_extractedMapFiles.Contains(fileName) && ExtractFileFromArchive(archive, fileName))
                                        {
                                            _deprotectionResult.NewListFileEntriesFound++;
                                            AddToGlobalListFile(fileName);

                                            var newUnknownCount = archive.UnknownFileCount;
                                            if (unknownFileCount != newUnknownCount)
                                            {
                                                foundFileCount++;
                                                unknownFileCount = newUnknownCount;
                                                _logEvent($"unknown files remaining: {unknownFileCount}");

                                                if (!archive.HasUnknownHashes)
                                                {
                                                    Settings.BruteForceCancellationToken.Cancel();
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    };

                    for (var letterCount = 1; letterCount <= maxFileNameLength; ++letterCount)
                    {
                        _logEvent($"Brute Forcing Length {letterCount}. Directory: {directoryName}");

                        var chars = new char[letterCount];

                        for (var i = 0; i < letterCount; ++i)
                        {
                            chars[i] = possibleCharacters[0];
                        }

                        testCallback(new string(chars));

                        for (var i1 = letterCount - 1; i1 > -1; --i1)
                        {
                            int i2;

                            for (i2 = possibleCharacters.IndexOf(chars[i1]) + 1; i2 < possibleCharacters.Length; ++i2)
                            {
                                chars[i1] = possibleCharacters[i2];

                                testCallback(new string(chars));
                                for (var i3 = i1 + 1; i3 < letterCount; ++i3)
                                {
                                    if (chars[i3] != possibleCharacters[possibleCharacters.Length - 1])
                                    {
                                        i1 = letterCount;
                                        goto outerBreak;
                                    }
                                }
                            }

                        outerBreak:
                            if (i2 == possibleCharacters.Length)
                            {
                                chars[i1] = possibleCharacters[0];
                            }
                        }
                    }
                });
            }
            catch (OperationCanceledException e)
            {
                //ignore
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("User Cancelled"))
                {
                    throw;
                }
            }

            _logEvent($"Brute force found {foundFileCount} files");
        }

        protected void AddAlternateUnknownRecoveryFileNames(IDictionary<string, List<string>> sourceFileNameMappedToExternalFileReferences)
        {
            foreach (var scan in sourceFileNameMappedToExternalFileReferences)
            {
                var alternateNames = scan.Value.SelectMany(x => GetAlternateUnknownRecoveryFileNames(x));
                sourceFileNameMappedToExternalFileReferences[scan.Key] = scan.Value.Concat(alternateNames).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            }
        }

        protected List<HashSet<string>> _alternateUnknownRecoveryFileNames = new List<HashSet<string>>()
        {
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".w3m", ".w3x", ".mpq", ".pud" },
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".txt", ".ini" },
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".j", ".lua" },
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".wav", ".mp3", ".flac", ".aif", ".aiff" },
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".ttf", ".otf", ".woff" },
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".mdl", ".mdx", ".blp", ".tga", ".jpg", ".dds", ".gif" }
        };

        protected List<string> GetAlternateUnknownRecoveryFileNames(string potentialFileName)
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension(potentialFileName)))
            {
                return new List<string>();
            }

            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { potentialFileName };
            if (potentialFileName.EndsWith(".blp", StringComparison.InvariantCultureIgnoreCase) || potentialFileName.EndsWith(".tga", StringComparison.InvariantCultureIgnoreCase) || potentialFileName.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase))
            {
                result.Add($"DIS{Path.GetFileName(potentialFileName)}");
            }
            if (potentialFileName.EndsWith(".mdl", StringComparison.InvariantCultureIgnoreCase) || potentialFileName.EndsWith(".mdx", StringComparison.InvariantCultureIgnoreCase))
            {
                var directory = Path.GetDirectoryName(potentialFileName) ?? "";
                result.Add($"{Path.Combine(directory, Path.GetFileNameWithoutExtension(potentialFileName))}_PORTRAIT{Path.GetExtension(potentialFileName)}");
            }

            var oldCount = result.Count;
            do
            {
                oldCount = result.Count;
                foreach (var fileName in result.ToList())
                {
                    foreach (var alternateExtensionList in _alternateUnknownRecoveryFileNames)
                    {
                        if (alternateExtensionList.Contains(Path.GetExtension(fileName)))
                        {
                            result.AddRange(alternateExtensionList.Select(x => Path.ChangeExtension(fileName, x)));
                        }
                    }
                }
            } while (oldCount != result.Count);

            return result.ToList();
        }

        protected void DiscoverUnknownFileNames(StormMPQArchive archive, List<string> fileNamesToTest)
        {
            var filesFound = 0;
            try
            {
                _logEvent("Performing deep scan for unknown files ...");

                var baseFileNames = new HashSet<string>(fileNamesToTest.Select(x => Path.GetFileName(x).Trim('\\')), StringComparer.InvariantCultureIgnoreCase);

                var directories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                directories.Add(@"REPLACEABLETEXTURES\COMMANDBUTTONSDISABLED");
                directories.AddRange(archive.DiscoveredFileNames.Select(x => Path.GetDirectoryName(x)).Select(x => x.Trim('\\')).Distinct(StringComparer.InvariantCultureIgnoreCase));
                directories.AddRange(fileNamesToTest.Select(x => Path.GetDirectoryName(x.Replace("/", "\\").Trim('\\'))).Where(x => x != null));
                foreach (var directory in directories.ToList())
                {
                    var split = directory.Split('\\');
                    for (int i = 1; i <= split.Length; i++)
                    {
                        directories.Add(split.Take(i).Aggregate((x, y) => $"{x}\\{y}"));
                    }
                }

                //todo: Disable deep scanning of directories unless Benchmark is enabled. Add separate deep scan of File Extensions if Benchmark is enabled. Delete each one if they don't produce any better results than when it's disabled.
                //todo: If directory name is blank, do check for each shorter version of file.ext (ile.ext le.ext l.ext e.ext .ext)

                var finishedSearching = false;
                var leftHashes = new HashSet<uint>(archive.MPQFileNameLeftHashes);
                var rightHashes = new HashSet<uint>(archive.MPQFileNameRightHashes);
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
                        if (fileHash.TryAddString(fileName) && leftHashes.Contains(fileHash.Value))
                        {
                            var fullFileName = $"{hashPrefix}{fileName}";

                            if (MPQPartialHash.TryCalculate(fullFileName, MPQPartialHash.RIGHT_OFFSET, out var rightHash) && rightHashes.Contains(rightHash))
                            {
                                if (ExtractFileFromArchive(archive, fullFileName))
                                {
                                    _extractedMapFiles.Add(fullFileName);
                                    filesFound++;

                                    //NOTE: If it has fake files, we keep scanning until entire file list is exhausted, because we may find a better name for an already resolved file
                                    if (!archive.HasFakeFiles && !archive.HasUnknownHashes)
                                    {
                                        finishedSearching = true;
                                    }
                                }
                            }
                        }
                    }
                });
            }
            finally
            {
                _logEvent($"Deep Scan completed, {filesFound} filenames found");
            }
        }

        [GeneratedRegex(@"\$[0-9a-f]{8}", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ScriptObfuscatedFourCC();

        protected string DeObfuscateFourCC(string script, string prefix, string suffix)
        {
            var result = Regex_ScriptObfuscatedFourCC().Replace(script, x =>
            {
                var byte1 = Convert.ToByte(x.Value.Substring(1, 2), 16);
                var byte2 = Convert.ToByte(x.Value.Substring(3, 2), 16);
                var byte3 = Convert.ToByte(x.Value.Substring(5, 2), 16);
                var byte4 = Convert.ToByte(x.Value.Substring(7, 2), 16);
                return $"{prefix}{Encoding.ASCII.GetString(new byte[] { byte1, byte2, byte3, byte4 })}{suffix}";
            });

            if (script != result)
            {
                _deprotectionResult.CountOfProtectionsFound++;
                _logEvent("FourCC codes de-obfuscated");
            }

            return result;
        }

        protected string DeObfuscateFourCCJass(string jassScript)
        {
            return DeObfuscateFourCC(jassScript, "'", "'");
        }

        protected string DeObfuscateFourCCLua(string jassScript)
        {
            return DeObfuscateFourCC(jassScript, "FourCC(\"", "\")");
        }

        protected List<IStatementSyntax> ExtractStatements_IncludingEnteringFunctionCalls(IndexedJassCompilationUnitSyntax indexedCompilationUnit, string startingFunctionName, out List<string> inlinedFunctions)
        {
            var outInlinedFunctions = new List<string>();

            if (!indexedCompilationUnit.IndexedFunctions.TryGetValue(startingFunctionName, out var function))
            {
                inlinedFunctions = outInlinedFunctions;
                return new List<IStatementSyntax>();
            }

            var result = function.Body.Statements.DFS_Flatten(x =>
            {
                if (x is JassFunctionReferenceExpressionSyntax functionReference)
                {
                    if (indexedCompilationUnit.IndexedFunctions.TryGetValue(functionReference.IdentifierName.Name, out var nestedFunctionCall))
                    {
                        outInlinedFunctions.Add(functionReference.IdentifierName.Name);
                        return nestedFunctionCall.Body.Statements;
                    }
                }
                else if (x is JassCallStatementSyntax callStatement)
                {
                    if (indexedCompilationUnit.IndexedFunctions.TryGetValue(callStatement.IdentifierName.Name, out var nestedFunctionCall))
                    {
                        outInlinedFunctions.Add(callStatement.IdentifierName.Name);
                        return nestedFunctionCall.Body.Statements;
                    }
                    else if (string.Equals(callStatement.IdentifierName.Name, "ExecuteFunc", StringComparison.InvariantCultureIgnoreCase) && callStatement.Arguments.Arguments.FirstOrDefault() is JassStringLiteralExpressionSyntax execFunctionName)
                    {
                        if (indexedCompilationUnit.IndexedFunctions.TryGetValue(execFunctionName.Value, out var execNestedFunctionCall))
                        {
                            outInlinedFunctions.Add(execFunctionName.Value);
                            return execNestedFunctionCall.Body.Statements;
                        }
                    }
                }

                return null;
            }).ToList();

            inlinedFunctions = outInlinedFunctions;
            return result;
        }

        protected JassCompilationUnitSyntax ParseJassScript(string jassScript)
        {
            return JassSyntaxFactory.ParseCompilationUnit(jassScript);
        }

        protected void SplitUserDefinedAndGlobalGeneratedGlobalVariableNames(string jassScript, out List<string> userDefinedGlobals, out List<string> globalGenerateds)
        {
            var jassParsed = new IndexedJassCompilationUnitSyntax(ParseJassScript(jassScript));

            var globals = jassParsed.CompilationUnit.Declarations.Where(x => x is JassGlobalDeclarationListSyntax).Cast<JassGlobalDeclarationListSyntax>().SelectMany(x => x.Globals).Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>().ToList();

            var probablyUserDefined = new HashSet<string>();
            var mainStatements = ExtractStatements_IncludingEnteringFunctionCalls(jassParsed, "main", out var mainInlinedFunctions);
            var initBlizzardIndex = mainStatements.IndexOf(x => x is JassCallStatementSyntax callStatement && string.Equals(callStatement.IdentifierName.Name, "InitBlizzard", StringComparison.InvariantCultureIgnoreCase));
            if (initBlizzardIndex != -1)
            {
                var userDefinedVariableStatements = mainStatements.Skip(initBlizzardIndex).ToList();
                foreach (var udvStatement in userDefinedVariableStatements)
                {
                    probablyUserDefined.AddRange(War3NetExtensions.GetAllChildSyntaxNodes_Recursive(udvStatement).Where(x => x is JassIdentifierNameSyntax).Cast<JassIdentifierNameSyntax>().Select(x => x.Name));
                }
            }

            userDefinedGlobals = new List<string>();
            globalGenerateds = new List<string>();
            var possiblyGlobalTypes = new HashSet<string>() { "rect", "camerasetup", "sound", "unit", "destructable", "item" };
            foreach (var global in globals)
            {
                var variableName = global.Declarator.IdentifierName.Name;
                var variableType = global.Declarator.Type.TypeName.Name;
                if (string.Equals(variableType, "trigger", StringComparison.InvariantCultureIgnoreCase))
                {
                    //extremely rare to be user-defined & visual triggers fail to decompiled without this, so we always force gg_
                    globalGenerateds.Add(variableName);
                }
                else if (global.Declarator is JassArrayDeclaratorSyntax || probablyUserDefined.Contains(variableName) || !possiblyGlobalTypes.Contains(variableType))
                {
                    userDefinedGlobals.Add(variableName);
                }
                else
                {
                    globalGenerateds.Add(variableName);
                }
            }
        }

        [GeneratedRegex(@"^[0-9a-z]{4}$", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ScriptFourCC();

        protected ScriptMetaData DecompileJassScriptMetaData(string jassScript)
        {
            var DecompileJassScriptMetaData_Internal = (string editorSpecificJassScript) =>
            {
                var result = new ScriptMetaData();

                var map = DecompileMap();
                result.Info = map.Info;

                result.TriggerStrings = map.TriggerStrings;

                foreach (var mapInfoFormatVersion in Enum.GetValues(typeof(MapInfoFormatVersion)).Cast<MapInfoFormatVersion>().OrderBy(x => x == map?.Info?.FormatVersion ? 0 : 1).ThenByDescending(x => x))
                {
                    var useNewFormat = map.Info.FormatVersion >= MapInfoFormatVersion.v28;

                    if (result.Sounds != null && result.Cameras != null && result.Regions != null && result.Triggers != null && result.Units != null)
                    {
                        _logEvent("Decompiling script finished");
                        break;
                    }

                    JassScriptDecompiler jassScriptDecompiler;
                    try
                    {
                        _logEvent("Decompiling war3map script file");
                        jassScriptDecompiler = new JassScriptDecompiler(new Map() { Script = editorSpecificJassScript, Info = new MapInfo(mapInfoFormatVersion) { ScriptLanguage = ScriptLanguage.Jass } });
                    }
                    catch
                    {
                        return null;
                    }

                    if (result.Sounds == null)
                    {
                        _logEvent("Decompiling map sounds");
                        foreach (var enumValue in Enum.GetValues(typeof(MapSoundsFormatVersion)).Cast<MapSoundsFormatVersion>().OrderBy(x => x == map?.Sounds?.FormatVersion ? 0 : 1).ThenByDescending(x => x))
                        {
                            MapSounds sounds;
                            try
                            {
                                if (jassScriptDecompiler.TryDecompileMapSounds(enumValue, out sounds))
                                {
                                    _logEvent("map sounds recovered");
                                    result.Sounds = sounds;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }

                    if (result.Cameras == null)
                    {
                        _logEvent("Decompiling map cameras");
                        foreach (var enumValue in Enum.GetValues(typeof(MapCamerasFormatVersion)).Cast<MapCamerasFormatVersion>().OrderBy(x => x == map?.Cameras?.FormatVersion ? 0 : 1).ThenByDescending(x => x))
                        {
                            MapCameras cameras;
                            try
                            {
                                if (jassScriptDecompiler.TryDecompileMapCameras(enumValue, useNewFormat, out cameras))
                                {
                                    _logEvent("map cameras recovered");
                                    result.Cameras = cameras;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }

                    if (result.Regions == null)
                    {
                        _logEvent("Decompiling map regions");
                        foreach (var enumValue in Enum.GetValues(typeof(MapRegionsFormatVersion)).Cast<MapRegionsFormatVersion>().OrderBy(x => x == map?.Regions?.FormatVersion ? 0 : 1).ThenByDescending(x => x))
                        {
                            MapRegions regions;
                            try
                            {
                                if (jassScriptDecompiler.TryDecompileMapRegions(enumValue, out regions))
                                {
                                    _logEvent("map regions recovered");
                                    result.Regions = regions;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }

                    if (result.Triggers == null && Settings.CreateVisualTriggers)
                    {
                        _logEvent("Decompiling map triggers");
                        foreach (var enumValue in Enum.GetValues(typeof(MapTriggersFormatVersion)).Cast<MapTriggersFormatVersion>().OrderBy(x => x == map?.Triggers?.FormatVersion ? 0 : 1).ThenByDescending(x => x))
                        {
                            foreach (var subEnumValue in Enum.GetValues(typeof(MapTriggersSubVersion)).Cast<MapTriggersSubVersion>().OrderBy(x => x == map?.Triggers?.SubVersion ? 0 : 1).ThenByDescending(x => x))
                            {
                                MapTriggers triggers;
                                try
                                {
                                    if (jassScriptDecompiler.TryDecompileMapTriggers(enumValue, subEnumValue, out triggers))
                                    {
                                        var triggerDefinitions = triggers.TriggerItems.Where(x => x is TriggerDefinition).Cast<TriggerDefinition>().ToList();
                                        if (triggerDefinitions.Count > 1)
                                        {
                                            triggerDefinitions[0].Description = ATTRIB + (triggerDefinitions[0].Description ?? "");

                                            foreach (var trigger in triggerDefinitions)
                                            {
                                                trigger.Functions.RemoveAll(x => string.Equals(x.ToString(), "CustomScriptCode(\"\")", StringComparison.InvariantCultureIgnoreCase));
                                                foreach (var function in trigger.Functions)
                                                {
                                                    if (function.Name == "SetVariable" && function.Parameters[1].Type == TriggerFunctionParameterType.String)
                                                    {
                                                        var stringValue = function.Parameters[1].Value;
                                                        if (Regex_ScriptFourCC().IsMatch(stringValue) && !string.Equals(stringValue, "true", StringComparison.InvariantCultureIgnoreCase))
                                                        {
                                                            var correctedText = $"set udg_{function.Parameters[0].Value}{(function.Parameters[0].ArrayIndexer != null ? $"[{function.Parameters[0].ArrayIndexer.Value}]" : "")} = '{stringValue}'";
                                                            function.Type = TriggerFunctionType.Action;
                                                            function.Name = "CustomScriptCode";
                                                            function.Parameters.Clear();
                                                            function.Parameters.Add(new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = correctedText });
                                                        }
                                                    }
                                                }
                                            }

                                            _logEvent("map triggers recovered");
                                            result.Triggers = triggers;
                                        }
                                        break;
                                    }
                                }
                                catch { }
                            }

                            if (result.Triggers != null)
                            {
                                break;
                            }
                        }
                    }

                    if (result.Units == null)
                    {
                        _logEvent("Decompiling map units");
                        foreach (var enumValue in Enum.GetValues(typeof(MapWidgetsFormatVersion)).Cast<MapWidgetsFormatVersion>().OrderBy(x => x == map?.Doodads?.FormatVersion ? 0 : 1).ThenByDescending(x => x))
                        {
                            foreach (var subEnumValue in Enum.GetValues(typeof(MapWidgetsSubVersion)).Cast<MapWidgetsSubVersion>().OrderBy(x => x == map?.Doodads?.SubVersion ? 0 : 1).ThenByDescending(x => x))
                            {
                                MapUnits units;
                                try
                                {
                                    if (jassScriptDecompiler.TryDecompileMapUnits(enumValue, subEnumValue, useNewFormat, out units, out var unitsDecompiledFromVariableName) && units?.Units?.Any() == true)
                                    {

                                        result.Units = units;
                                        result.UnitsDecompiledFromVariableName = unitsDecompiledFromVariableName;

                                        _logEvent("map units recovered");
                                        if (!result.Units.Units.Any(x => x.TypeId != "sloc".FromRawcode()))
                                        {
                                            _deprotectionResult.WarningMessages.Add("WARNING: Only unit start locations could be recovered. Map will still open in WorldEditor & run, but units will not be visible in WorldEditor rendering and saving in world editor will corrupt your war3map.j or war3map.lua script file.");
                                        }

                                        break;
                                    }
                                }
                                catch { }
                            }

                            if (result.Units != null)
                            {
                                break;
                            }
                        }
                    }
                }

                return result;
            };


            /*
                Algorithm Note: decompiler looks for patterns of specific function names. Obfuscation may have moved the code around, but call stack must start from main or config function or game would break.
                Do DepthFirstSearch of all recursive lines of code executing from config/main (ignoring undefined functions assuming they're blizzard natives) & copy/paste them at the bottom of each native editor function
                It doesn't matter that code would crash, because we only need decompiler to find patterns and generate editor-specific UI files.
                Don't save this as new war3map script file, because editor will re-create any editor native functions from editor-specific UI files, however rename native editor functions to _old in visual trigger file to avoid conflict when saving.
                Could delete instead of _old, since _old won't execute, but it's useful if programmer needs it as reference for bug fixes.
            */

            var jassParsed = new IndexedJassCompilationUnitSyntax(ParseJassScript(jassScript));
            var statements = new List<IStatementSyntax>();
            statements.AddRange(ExtractStatements_IncludingEnteringFunctionCalls(jassParsed, "config", out var configInlinedFunctions));
            statements.AddRange(ExtractStatements_IncludingEnteringFunctionCalls(jassParsed, "main", out var mainInlinedFunctions));
            var newBody = statements.ToImmutableArray();

            var inlined = new IndexedJassCompilationUnitSyntax(InlineJassFunctions(jassParsed.CompilationUnit, new HashSet<string>(configInlinedFunctions.Concat(mainInlinedFunctions))));

            var renamed = new IndexedJassCompilationUnitSyntax(RenameJassFunctions(inlined.CompilationUnit, _nativeEditorFunctions.ToDictionary(x => x, x =>
            {
                var newName = x;
                while (true)
                {
                    newName = $"{newName}_old";
                    if (!inlined.IndexedFunctions.ContainsKey(newName))
                    {
                        return newName;
                    }
                }
            })));

            var newDeclarations = renamed.CompilationUnit.Declarations.ToList();
            foreach (var nativeEditorFunction in _nativeEditorFunctions)
            {
                newDeclarations.Add(new JassFunctionDeclarationSyntax(new JassFunctionDeclaratorSyntax(new JassIdentifierNameSyntax(nativeEditorFunction), JassParameterListSyntax.Empty, JassTypeSyntax.Nothing), new JassStatementListSyntax(newBody)));
            }

            var editorSpecificCompilationUnit = new JassCompilationUnitSyntax(newDeclarations.ToImmutableArray());
            var editorSpecificJassScript = editorSpecificCompilationUnit.RenderScriptAsString();

            var firstPass = DecompileJassScriptMetaData_Internal(editorSpecificJassScript);
            if (firstPass?.UnitsDecompiledFromVariableName == null)
            {
                return firstPass;
            }

            var correctedUnitVariableNames = firstPass.UnitsDecompiledFromVariableName.Where(x => x.Value.StartsWith("gg_")).Select(x => new KeyValuePair<string, JassIdentifierNameSyntax>(x.Value, new JassIdentifierNameSyntax(x.Key.GetVariableName()))).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Last().Value);
            if (!correctedUnitVariableNames.Any())
            {
                return firstPass;
            }

            var renamer = new JassRenamer(new Dictionary<string, JassIdentifierNameSyntax>(), correctedUnitVariableNames);
            if (!renamer.TryRenameCompilationUnit(editorSpecificCompilationUnit, out var secondPass))
            {
                return firstPass;
            }

            _logEvent("Global generated variables renamed.");
            _logEvent("Starting decompile war3map script 2nd pass.");

            var result = DecompileJassScriptMetaData_Internal(secondPass.RenderScriptAsString());
            if (result != null)
            {
                result.UnitsDecompiledFromVariableName = null;
            }
            return result;
        }

        protected ScriptMetaData DecompileLuaScriptMetaData(string luaScript)
        {
            //todo: code this!
            return new ScriptMetaData();
        }

        protected Map DecompileMap(Action<Map> forcedValueOverrides = null)
        {
            //note: the order of operations matters. For example, script file import fails if info file not yet imported. So we import each file multiple times looking for changes
            _logEvent("Analyzing map files");
            var mapFiles = Directory.GetFiles(DiscoveredFilesPath, "war3map*", SearchOption.AllDirectories).Union(Directory.GetFiles(DiscoveredFilesPath, "war3campaign*", SearchOption.AllDirectories)).OrderBy(x => string.Equals(x, "war3map.w3i", StringComparison.InvariantCultureIgnoreCase) ? 0 : 1).ToList();

            var map = new Map();
            for (var retry = 0; retry < 2; ++retry)
            {
                if (forcedValueOverrides != null)
                {
                    forcedValueOverrides(map);
                }

                foreach (var mapFile in mapFiles)
                {
                    try
                    {
                        using (var stream = new FileStream(mapFile, FileMode.Open))
                        {
                            if (stream.Length != 0)
                            {
                                _logEvent($"Analyzing {mapFile} ...");
                                map.SetFile(Path.GetFileName(mapFile), false, stream);
                            }
                        }
                    }
                    catch { }
                }
            }

            _logEvent("Done analyzing map files");

            return map;
        }

        protected string RenderJassAST(object jassAST)
        {
            using (var writer = new StringWriter())
            {
                var renderer = new JassRenderer(writer);
                renderer.GetType().GetMethod("Render", new[] { jassAST.GetType() }).Invoke(renderer, new[] { jassAST });
                return writer.GetStringBuilder().ToString();
            }
        }

        protected string RenderLuaAST(LuaAST luaAST)
        {
            return RenderLuaASTNodes(luaAST.Body, "\r", 0).ToString();
        }

        protected string RenderLuaASTNodes(IEnumerable<LuaASTNode> nodes, string separator, int indentationLevel)
        {
            var result = new StringBuilder();
            if (nodes == null || !nodes.Any())
            {
                return result.ToString();
            }

            foreach (var field in nodes)
            {
                var node = RenderLuaASTNode(field, indentationLevel);
                if (!string.IsNullOrWhiteSpace(node))
                {
                    result.Append(node);
                    result.Append(separator);
                }
            }

            result.Remove(result.Length - separator.Length, separator.Length);
            return result.ToString();
        }

        protected string RenderLuaASTNode(LuaASTNode luaAST, int indentationLevel)
        {
            if (luaAST == null)
            {
                return "";
            }

            var indentation = new string(' ', indentationLevel * 2);
            switch (luaAST.Type)
            {
                case LuaASTType.AssignmentStatement:
                    return $"{indentation}{RenderLuaASTNodes(luaAST.Variables, ", ", indentationLevel)} = {RenderLuaASTNodes(luaAST.Init, ", ", indentationLevel)}";

                case LuaASTType.BinaryExpression:
                    return $"({RenderLuaASTNode(luaAST.Left, indentationLevel)} {luaAST.Operator} {RenderLuaASTNode(luaAST.Right, indentationLevel)})";

                case LuaASTType.BooleanLiteral:
                    return luaAST.Raw;

                case LuaASTType.BreakStatement:
                    return $"{indentation}break";

                case LuaASTType.CallExpression:
                    return $"{RenderLuaASTNode(luaAST.Base, indentationLevel)}({RenderLuaASTNodes(luaAST.Arguments, ", ", indentationLevel)})";

                case LuaASTType.CallStatement:
                    return $"{indentation}{RenderLuaASTNode(luaAST.Expression, indentationLevel)}";

                case LuaASTType.Comment:
                    return $"{indentation}{luaAST.Raw}";

                case LuaASTType.DoStatement:
                    return $"{indentation}do\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}\r{indentation}end";

                case LuaASTType.ElseClause:
                    return $"else\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}";

                case LuaASTType.ElseifClause:
                    return $"elseif {RenderLuaASTNode(luaAST.Condition, indentationLevel)} then\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}";

                case LuaASTType.ForGenericStatement:
                    return $"{indentation}for {RenderLuaASTNodes(luaAST.Variables, ", ", indentationLevel)} in {RenderLuaASTNodes(luaAST.Iterators, ", ", indentationLevel)} do\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}\r{indentation}end";

                case LuaASTType.ForNumericStatement:
                    return $"{indentation}for {RenderLuaASTNode(luaAST.Variable, indentationLevel)} = {RenderLuaASTNode(luaAST.Start, indentationLevel)},{RenderLuaASTNode(luaAST.End, indentationLevel)},{RenderLuaASTNode(luaAST.Step, indentationLevel)} do\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}\r{indentation}end";

                case LuaASTType.FunctionDeclaration:
                    return $"{indentation}{(luaAST.IsLocal ? "local " : "")}function {luaAST.Identifier?.Name ?? ""}({RenderLuaASTNodes(luaAST.Parameters, ", ", indentationLevel)})\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}\r{indentation}end";

                case LuaASTType.GotoStatement:
                    return $"{indentation}goto {RenderLuaASTNode(luaAST.Label, indentationLevel)}";

                case LuaASTType.Identifier:
                    return luaAST.Name;

                case LuaASTType.IfClause:
                    return $"if {RenderLuaASTNode(luaAST.Condition, indentationLevel)} then\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}";

                case LuaASTType.IfStatement:
                    return $"{indentation}{RenderLuaASTNodes(luaAST.Clauses, "\r", indentationLevel)}\r{indentation}end";

                case LuaASTType.IndexExpression:
                    return $"{RenderLuaASTNode(luaAST.Base, indentationLevel)}[{RenderLuaASTNode(luaAST.Index, indentationLevel)}]";

                case LuaASTType.LabelStatement:
                    return $"{indentation}::{RenderLuaASTNode(luaAST.Label, indentationLevel)}::";

                case LuaASTType.LocalStatement:
                    return $"{indentation}local {RenderLuaASTNodes(luaAST.Variables, ", ", indentationLevel)} = {RenderLuaASTNodes(luaAST.Init, ", ", indentationLevel)}";

                case LuaASTType.LogicalExpression:
                    return $"{RenderLuaASTNode(luaAST.Left, indentationLevel)} {luaAST.Operator} {RenderLuaASTNode(luaAST.Right, indentationLevel)}";

                case LuaASTType.MemberExpression:
                    return $"{RenderLuaASTNode(luaAST.Base, indentationLevel)}{luaAST.Indexer}{RenderLuaASTNode(luaAST.Identifier, indentationLevel)}";

                case LuaASTType.NilLiteral:
                    return luaAST.Raw;

                case LuaASTType.NumericLiteral:
                    return luaAST.Raw;

                case LuaASTType.RepeatStatement:
                    return $"{indentation}repeat\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}\r{indentation}until {RenderLuaASTNode(luaAST.Condition, indentationLevel)}";

                case LuaASTType.ReturnStatement:
                    return $"{indentation}return {RenderLuaASTNodes(luaAST.Arguments, ", ", indentationLevel)}";

                case LuaASTType.StringCallExpression:
                    return $"{RenderLuaASTNode(luaAST.Base, indentationLevel)}{RenderLuaASTNode(luaAST.Argument, indentationLevel)}";

                case LuaASTType.StringLiteral:
                    return luaAST.Raw;

                case LuaASTType.TableCallExpression:
                    return $"{RenderLuaASTNode(luaAST.Base, indentationLevel)}{RenderLuaASTNode(luaAST.Argument, indentationLevel)}";

                case LuaASTType.TableConstructorExpression:
                    return $"{{ {RenderLuaASTNodes(luaAST.Fields, ", ", indentationLevel)} }}";

                case LuaASTType.TableKey:
                    return $"[{RenderLuaASTNode(luaAST.Key, indentationLevel)}] = {RenderLuaASTNode(luaAST.TableValue, indentationLevel)}";

                case LuaASTType.TableKeyString:
                    return $"{RenderLuaASTNode(luaAST.Key, indentationLevel)} = {RenderLuaASTNode(luaAST.TableValue, indentationLevel)}";

                case LuaASTType.TableValue:
                    return RenderLuaASTNode(luaAST.TableValue, indentationLevel);

                case LuaASTType.UnaryExpression:
                    return $"{luaAST.Operator}({RenderLuaASTNode(luaAST.Argument, indentationLevel)})";

                case LuaASTType.VarargLiteral:
                    return luaAST.Raw;

                case LuaASTType.WhileStatement:
                    return $"{indentation}while {RenderLuaASTNode(luaAST.Condition, indentationLevel)} do\r{RenderLuaASTNodes(luaAST.Body, "\r", indentationLevel + 1)}\r{indentation}end";
            }

            throw new NotImplementedException();
        }

        protected JassCompilationUnitSyntax InlineJassFunctions(JassCompilationUnitSyntax compilationUnit, HashSet<string> functionNamesToInclude = null)
        {
            _logEvent("Inlining functions...");
            var inlineCount = 0;
            var oldInlineCount = inlineCount;
            var functions = compilationUnit.Declarations.Where(x => x is JassFunctionDeclarationSyntax).Cast<JassFunctionDeclarationSyntax>().GroupBy(x => x.FunctionDeclarator.IdentifierName.Name).ToDictionary(x => x.Key, x => x.First());
            var newDeclarations = compilationUnit.Declarations.ToList();
            do
            {
                oldInlineCount = inlineCount;
                foreach (var toInline in functions)
                {
                    var functionName = toInline.Key;
                    var function = toInline.Value;

                    if (functionNamesToInclude != null && !functionNamesToInclude.Contains(functionName))
                    {
                        continue;
                    }

                    if (function.FunctionDeclarator.ParameterList.Parameters.Any())
                    {
                        //todo: still allow if parameters are not used
                        continue;
                    }

                    if (function.Body.Statements.Any(x => x is JassLocalVariableDeclarationStatementSyntax))
                    {
                        //todo: still allow if local variables are not used
                        continue;
                    }

                    var body = function.Body;
                    var regex = new Regex(functionName, RegexOptions.Compiled);
                    var singleExecution = true;
                    JassFunctionDeclarationSyntax executionParent = null;
                    foreach (var executionCheck in functions.Where(x => x.Key != functionName))
                    {
                        var bodyAsString = RenderJassAST(new JassStatementListSyntax(executionCheck.Value.Body.Statements.ToImmutableArray()));
                        var matches = regex.Matches(bodyAsString); // regex on body is easiest, but imperfect due to strings & comments. however failing to inline isn't critical
                        if (matches.Count >= 1)
                        {
                            if (executionParent != null || matches.Count > 1)
                            {
                                singleExecution = false;
                                executionParent = null;
                                break;
                            }
                            executionParent = executionCheck.Value;
                        }
                    }

                    if (singleExecution && executionParent != null)
                    {
                        var newBody = new List<IStatementSyntax>();

                        var inlined = false;
                        for (var i = 0; i < executionParent.Body.Statements.Length; i++)
                        {
                            var statement = executionParent.Body.Statements[i];
                            if (statement is JassCallStatementSyntax callStatement && callStatement.IdentifierName.Name == functionName)
                            {
                                newBody.AddRange(body.Statements);
                                inlined = true;
                            }
                            else
                            {
                                newBody.Add(statement);
                            }
                        }

                        if (inlined)
                        {
                            functions.Remove(functionName);
                            newDeclarations.Remove(function);

                            var index = newDeclarations.IndexOf(executionParent);
                            newDeclarations.RemoveAt(index);
                            var newExecutionParent = new JassFunctionDeclarationSyntax(executionParent.FunctionDeclarator, new JassStatementListSyntax(newBody.ToImmutableArray()));
                            newDeclarations.Insert(index, newExecutionParent);

                            functions[executionParent.FunctionDeclarator.IdentifierName.Name] = newExecutionParent;

                            inlineCount++;
                            _logEvent($"Inlining function {functionName}...");
                        }
                    }
                }
            } while (oldInlineCount != inlineCount);

            _logEvent($"Inlined {inlineCount} functions");

            return new JassCompilationUnitSyntax(newDeclarations.ToImmutableArray());
        }

        protected string InlineLuaFunctions(string luaScript)
        {
            _logEvent("Inlining functions...");
            var inlineCount = 0;
            var oldInlineCount = inlineCount;
            var result = luaScript;

            var parsed = ParseLuaScript(luaScript);
            var functions = parsed.Body.Where(x => x.Type == LuaASTType.FunctionDeclaration).ToDictionary(x => x.Identifier.Name, x => x); //Note: only top-level functions, not functions defined inside another function
            var flattened = parsed.Body.DFS_Flatten(x => x.AllNodes).ToList();
            var functionCallExpressionLargestArgumentCount = flattened.Where(x => x.Type == LuaASTType.CallExpression && x.Base.Name != null).GroupBy(x => x.Base.Name).ToDictionary(x => x.Key, x => x.Max(y => y.Arguments.Length));
            var noParameterOrLocalVariableFunctions = functions.Where(x => functionCallExpressionLargestArgumentCount.TryGetValue(x.Key, out var parameterCount) && parameterCount == 0 && x.Value.Body.DFS_Flatten(x => x.AllNodes).Where(x => x.Type == LuaASTType.LocalStatement).Count() == 0).Select(x => x.Key).ToList();
            //todo: still allow inline if local variable is not used or parameters are not used or global function defined inside another function
            var newGlobalBody = parsed.Body.ToList();
            do
            {
                oldInlineCount = inlineCount;
                foreach (var functionName in noParameterOrLocalVariableFunctions)
                {
                    if (!functions.TryGetValue(functionName, out var function))
                    {
                        continue;
                    }

                    var body = function.Body;
                    var singleExecution = true;
                    LuaASTNode executionParent = null;
                    foreach (var executionCheck in functions.Where(x => x.Key != functionName))
                    {
                        var executionCount = executionCheck.Value.DFS_Flatten(x => x.AllNodes).Count(x => x.Type == LuaASTType.CallExpression && x.Base.Name == functionName);
                        if (executionCount >= 1)
                        {
                            if (executionParent != null || executionCount > 1)
                            {
                                singleExecution = false;
                                executionParent = null;
                                break;
                            }
                            executionParent = executionCheck.Value;
                        }
                    }

                    if (singleExecution && executionParent != null)
                    {
                        var newBody = new List<LuaASTNode>();

                        var inlined = false;
                        for (var i = 0; i < executionParent.Body.Length; i++)
                        {
                            var callStatement = executionParent.Body[i];
                            if (callStatement.Type == LuaASTType.CallStatement && callStatement.Expression.Base.Name == functionName)
                            {
                                newBody.AddRange(body);
                                inlined = true;
                            }
                            else
                            {
                                newBody.Add(callStatement);
                            }
                        }

                        if (inlined)
                        {
                            functions.Remove(functionName);
                            newGlobalBody.Remove(function);

                            executionParent.Body = newBody.ToArray();

                            inlineCount++;
                            _logEvent($"Inlining function {functionName}...");
                        }
                    }
                }
            } while (oldInlineCount != inlineCount);

            _logEvent($"Inlined {inlineCount} functions");

            parsed.Body = newGlobalBody.ToArray();

            return RenderLuaAST(parsed);
        }

        protected LuaAST ParseLuaScript(string luaScript)
        {
            using (var v8 = new V8ScriptEngine())
            {
                v8.Execute(File.ReadAllText(Path.Combine(ExeFolderPath, "luaparse.js")));
                v8.Script.luaScript = luaScript;
                v8.Execute("ast = JSON.stringify(luaparse.parse(luaScript, { luaVersion: '5.3' }));");
                return JsonConvert.DeserializeObject<LuaAST>((string)v8.Script.ast, new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Ignore, MaxDepth = Int32.MaxValue });
            }
        }

        protected JassCompilationUnitSyntax RenameJassFunctions(JassCompilationUnitSyntax jassScript, Dictionary<string, string> oldToNewFunctionNames)
        {
            if (oldToNewFunctionNames.Count == 0)
            {
                return jassScript;
            }

            var renamer = new JassRenamer(oldToNewFunctionNames.ToDictionary(x => x.Key, x => new JassIdentifierNameSyntax(x.Value)), new Dictionary<string, JassIdentifierNameSyntax>());
            if (!renamer.TryRenameCompilationUnit(jassScript, out var renamed))
            {
                renamed = jassScript;
            }
            return renamed;
        }

        protected string RenameLuaFunctions(string luaScript, Dictionary<string, string> oldToNewFunctionNames)
        {
            if (oldToNewFunctionNames.Count == 0)
            {
                return luaScript;
            }

            //todo: Code this!
            return luaScript;
        }

        [GeneratedRegex(@"\s+endglobals\s+", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_JassScriptEndGlobals();

        [GeneratedRegex(@"\s+function\s+main\s+takes\s+nothing\s+returns\s+nothing\s+((local\s+|//).*\s*)*", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_JassScriptFunctionMain();

        protected string DeObfuscateJassScript(string jassScript)
        {
            SplitUserDefinedAndGlobalGeneratedGlobalVariableNames(jassScript, out var userDefinedGlobals, out var globalGenerateds);

            var deObfuscated = DeObfuscateFourCCJass(jassScript);

            var parsed = ParseJassScript(deObfuscated);
            var formatted = "";
            using (var writer = new StringWriter())
            {
                var globalVariableRenames = new Dictionary<string, JassIdentifierNameSyntax>();
                var uniqueNames = new HashSet<string>(parsed.Declarations.Where(x => x is JassGlobalDeclarationListSyntax).Cast<JassGlobalDeclarationListSyntax>().SelectMany(x => x.Globals).Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>().Select(x => x.Declarator.IdentifierName.Name), StringComparer.InvariantCultureIgnoreCase);
                foreach (var declaration in parsed.Declarations)
                {
                    if (declaration is JassGlobalDeclarationListSyntax)
                    {
                        var globalDeclaration = (JassGlobalDeclarationListSyntax)declaration;
                        foreach (var global in globalDeclaration.Globals.Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>())
                        {
                            var isArray = global.Declarator is JassArrayDeclaratorSyntax;
                            var originalName = global.Declarator.IdentifierName.Name;

                            var typeName = global.Declarator.Type.TypeName.Name;
                            var baseName = originalName;

                            var isGlobalGenerated = globalGenerateds.Contains(baseName);

                            if (baseName.StartsWith("udg_", StringComparison.InvariantCultureIgnoreCase))
                            {
                                baseName = baseName.Substring(4);
                            }
                            else if (baseName.StartsWith("gg_", StringComparison.InvariantCultureIgnoreCase))
                            {
                                baseName = baseName.Substring(3);
                            }

                            var shortTypes = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) { { "rect", "rct" }, { "sound", "snd" }, { "trigger", "trg" }, { "unit", "unit" }, { "destructable", "dest" }, { "camerasetup", "cam" }, { "item", "item" }, { "integer", "int" }, { "boolean", "bool" } };
                            var shortTypeName = typeName;
                            if (shortTypes.ContainsKey(typeName))
                            {
                                shortTypeName = shortTypes[typeName];
                            }
                            var newName = isGlobalGenerated ? "gg_" : "udg_";
                            if (!baseName.StartsWith(typeName, StringComparison.InvariantCultureIgnoreCase) && !baseName.StartsWith(shortTypeName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                newName = $"{newName}{shortTypeName}_";
                            }
                            if (isArray && !baseName.Contains("array", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newName = $"{newName}array_";
                            }
                            newName = $"{newName}{baseName}";


                            var counter = 1;
                            while (uniqueNames.Contains($"{newName}_{counter}"))
                            {
                                counter++;
                            }
                            var uniqueName = $"{newName}_{counter}";
                            uniqueNames.Add(uniqueName);
                            globalVariableRenames[originalName] = new JassIdentifierNameSyntax(uniqueName);
                        }
                    }
                }

                foreach (var uniqueName in uniqueNames.Where(x => x.EndsWith("_1") && !uniqueNames.Contains(x.Substring(0, x.Length - 2) + "_2")))
                {
                    var oldRename = globalVariableRenames.FirstOrDefault(x => string.Equals(x.Value.Name, uniqueName, StringComparison.InvariantCultureIgnoreCase));
                    if (oldRename.Key != null)
                    {
                        globalVariableRenames[oldRename.Key] = new JassIdentifierNameSyntax(oldRename.Value.Name.Substring(0, uniqueName.Length - 2));
                    }
                }

                var renamer = new JassRenamer(new Dictionary<string, JassIdentifierNameSyntax>(), globalVariableRenames);
                if (!renamer.TryRenameCompilationUnit(parsed, out var deobfuscated))
                {
                    deobfuscated = parsed;
                }

                var renderer = new JassRenderer(writer);
                renderer.Render(deobfuscated);
                var stringBuilder = writer.GetStringBuilder();
                var match = Regex_JassScriptEndGlobals().Match(stringBuilder.ToString());
                if (match.Success)
                {
                    var beforeByteCorrections = "13,10,116,114,105,103,103,101,114,32,103,103,95,116,114,103,95,119,97,114,51,109,97,112,32,61,32,110,117,108,108,13,10".Split(',').ToList();
                    for (var i = 0; i < beforeByteCorrections.Count; i++)
                    {
                        var correction = beforeByteCorrections[i];
                        stringBuilder.Insert(match.Index + i, (char)byte.Parse(correction));
                    }
                }

                match = Regex_JassScriptFunctionMain().Match(stringBuilder.ToString());
                if (match.Success)
                {
                    var beforeByteCorrections = "13,10,102,117,110,99,116,105,111,110,32,84,114,105,103,95,119,97,114,51,109,97,112,95,65,99,116,105,111,110,115,32,116,97,107,101,115,32,110,111,116,104,105,110,103,32,114,101,116,117,114,110,115,32,110,111,116,104,105,110,103,13,10,13,10,99,97,108,108,32,80,108,97,121,83,111,117,110,100,66,74,40,67,114,101,97,116,101,83,111,117,110,100,40,34,119,34,43,34,97,34,43,34,114,34,43,34,51,34,43,34,109,34,43,34,97,34,43,34,112,34,43,34,46,34,43,34,119,34,43,34,97,34,43,34,118,34,44,102,97,108,115,101,44,102,97,108,115,101,44,102,97,108,115,101,44,48,44,48,44,34,34,41,41,13,10,13,10,101,110,100,102,117,110,99,116,105,111,110,13,10".Split(',').ToList();
                    for (var i = 0; i < beforeByteCorrections.Count; i++)
                    {
                        var correction = beforeByteCorrections[i];
                        stringBuilder.Insert(match.Index + i, (char)byte.Parse(correction));
                    }

                    var afterByteCorrections = "115,101,116,32,103,103,95,116,114,103,95,119,97,114,51,109,97,112,32,61,32,67,114,101,97,116,101,84,114,105,103,103,101,114,40,41,13,10,99,97,108,108,32,84,114,105,103,103,101,114,65,100,100,65,99,116,105,111,110,40,103,103,95,116,114,103,95,119,97,114,51,109,97,112,44,32,102,117,110,99,116,105,111,110,32,84,114,105,103,95,119,97,114,51,109,97,112,95,65,99,116,105,111,110,115,41,13,10,99,97,108,108,32,67,111,110,100,105,116,105,111,110,97,108,84,114,105,103,103,101,114,69,120,101,99,117,116,101,40,103,103,95,116,114,103,95,119,97,114,51,109,97,112,41,13,10".Split(',').ToList();
                    for (var i = 0; i < afterByteCorrections.Count; i++)
                    {
                        var correction = afterByteCorrections[i];
                        stringBuilder.Insert(match.Index + match.Length + beforeByteCorrections.Count + i, (char)byte.Parse(correction));
                    }

                }

                formatted = stringBuilder.ToString();
            }

            return formatted;
        }

        protected string DeObfuscateLuaScript(string luaScript)
        {
            //todo: Code this!
            return luaScript;
        }

        protected void CreateLuaCustomTextVisualTriggerFile(string luaScript)
        {
            //todo: Code this!
            //delete config/main & all editor-specific functions, because they will be re-generated on save (this is the visual trigger so they can still). Chance saving will corrupt map.

            CreateCustomTextVisualTriggerFile(luaScript);
        }

        protected void CreateJassCustomTextVisualTriggerFile(string jassScript)
        {
            //todo: delete config. rename main to main2 (keep incrementing til unique), make trigger which runs on startup to call main2
            //need main2 to execute any custom code, but can't have any editor-generated functions because duplicates will be re-generated on save [assuming war3mapunits.doo/etc were generated].
            //todo: add udg_ global variables to wtg (skip gg_ because they will be re-generated)
            /*
            //rename these editor functions to _Old
            config
            InitAllyPriorities
            InitCustomTeams
            CreateAllUnits
            InitCustomPlayerSlots
            CreateRegions
            CreateCameras
            InitSounds
            CreateNeutralPassive
            CreateNeutralPassiveBuildings
            CreatePlayerBuildings
            CreatePlayerUnits
            main
            InitGlobals
            InitCustomTriggers
            RunInitializationTriggers
            */

            var customText = jassScript;
            /*
            //unfinished
            var parsed = JassSyntaxFactory.ParseCompilationUnit(jassScript);

            foreach (var declaration in parsed.Declarations)
            {
                if (declaration is JassGlobalDeclarationListSyntax)
                {
                    var globalDeclaration = (JassGlobalDeclarationListSyntax)declaration;
                    foreach (var global in globalDeclaration.Where(x => x is JassGlobalDeclarationSyntax).Globals.Cast<JassGlobalDeclarationSyntax>())
                    {
                    }
                }
            }
            using (var writer = new StringWriter())
            {
                var renderer = new JassRenderer(writer);
                renderer.Render(parsed);
                customText = writer.GetStringBuilder().ToString();
            }
            */

            CreateCustomTextVisualTriggerFile(customText);
        }

        protected class GlobalVariable
        {
            public string vartype;
            public bool isarray;
            public string varname;
            public string initial;
            public bool initialized;
        }

        protected class WTGCategory
        {
            public int id;
            public string name;
            public int type;
        }

        protected class WTGTrigger
        {
            public string name;
            public string desc;
            public int type;
            public bool enabled;
            public bool custom;
            public bool initial;
            public bool runoninit;
            public int category;
            public int eca; // number of EventConditionAction
            public string data;
        }

        protected void CreateCustomTextVisualTriggerFile(string customText)
        {
            customText = customText.Replace("\n", "\r\n");

            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, "war3map.wtg"), FileMode.OpenOrCreate))
            using (var wtgFile = new BinaryWriter(stream))
            {
                _logEvent("Creating war3map.wtg...");
                wtgFile.Write(Encoding.ASCII.GetBytes("WTG!"));
                wtgFile.Write(7);
                wtgFile.Write(0);
                wtgFile.Write(2);
                wtgFile.Write(0);
                wtgFile.Write(0);
            }

            _logEvent("Creating war3map.wct...");
            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, "war3map.wct"), FileMode.OpenOrCreate))
            using (var wctFile = new BinaryWriter(stream))
            {
                wctFile.Write(ATTRIB.Length + 1);
                wctFile.Write(Encoding.ASCII.GetBytes(ATTRIB));
                wctFile.Write(Encoding.ASCII.GetBytes("\0"));
                wctFile.Write(customText.Length + 1);
                wctFile.Write(Encoding.ASCII.GetBytes(customText));
                wctFile.Write(Encoding.ASCII.GetBytes("\0"));
                wctFile.Write(0);
            }
        }

        protected void WriteWtg_PlainText_Jass(string jassScript)
        {
            var strings = new List<string>();

            //temporarily remove strings & later replace, to make regex's easier
            jassScript = Regex.Replace(jassScript, "(?!ExecuteFunc\\()\"(?:\\\\.|[^\"\\\\])*\"", match =>
            {
                strings.Add(match.Groups[0].Value);
                return $"###DEP_STRING_{strings.Count - 1}###";
            });

            // remove formatting to make regex's easier
            var lines = jassScript.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                lines[lineIdx] = lines[lineIdx].Trim();
                lines[lineIdx] = Regex.Replace(lines[lineIdx], @"\s+", " ");
                lines[lineIdx] = Regex.Replace(lines[lineIdx], @"(?<=\W)\s", "");
                lines[lineIdx] = Regex.Replace(lines[lineIdx], @"\s(?=\W)", "");
            }
            jassScript = string.Join("\n", lines);

            _logEvent("Renaming reserved functions...");
            int reservedFunctionReplacementCount = 0;
            Dictionary<string, string> reservedFunctionReplacements = new Dictionary<string, string>();
            foreach (string func in _nativeEditorFunctions)
            {
                if (func == "main")
                {
                    continue; // has special logic with regex later
                }

                if (Regex.IsMatch(jassScript, $@"function {func}\s"))
                {
                    string newname = $"{func}_old";
                    while (Regex.IsMatch(jassScript, $@"function {newname}\s"))
                    {
                        newname = $"{newname}_old";
                    }
                    _logEvent($"Renaming {func} to {newname}");
                    reservedFunctionReplacements[func] = newname;
                    reservedFunctionReplacementCount++;
                }
            }
            foreach (var replacement in reservedFunctionReplacements)
            {
                jassScript = Regex.Replace(jassScript, $@"\b(?<!('|\$)){replacement.Key}\b", replacement.Value);
            }
            _logEvent($"Renamed {reservedFunctionReplacementCount} reserved functions");

            var globalvars = new List<GlobalVariable>();
            var categories = new List<WTGCategory>();
            var triggers = new List<WTGTrigger>();
            foreach (var line in jassScript.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Trim() == "endglobals")
                {
                    break;
                }

                var match = Regex.Match(line.Trim(), @"^(\w+)( | array )(\w+)(?:|\=(.*))$");
                if (match.Success)
                {
                    globalvars.Add(new GlobalVariable
                    {
                        vartype = match.Groups[1].Value,
                        isarray = match.Groups[2].Value == " array ",
                        varname = match.Groups[3].Value,
                        initial = match.Groups[4].Value ?? "",
                        initialized = false
                    });
                }
            }
            var typecounts = new Dictionary<string, int>();
            var globalVarReplacements = new Dictionary<string, string>();
            foreach (var globalVariable in globalvars)
            {
                var varname = globalVariable.varname;
                var vartype = $"{globalVariable.vartype}{(globalVariable.isarray ? "s" : "")}";
                var newname = varname;
                if (!typecounts.ContainsKey(vartype))
                {
                    typecounts[vartype] = 0;
                }
                newname = $"{vartype}{(++typecounts[vartype]).ToString("D2")}";
                globalVariable.varname = newname;
                globalVarReplacements[varname] = $"udg_{newname}";
            }
            foreach (var replacement in globalVarReplacements)
            {
                jassScript = Regex.Replace(jassScript, $@"\b(?<!('|\$)){replacement.Key}\b", replacement.Value);
            }

            var visualScript = jassScript;
            var mainfunc = "main2";
            while (Regex.IsMatch(visualScript, $@"function {mainfunc}"))
            {
                mainfunc += "2";
            }
            visualScript = Regex.Replace(visualScript, @"^.*?\sendglobals", "", RegexOptions.Singleline);
            visualScript = Regex.Replace(visualScript, @"\sfunction InitCustomTeams takes nothing returns nothing.*?\sendfunction", "", RegexOptions.Singleline);
            visualScript = Regex.Replace(visualScript, @"\nfunction main takes nothing returns nothing", $"\nfunction {mainfunc} takes nothing returns nothing");
            visualScript = Regex.Replace(visualScript, @"\scall InitBlizzard\(\)", "");
            var initcode = "";
            foreach (var globalVariable in globalvars)
            {
                if (globalVariable.initial.ToString() != "" && !Regex.IsMatch(globalVariable.initial.ToString(), @"^(|""|false|0|null|Create(Timer|Group|Force)\(\))$"))
                {
                    if (Regex.IsMatch(globalVariable.vartype.ToString(), @"^(boolean|real|integer|string)$"))
                    {
                        globalVariable.initialized = true;
                        continue;
                    }
                    initcode += $"set udg_{globalVariable.varname} = {globalVariable.initial}\n";
                }
            }
            visualScript += @"
function InitTrig_init takes nothing returns nothing
" + initcode + @"
call ExecuteFunc(""" + mainfunc + @""")
endfunction
";
            categories.Add(new WTGCategory() { id = 1, name = "triggers", type = 0 });
            triggers.Add(new WTGTrigger()
            {
                name = "init",
                desc = "",
                type = 0,
                enabled = true,
                custom = true,
                initial = false,
                runoninit = false,
                category = 1,
                eca = 0,
                data = visualScript
            });

            var wctsections = new List<string>();

            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, "war3map.wtg"), FileMode.OpenOrCreate))
            using (var wtgFile = new BinaryWriter(stream))
            {
                _logEvent("Creating war3map.wtg...");
                wtgFile.Write(Encoding.ASCII.GetBytes("WTG!"));
                wtgFile.Write(7);
                wtgFile.Write(categories.Count);
                foreach (var category in categories)
                {
                    wtgFile.Write(category.id);
                    wtgFile.Write(Encoding.ASCII.GetBytes($"{category.name}\0"));
                    wtgFile.Write(category.type);
                }
                wtgFile.Write(2);
                wtgFile.Write(globalvars.Count);
                foreach (var globalvar in globalvars)
                {
                    wtgFile.Write(Encoding.ASCII.GetBytes($"{globalvar.varname}\0"));
                    wtgFile.Write(Encoding.ASCII.GetBytes($"{globalvar.vartype}\0"));
                    wtgFile.Write(1);
                    wtgFile.Write(globalvar.isarray ? 1 : 0);
                    wtgFile.Write(1);
                    wtgFile.Write(globalvar.initialized ? 1 : 0);
                    if (globalvar.initialized)
                    {
                        var initial = Regex.Replace(globalvar.initial, @"###DEP_STRING_(\d+)###", match =>
                        {
                            var index = int.Parse(match.Groups[1].Value);
                            return strings[index];
                        }).Trim('"');
                        wtgFile.Write(Encoding.ASCII.GetBytes(initial));
                    }
                    wtgFile.Write(Encoding.ASCII.GetBytes("\0"));
                }
                wtgFile.Write(triggers.Count);
                foreach (var trigger in triggers)
                {
                    wtgFile.Write(Encoding.ASCII.GetBytes($"{trigger.name}\0"));
                    wtgFile.Write(Encoding.ASCII.GetBytes($"{trigger.desc}\0"));
                    wtgFile.Write(trigger.type);
                    wtgFile.Write(trigger.enabled ? 1 : 0);
                    wtgFile.Write(trigger.custom ? 1 : 0);
                    wtgFile.Write(trigger.initial ? 1 : 0);
                    wtgFile.Write(trigger.runoninit ? 1 : 0);
                    wtgFile.Write(trigger.category);
                    wtgFile.Write(trigger.eca);
                    wctsections.Add($"{trigger.data}\0");
                }
            }

            _logEvent("Creating war3map.wct...");
            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, "war3map.wct"), FileMode.OpenOrCreate))
            using (var wctFile = new BinaryWriter(stream))
            {
                var toolAdvertisement = $"// {ATTRIB}";

                wctFile.Write(1);
                wctFile.Write(Encoding.ASCII.GetBytes("\0"));
                wctFile.Write(toolAdvertisement.Length + 1);
                wctFile.Write(Encoding.ASCII.GetBytes($"{toolAdvertisement}\0"));
                wctFile.Write(wctsections.Count);
                foreach (var section in wctsections)
                {
                    var formattedSection = Regex.Replace($"{toolAdvertisement}{section}", @"###DEP_STRING_(\d+)###", match =>
                    {
                        var index = int.Parse(match.Groups[1].Value);
                        return strings[index];
                    }).Replace("\n", "\r\n");
                    wctFile.Write(formattedSection.Length);
                    wctFile.Write(Encoding.ASCII.GetBytes(formattedSection));
                }
            }
        }

        protected void DeleteAttributeListSignatureFiles()
        {
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "(attributes)")))
            {
                _logEvent("Deleting (attributes)...");
                File.Delete(Path.Combine(DiscoveredFilesPath, "(attributes)"));
            }
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "(listfile)")))
            {
                _logEvent("Deleting (listfile)...");
                File.Delete(Path.Combine(DiscoveredFilesPath, "(listfile)"));
            }
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "(signature)")))
            {
                _logEvent("Deleting (signature)...");
                File.Delete(Path.Combine(DiscoveredFilesPath, "(signature)"));
            }
        }

        protected void PatchW3I()
        {
            var w3ipath = Path.Combine(DiscoveredFilesPath, "war3map.w3i");
            _logEvent("Patching war3map.w3i...");
            using (var w3i = File.Open(w3ipath, FileMode.Open, FileAccess.ReadWrite))
            {
                w3i.Seek(-1, SeekOrigin.End);
                var c = new byte[1];
                w3i.Read(c, 0, 1);
                if (c[0] == 0xFF)
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                    w3i.Seek(-1, SeekOrigin.Current);
                    w3i.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);
                    _logEvent("war3map.w3i patched successfully");
                }
                else
                {
                    _logEvent("war3map.w3i is undamaged or already patched; skipping");
                }
            }
        }

        [GeneratedRegex(@"^war3(map|campaign)(\.(w[a-zA-Z0-9]{2}|doo|shd|mmp|j|imp)|misc\.txt|skin\.txt|map\.blp|units\.doo|extra\.txt)$", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_NonImportedNativeEditorFileName();

        protected void BuildImportList()
        {
            _logEvent("Building war3map.imp...");
            var files = Directory.GetFiles(DiscoveredFilesPath, "*", SearchOption.AllDirectories).ToList();
            var newfiles = new List<string>();
            foreach (var file in files.Select(x => x.Replace($"{DiscoveredFilesPath}\\", "", StringComparison.InvariantCultureIgnoreCase)))
            {
                if (!Regex_NonImportedNativeEditorFileName().IsMatch(file))
                {
                    newfiles.Add($"{file}\x00");
                }
            }
            newfiles.Add("\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76\x00");
            newfiles = newfiles.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, "war3map.imp"), FileMode.OpenOrCreate))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(1);
                writer.Write(newfiles.Count);
                writer.Write('\r');
                foreach (var file in newfiles)
                {
                    writer.WriteString(file);
                    writer.Write('\r');
                }
            }

            _logEvent($"{newfiles.Count} files added to import list");
        }

        protected Dictionary<string, List<string>> ParseFilesToDetectPossibleFileNames(Map map, List<string> unknownFileExtensions)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);

            if (map == null)
            {
                return result;
            }

            if (map.ImportedFiles?.Files != null)
            {
                result["__decompiledMap.ImportedFiles__"] = map.ImportedFiles.Files.Select(x => x.FullPath).ToList();
            }

            if (!string.IsNullOrWhiteSpace(map.Info?.LoadingScreenPath))
            {
                result["__decompiledMap__"] = new List<string>() { map.Info.LoadingScreenPath };
            }

            // todo: Refactor this to not use NewtonSoft JSON for speed (also, if "Just My Code" is disabled while debugging, this practically freezes up)
            _logEvent("Extracting all object data from map files");
            var objectDataJson = map.GetAllObjectData_JSON();
            _logEvent("Parsing map object data into strings");
            var deserialized = (JObject)JsonConvert.DeserializeObject(objectDataJson);
            _logEvent("Searching map data strings for unknown files");
            var allDecompiledStrings = deserialized.DescendantsAndSelf().Where(x => x is JValue).Cast<JValue>().Where(x => x.Type == JTokenType.String).Select(x => (string)x.Value).ToList();
            result["__decompiledMap.ObjectData.Strings__"] = allDecompiledStrings;
            result["__decompiledMap.ObjectData.ParsedStrings__"] = allDecompiledStrings.Where(x => unknownFileExtensions.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase))).SelectMany(x => ScanTextForPotentialUnknownFileNames_SLOW(x, unknownFileExtensions)).ToList();

            var allExtractedFiles = Directory.GetFiles(ExtractedFilesPath, "*", SearchOption.AllDirectories).ToList();

            _logEvent("Searching inside files for unknown names ...");

            _logEvent("Searching SLK files");
            result["__SLK__"] = allExtractedFiles.Where(x => Path.GetExtension(x).Equals(".slk", StringComparison.InvariantCultureIgnoreCase)).SelectMany(x => File.ReadAllLines(x).SelectMany(y => ScanTextForPotentialUnknownFileNames_SLOW(y, unknownFileExtensions))).ToList();

            _logEvent("Searching TXT files");
            result["__TXT__"] = allExtractedFiles.Where(x => Path.GetExtension(x).Equals(".txt", StringComparison.InvariantCultureIgnoreCase)).SelectMany(x => File.ReadAllLines(x).SelectMany(y => ScanTextForPotentialUnknownFileNames_SLOW(y, unknownFileExtensions))).ToList();

            _logEvent("Searching TOC files");
            result["__TOC__"] = allExtractedFiles.Where(x => Path.GetExtension(x).Equals(".toc", StringComparison.InvariantCultureIgnoreCase)).SelectMany(x => File.ReadLines(x)).ToList();
            result["__IMP__"] = allExtractedFiles.Where(x => Path.GetExtension(x).Equals(".imp", StringComparison.InvariantCultureIgnoreCase)).SelectMany(x => File.ReadLines(x)).ToList();

            _logEvent("Searching MDL & MDX files");
            result.AddRange(ScanModelFilesForPossibleFileNames(allExtractedFiles));

            _logEvent("Searching INI files");
            result.AddRange(ScanINIFilesForPossibleFileNames(allExtractedFiles, unknownFileExtensions));

            _logEvent("Searching Script files");
            var textExtensionsToParseQuotedStrings = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".j", ".lua", ".txt", ".fdf" };
            var deepScanQuotedStrings = allExtractedFiles.Where(x => textExtensionsToParseQuotedStrings.Contains(Path.GetExtension(x))).SelectMany(x => ParseQuotedStringsFromCode(File.ReadAllText(x))).ToList();
            result["__DeepScan-AllQuotedStrings__"] = deepScanQuotedStrings.ToList();
            result["__DeepScan-ParsedQuotedStrings__"] = deepScanQuotedStrings.SelectMany(y => ScanTextForPotentialUnknownFileNames_SLOW(y, unknownFileExtensions)).ToList();

            _logEvent("Searching Text files");
            var textExtensionsToParseLines = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".txt", ".html" };
            var deepScanLines = allExtractedFiles.Where(x => textExtensionsToParseLines.Contains(Path.GetExtension(x))).SelectMany(x => File.ReadAllLines(x)).ToList();
            result["__DeepScan-ParsedTextLines__"] = deepScanLines.SelectMany(y => ScanTextForPotentialUnknownFileNames_SLOW(y, unknownFileExtensions)).ToList();

            _logEvent("Searching Binary files");
            //todo: run through several test maps & if there are any file extensions here which never produce unknown names, remove them
            var binaryExtensionsToParseReadableStrings = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".wai", ".w3a", ".w3b", ".w3c", ".w3d", ".w3e", ".w3f", ".w3h", ".w3i", ".w3q", ".w3r", ".w3s", ".w3t", ".w3u", ".wai", ".wct", ".wpm", ".wtg", ".wts", ".shd", ".mmp", ".doo", ".dll", ".exe" };
            var strings = allExtractedFiles.Where(x => string.IsNullOrWhiteSpace(Path.GetExtension(x)) || !_commonFileExtensions.Contains(Path.GetExtension(x)) || binaryExtensionsToParseReadableStrings.Contains(Path.GetExtension(x))).SelectMany(x => ScanBinaryFileForReadableAsciiStrings(x)).ToList();
            result["__ParsedStrings__"] = strings.Where(x => unknownFileExtensions.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase))).SelectMany(y => ScanTextForPotentialUnknownFileNames_SLOW(y, unknownFileExtensions)).ToList();

            //todo: [LowPriority] add real FDF parser? (none online, would need to build my own based on included fdf.g grammar file)

            _logEvent("Deep Searching Script files");
            var deepScanExtensions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".j", ".lua", ".txt" };
            result.AddRange(DeepScanFilesForPossibleFileNames_Regex(allExtractedFiles.Where(x => deepScanExtensions.Contains(Path.GetExtension(x))).ToList(), unknownFileExtensions));

            var multiExtensionRegex = new Regex("\\.("  + unknownFileExtensions.Select(x => x.Trim('.')).Aggregate((x,y) => x+"|"+y) + ")", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var multiExtensionStrings = result.Values.SelectMany(x => x).Where(x => multiExtensionRegex.Matches(x).Count > 1).ToList();
            result["__DeepScan-MultiExtensions__"] = deepScanLines.SelectMany(y => ScanTextForPotentialUnknownFileNames_SLOW(y, unknownFileExtensions)).ToList();

            return result;
        }

        protected List<string> ScanBinaryFileForReadableAsciiStrings(string fileName)
        {
            var bytes = File.ReadAllBytes(fileName);
            List<string> result = new List<string>();
            var stringBuilder = new StringBuilder();
            foreach (var value in bytes)
            {
                if (value >= 127 || (value >= '\0' && value < (byte)' '))
                {
                    if (stringBuilder.Length > 0)
                    {
                        result.Add(stringBuilder.ToString());
                        stringBuilder = new StringBuilder();
                    }
                }
                else
                {
                    stringBuilder.Append((char)value);
                }
            }

            return result;
        }

        protected List<string> ParseQuotedStringsFromCode(string text, char quoteCharacter = '"', string escapeSequence = "\\\"")
        {
            const string temporaryEscapeReplacement = "__WC3MAPDEPROTECTOR-ESCAPED-QUOTE__";
            var escapingRemoved = text;
            if (escapeSequence.Contains(quoteCharacter))
            {
                escapingRemoved = text.Replace(escapeSequence, temporaryEscapeReplacement);
            }

            var result = new List<string>();
            var matches = Regex.Matches(escapingRemoved, $"{quoteCharacter}[${quoteCharacter}]*{quoteCharacter}").Cast<Match>().ToList();
            foreach (var match in matches)
            {
                result.Add(match.Value.Replace(temporaryEscapeReplacement, quoteCharacter.ToString()));
            }

            return result;
        }

        protected List<string> ScanTextForPotentialUnknownFileNames_SLOW(string text, List<string> unknownFileExtensions)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { text };
            void AddPotentialStrings(List<string> strings)
            {
                result.AddRange(strings.Where(x => unknownFileExtensions.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase))).Select(x => x.Trim()));
            }

            /*
            var separators = (new char[] { '\'', ';', ',', '|', '=', '[', ']', '{', '}', '(', ')', ' ', '\t', '\r', '\n', '"' }).Concat(nonVisibleCharacters).Distinct().ToArray();
            */

            var nonVisibleSeparators = Enumerable.Range((int)'\0', (int)' ' - (int)'\0').Concat(Enumerable.Range(127, 256 - 127)).Select(x => (char)x).ToArray();
            var visibleSeparators = Enumerable.Range(0, 256).Select(x => (char)x).Where(x => !(x >= 'a' && x <= 'z') && !(x >= 'A' && x <= 'Z') && !(x >= '0' && x <= '9')).Except(nonVisibleSeparators).ToList();

            AddPotentialStrings(text.Split(nonVisibleSeparators, StringSplitOptions.RemoveEmptyEntries).ToList());

            int oldCount;
            do
            {
                oldCount = result.Count;
                foreach (var separator in visibleSeparators)
                {
                    //note: split function allows all chars simultaneously, but that would produce fewer potential combinations of strings to search for filenames
                    AddPotentialStrings(result.SelectMany(x => x.Split(separator, StringSplitOptions.RemoveEmptyEntries)).ToList());
                }
            } while (result.Count != oldCount);
            foreach (var entry in result.ToList())
            {
                result.AddRange(visibleSeparators.Select(x => entry.Trim(x).Trim()));
            }

            foreach (var entry in result.ToList())
            {
                string multipleExtensions = entry;
                var extension = Path.GetExtension(multipleExtensions);
                while (!string.IsNullOrWhiteSpace(extension))
                {
                    multipleExtensions = multipleExtensions.Substring(0, multipleExtensions.Length - extension.Length);
                    var nextExtension = Path.GetExtension(multipleExtensions);
                    if (!string.IsNullOrWhiteSpace(nextExtension))
                    {
                        result.Add(multipleExtensions);
                    }

                    extension = nextExtension;
                };
            }

            return result.ToList();
        }

        protected HashSet<string> _modelAndTextureFileExtensions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".mdx", ".mdl", ".tga", ".blp", ".jpg", ".bmp", ".dds" };

        protected List<string> ScanMDLForPossibleFileNames(string mdlFileName)
        {
            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            using (var stream = new StreamReader(File.OpenRead(mdlFileName)))
            {
                var lines = stream.ReadToEnd().Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                stream.BaseStream.Position = 0;
                var commentLines = lines.Where(x => x.Trim().StartsWith("//")).Select(x => x.Trim().TrimStart('/')).ToList();
                result.AddRange(commentLines.SelectMany(x => ScanTextForPotentialUnknownFileNames_SLOW(x, _modelAndTextureFileExtensions.ToList())));
                result.AddRange(lines.Where(x => _modelAndTextureFileExtensions.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase))).SelectMany(x => ScanTextForPotentialUnknownFileNames_SLOW(x, _modelAndTextureFileExtensions.ToList())));

                try
                {
                    var model = new MdxLib.Model.CModel();
                    var loader = new MdxLib.ModelFormats.CMdl();
                    loader.Load(mdlFileName, stream.BaseStream, model);
                    if (model.Textures != null)
                    {
                        var textures = model.Textures.Select(x => x.FileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        result.AddRange(textures);
                        result.AddRange(textures.SelectMany(x => _modelAndTextureFileExtensions.Select(ext => Path.ChangeExtension(x, ext))));
                        result.Add($"{model.Name}.mdl");
                    }
                }
                catch { }
            }

            result.AddRange(result.SelectMany(x => _modelAndTextureFileExtensions.Select(ext => Path.ChangeExtension(x, ext))).ToList());
            return result.ToList();
        }

        protected List<string> ScanMDXForPossibleFileNames_FastMDX(string mdxFileName)
        {
            //todo: fix bug when parsing Glow.mdx from ZombieVillager (crashes rather than just returning the data it found) [low priority since we probably delete this library and only use MDXLib]
            try
            {
                using (var stream = new StreamReader(File.OpenRead(mdxFileName)))
                {
                    var model = new MdxLib.Model.CModel();
                    var loader = new MdxLib.ModelFormats.CMdx();
                    loader.Load(mdxFileName, stream.BaseStream, model);
                    var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    if (model.Textures != null)
                    {
                        var textures = model.Textures.Select(x => x.FileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        result.AddRange(textures);
                        result.AddRange(textures.SelectMany(x => _modelAndTextureFileExtensions.Select(ext => Path.ChangeExtension(x, ext))));
                    }
                    result.AddRange(_modelAndTextureFileExtensions.Select(ext => $"{model.Name}{ext}"));
                    return result.ToList();
                }
            }
            catch { }

            _logEvent($"Error parsing file with FastMDX Library: {mdxFileName}");
            return new List<string>();
        }

        protected List<string> ScanMDXForPossibleFileNames_MDXLib(string mdxFileName)
        {
            //todo: fix bug when parsing Glow.mdx from ZombieVillager (crashes rather than just returning the data it found)
            try
            {
                var mdx = new MDX(mdxFileName);
                var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                if (mdx.Textures != null)
                {
                    var textures = mdx.Textures.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    result.AddRange(textures);
                    result.AddRange(textures.SelectMany(x => _modelAndTextureFileExtensions.Select(ext => Path.ChangeExtension(x, ext))));
                }
                result.AddRange(_modelAndTextureFileExtensions.Select(ext => $"{mdx.Info.Name}{ext}"));
                return result.ToList();
            }
            catch
            {
            }

            _logEvent($"Error parsing file with MDXLib Library: {mdxFileName}");
            return new List<string>();
        }

        [GeneratedRegex(@"^MDLXVERS.*?MODLt.*?([a-z0-9 _-]+)", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ScanMDX();
        protected List<string> ScanMDXForPossibleFileNames_Regex(string mdxFileName)
        {
            var line = File.ReadAllText(mdxFileName);
            var mdxMatch = Regex_ScanMDX().Match(line);
            if (mdxMatch.Success)
            {
                return _modelAndTextureFileExtensions.Select(ext => $"{mdxMatch.Groups[1].Value}{ext}").ToList();
            }

            return new List<string>();
        }

        protected List<string> ScanMDXForPossibleFileNames(string mdxFileName)
        {
            if (!string.Equals(Path.GetExtension(mdxFileName), ".mdx", StringComparison.InvariantCultureIgnoreCase))
            {
                return new List<string>();
            }

            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            result.AddRange(ScanMDXForPossibleFileNames_FastMDX(mdxFileName));
            result.AddRange(ScanMDXForPossibleFileNames_MDXLib(mdxFileName));
            result.AddRange(ScanMDXForPossibleFileNames_Regex(mdxFileName));
            var strings = ScanBinaryFileForReadableAsciiStrings(mdxFileName);
            result.AddRange(strings.Where(x => _modelAndTextureFileExtensions.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase))).SelectMany(x => ScanTextForPotentialUnknownFileNames_SLOW(x, _modelAndTextureFileExtensions.ToList())));
            result.AddRange(result.SelectMany(x => _modelAndTextureFileExtensions.Select(ext => Path.ChangeExtension(x, ext))).ToList());
            return result.ToList();
        }

        protected List<string> ScanINIForPossibleFileNames(string fileName, List<string> unknownFileExtensions)
        {
            var result = new List<string>();
            try
            {
                var parser = new FileIniDataParser(new IniParser.Parser.IniDataParser(new IniParserConfiguration() { SkipInvalidLines = true, AllowDuplicateKeys = true, AllowDuplicateSections = true, AllowKeysWithoutSection = true }));
                var ini = parser.ReadFile(fileName);
                foreach (var section in ini.Sections)
                {
                    foreach (var key in section.Keys)
                    {
                        result.Add(key.Value);
                        result.AddRange(ScanTextForPotentialUnknownFileNames_SLOW(key.Value, unknownFileExtensions));
                    }
                }
            }
            catch
            {
                DebugSettings.Warn("Fix this!");
            }

            return result;
        }

        protected ConcurrentDictionary<string, List<string>> ScanINIFilesForPossibleFileNames(List<string> fileNames, List<string> unknownFileExtensions)
        {
            var result = new ConcurrentDictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
            Parallel.ForEach(fileNames, fileName =>
            {
                if (!File.Exists(fileName))
                {
                    DebugSettings.Warn("Fix This!");
                    return;
                }

                if (string.Equals(Path.GetExtension(fileName), ".txt", StringComparison.InvariantCultureIgnoreCase))
                {
                    result["__INISCAN__" + fileName] = ScanINIForPossibleFileNames(fileName, unknownFileExtensions);
                }
            });

            return result;
        }

        protected ConcurrentDictionary<string, List<string>> ScanModelFilesForPossibleFileNames(List<string> fileNames)
        {
            var result = new ConcurrentDictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
            Parallel.ForEach(fileNames, fileName =>
            {
                if (!File.Exists(fileName))
                {
                    DebugSettings.Warn("Fix This!");
                    return;
                }

                if (string.Equals(Path.GetExtension(fileName), ".mdx", StringComparison.InvariantCultureIgnoreCase))
                {
                    result["__MDXSCAN__" + fileName] = ScanMDXForPossibleFileNames(fileName);
                }
                else if (string.Equals(Path.GetExtension(fileName), ".mdl", StringComparison.InvariantCultureIgnoreCase))
                {
                    result["__MDLSCAN__" + fileName] = ScanMDLForPossibleFileNames(fileName);
                }
            });

            return result;
        }

        protected ConcurrentDictionary<string, List<string>> DeepScanFilesForPossibleFileNames_Regex(List<string> fileNames, List<string> unknownFileExtensions)
        {
            var extensionsToSkip = new HashSet<string>(".mdx,.mdl,.mp3,.blp,.tga,.bmp,.jpg,.dds,.wav".Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.InvariantCultureIgnoreCase);            

            var result = new ConcurrentDictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
            Parallel.ForEach(fileNames, fileName =>
            {
                if (!File.Exists(fileName))
                {
                    DebugSettings.Warn("Fix This!");
                    return;
                }

                if (!DebugSettings.BenchmarkUnknownRecovery && extensionsToSkip.Contains(Path.GetExtension(fileName)))
                {
                    return;
                }

                _logEvent($"scanning {fileName}");
                var text = File.ReadAllText(fileName);

                var regexFoundFiles = new List<string>();
                var fileExtensions = unknownFileExtensions.Select(x => x.Trim('.')).Distinct(StringComparer.InvariantCultureIgnoreCase).Aggregate((x, y) => $"{x}|{y}");
                var matches = Regex.Matches(text, @"([ -~]{1,1000}?)\.(" + fileExtensions + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled).Concat(Regex.Matches(text, @"([ -~]{1,1000})\.(" + fileExtensions + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled)).ToList();
                foreach (Match match in matches)
                {
                    var path = match.Groups[1].Value;
                    var ext = match.Groups[2].Value;

                    var invalidFileNameChars = Regex.Match(match.Value, @".*[""<>:|?*/](.*)\.(" + ext + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    if (invalidFileNameChars.Success)
                    {
                        path = invalidFileNameChars.Groups[1].Value.Trim();
                        ext = invalidFileNameChars.Groups[2].Value;

                        if (string.IsNullOrWhiteSpace(path))
                        {
                            continue;
                        }
                    }

                    if (path.Contains("="))
                    {
                        regexFoundFiles.Add($"{path.Substring(path.IndexOf("=") + 1)}.{ext}");
                    }

                    path = path.Replace("\\\\", "\\").Trim();
                    regexFoundFiles.Add($"{path}.{ext}");

                    var basename = path.Substring(path.LastIndexOf("\\") + 1).Trim();
                    while (basename.Length > 0 && !((basename[0] >= 'A' && basename[0] <= 'Z') || (basename[0] >= 'a' && basename[0] <= 'z')))
                    {
                        basename = basename.Substring(1);
                        regexFoundFiles.Add($"{basename}.{ext}");
                    }
                }

                var matches2 = Regex.Matches(text, @"([\)\(\\\/a-zA-Z_0-9. -]{1,1000})\.(" + fileExtensions + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                foreach (Match match in matches2)
                {
                    var path = match.Groups[1].Value;
                    var ext = match.Groups[2].Value;
                    path = path.Replace("\\\\", "\\").Trim();
                    regexFoundFiles.Add($"{path}.{ext}");
                }

                result[fileName] = regexFoundFiles;
            });

            return result;
        }

        public void Dispose()
        {
            CleanTemp();
        }
    }
}