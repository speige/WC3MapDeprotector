using CSharpLua;
using ICSharpCode.Decompiler.Util;
using IniParser;
using IniParser.Model.Configuration;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using NuGet.Packaging;
using System.Collections.Immutable;
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
using FastMDX;
using System.Collections.Concurrent;
using War3Net.IO.Slk;
using Microsoft.Win32;
using System.Diagnostics;
using War3Net.Build.Import;
using System.Linq;

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
        protected static readonly HashSet<string> _nativeEditorFunctions;
        protected static readonly List<Encoding> _allEncodings;
        static Deprotector()
        {
            _nativeEditorFunctions = new HashSet<string>() { "config", "main", "CreateAllUnits", "CreateAllItems", "CreateNeutralPassiveBuildings", "CreateNeutralHostileBuildings", "CreatePlayerBuildings", "CreatePlayerUnits", "InitCustomPlayerSlots", "InitGlobals", "InitCustomTriggers", "RunInitializationTriggers", "CreateRegions", "CreateCameras", "InitSounds", "InitCustomTeams", "InitAllyPriorities", "CreateNeutralPassive", "CreateNeutralHostile", "InitUpgrades", "InitTechTree", "CreateAllDestructables", "InitBlizzard" };
            for (var playerIdx = 0; playerIdx <= 23; playerIdx++)
            {
                _nativeEditorFunctions.Add($"InitUpgrades_Player{playerIdx}");
                _nativeEditorFunctions.Add($"InitTechTree_Player{playerIdx}");
                _nativeEditorFunctions.Add($"CreateBuildingsForPlayer{playerIdx}");
                _nativeEditorFunctions.Add($"CreateUnitsForPlayer{playerIdx}");
            }

            _allEncodings = Encoding.GetEncodings().Select(x =>
            {
                try
                {
                    return x.GetEncoding();
                }
                catch { }
                return null;
            }).Where(x => x != null).OrderBy(x => x == Encoding.ASCII ? 0 : 1).ThenBy(x => x == Encoding.UTF8 ? 0 : 1).ToList();
        }

        protected const string ATTRIB = "Map deprotected by WC3MapDeprotector https://github.com/speige/WC3MapDeprotector\r\n\r\n";
        protected readonly HashSet<string> _commonFileExtensions = new HashSet<string>((new[] { "pcx", "gif", "cel", "dc6", "cl2", "ogg", "smk", "bik", "avi", "lua", "ai", "asi", "ax", "blp", "ccd", "clh", "css", "dds", "dll", "dls", "doo", "exe", "exp", "fdf", "flt", "gid", "html", "ifl", "imp", "ini", "j", "jpg", "js", "log", "m3d", "mdl", "mdx", "mid", "mmp", "mp3", "mpq", "mrf", "pld", "png", "shd", "slk", "tga", "toc", "ttf", "otf", "woff", "txt", "url", "w3a", "w3b", "w3c", "w3d", "w3e", "w3g", "w3h", "w3i", "w3m", "w3n", "w3f", "w3v", "w3z", "w3q", "w3r", "w3s", "w3t", "w3u", "w3x", "wai", "wav", "wct", "wpm", "wpp", "wtg", "wts", "mgv", "mg", "sav" }).Select(x => $".{x.Trim('.')}"), StringComparer.InvariantCultureIgnoreCase);

        protected string _inMapFile;
        protected readonly string _outMapFile;
        public DeprotectionSettings Settings { get; private set; }
        protected readonly Action<string> _logEvent;
        protected DeprotectionResult _deprotectionResult;
        protected ObjectDataParser _objectDataParser = new ObjectDataParser();
        protected Map _defaultSLKData;

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
                _logEvent($"added to global listfile: {fileName}");
            }
        }

        protected void ProcessGlobalListFile(StormMPQArchive archive)
        {
            if (File.Exists(InstallerListFileName))
            {
                HashSet<string> result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                var extractedListFileFolder = Path.Combine(Path.GetTempPath(), "WC3MapDeprotector");
                Directory.CreateDirectory(extractedListFileFolder);
                ZipFile.ExtractToDirectory(InstallerListFileName, extractedListFileFolder, true);

                var extractedListFileName = Path.Combine(extractedListFileFolder, "listfile.txt");
                if (File.Exists(extractedListFileName))
                {
                    result.AddRange(File.ReadLines(extractedListFileName));
                }

                if (File.Exists(WorkingListFileName))
                {
                    result.AddRange(File.ReadLines(WorkingListFileName));
                }

                Directory.CreateDirectory(Path.GetDirectoryName(WorkingListFileName));
                File.WriteAllLines(WorkingListFileName, result);
                File.Delete(InstallerListFileName);
                File.Delete(Path.Combine(extractedListFileFolder, "listfile.txt"));
            }

            if (File.Exists(WorkingListFileName))
            {
                archive.ProcessListFile(File.ReadLines(WorkingListFileName));
            }
        }

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

        protected string DeletedFilesPath
        {
            get
            {
                return Path.Combine(ExtractedFilesPath, "Deleted");
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
                IndexedFunctions = CompilationUnit.Declarations.OfType<JassFunctionDeclarationSyntax>().GroupBy(x => x.FunctionDeclarator.IdentifierName.Name).ToDictionary(x => x.Key, x => x.First());
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
            public MapCustomTextTriggers CustomTextTriggers { get; set; }
            public TriggerStrings TriggerStrings { get; set; }
            public MapUnits Units { get; set; }
            public Dictionary<UnitData, UnitDataDecompilationMetaData> UnitDecompilationMetaData { get; set; }
            public List<string> Destructables { get; set; }

            public List<MpqKnownFile> ConvertToFiles()
            {
                var map = new Map() { Info = Info, Units = Units, Sounds = Sounds, Cameras = Cameras, Regions = Regions, Triggers = Triggers, TriggerStrings = TriggerStrings, CustomTextTriggers = CustomTextTriggers };
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

        protected string decode(string encoded)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }

        protected Process LaunchWC3(string mapFileName)
        {
            return Utils.ExecuteCommand(UserSettings.WC3ExePath, $"-launch -loadfile \"{mapFileName}\"");
        }

        protected void LiveGameScanForUnknownFiles(StormMPQArchive archive)
        {
            const string REGISTRY_KEY = @"HKEY_CURRENT_USER\SOFTWARE\Blizzard Entertainment\Warcraft III";
            const string REGISTRY_VALUE = "Allow Local Files";

            var alreadyDiscoveredFileNames = new HashSet<string>(archive.GetDiscoveredFileNames(), StringComparer.InvariantCultureIgnoreCase);
            var deepScanDirectories = archive.DeepScan_GetDirectories(archive.GetDiscoveredFileNames());
            var oldEnableLocalFiles = Registry.GetValue(REGISTRY_KEY, REGISTRY_VALUE, null);
            Registry.SetValue(REGISTRY_KEY, REGISTRY_VALUE, 1);

            Process process = null;
            var tempMapLocation = Path.ChangeExtension(Path.GetTempPath(), "WC3MapDeprotector_LiveGameScan" + Path.GetExtension(_inMapFile));
            try
            {
                if (File.Exists(tempMapLocation))
                {
                    File.Delete(tempMapLocation);
                }

                File.Copy(_inMapFile, tempMapLocation);
                using (var scanner = new ProcessFileAccessScanner())
                using (var form = new frmLiveGameScanner())
                {
                    form.WC3ExePath = UserSettings.WC3ExePath;

                    scanner.FileAccessed += fileName =>
                    {
                        if (!alreadyDiscoveredFileNames.Contains(fileName))
                        {
                            archive.DiscoverFile(fileName, out var _);
                            archive.DiscoverUnknownFileNames_DeepScan(new List<string>() { fileName }, deepScanDirectories.ToList());
                            var newDeepScanDirectories = archive.DeepScan_GetDirectories(new List<string>() { fileName }).Where(x => !deepScanDirectories.Contains(x));
                            if (newDeepScanDirectories.Count() > 0)
                            {
                                archive.DiscoverUnknownFileNames_DeepScan(alreadyDiscoveredFileNames.ToList(), newDeepScanDirectories.ToList());
                                deepScanDirectories.AddRange(newDeepScanDirectories);
                            }
                            var recoveredFileNames = archive.GetDiscoveredFileNames().Where(x => !alreadyDiscoveredFileNames.Contains(x)).ToList();
                            if (recoveredFileNames.Any())
                            {
                                alreadyDiscoveredFileNames.UnionWith(recoveredFileNames);
                                form.BeginInvoke(() => form.UpdateLabels(archive));
                                AddToGlobalListFile(recoveredFileNames);
                                foreach (var recoveredFile in recoveredFileNames)
                                {
                                    _logEvent($"Extracted from MPQ: {recoveredFile}");
                                }
                            }

                            if (!archive.ShouldKeepScanningForUnknowns)
                            {
                                form.Close();
                                process.Kill();
                                return;
                            }
                        }
                    };

                    var restartProcess = () =>
                    {
                        if (process != null)
                        {
                            process.Kill();
                        }

                        scanner.MonitorFolderPath = UserSettings.WC3LocalFilesPath;
                        process = LaunchWC3(tempMapLocation);
                        scanner.ProcessId = process.Id;
                    };

                    form.RestartGameRequested += () => { restartProcess(); };
                    form.RequestGameRestart();

                    form.UpdateLabels(archive);
                    form.ShowDialog();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to launch Live Game Scanner: " + e.Message);
            }
            finally
            {
                if (process != null)
                {
                    process.Kill();
                }

                Registry.SetValue(REGISTRY_KEY, REGISTRY_VALUE, oldEnableLocalFiles);

                new Thread(() =>
                {
                    try
                    {
                        //wait for process to end so file can unlock
                        Thread.Sleep(15 * 1000);
                        File.Delete(tempMapLocation);
                    }
                    catch { }
                }).Start();
            }
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

        public string ReadAllText(string fileName)
        {
            return File.ReadAllText(fileName);
        }

        public string ConvertBytesToAscii(byte[] bytes)
        {
            //File.ReadAllText with Encoding.Ascii seems to corrupt non-readable characters sometimes
            var result = new StringBuilder();
            foreach (var value in bytes)
            {
                result.Append((char)value);
            }
            return result.ToString();
        }

        [GeneratedRegex(@"^(?!ID;|b;|c;|e)", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_SLKInvalidRow();

        [GeneratedRegex(@"(<[^>]*>)|(.)", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ObjectDataPropertyReferenceCSV();

        public async Task<DeprotectionResult> Deprotect()
        {
            while (WorldEditor.GetRunningInstanceOfEditor() != null)
            {
                MessageBox.Show("A running \"World Editor.exe\" process has been detected. Please close it while performing deprotection");
            }

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

            Directory.CreateDirectory(Path.GetDirectoryName(_outMapFile));

            _deprotectionResult = new DeprotectionResult();
            _deprotectionResult.WarningMessages.Add($"NOTE: This tool is a work in progress. Deprotection does not work perfectly on every map. If objects are missing or script has compilation errors, you will need to fix these by hand. You can get help from my YouTube channel or report defects by clicking the bug icon.");
            _deprotectionResult.WarningMessages.Add($"NOTE: World Editor has SD & HD modes. HD is prone to crashing and should not be used, it's find to use HD in game itself. The setting can be changed in WorldEditor.exe via File/Preferences/AssetMode");

            var baseMapFilesZip = Path.Combine(ExeFolderPath, "BaseMapFiles.zip"); 
            if (!Directory.Exists(BaseMapFilesPath) && File.Exists(baseMapFilesZip))
            {
                ZipFile.ExtractToDirectory(baseMapFilesZip, BaseMapFilesPath, true);
            }

            var silkObjectOptimizerZip = Path.Combine(ExeFolderPath, "SilkObjectOptimizer.zip");
            if (!File.Exists(SLKRecoverEXE) && File.Exists(silkObjectOptimizerZip))
            {
                ZipFile.ExtractToDirectory(silkObjectOptimizerZip, SLKRecoverPath, true);
            }

            _defaultSLKData = new Map();
            var defaultSLKFiles = Directory.GetFiles(Path.Combine(SLKRecoverPath, "STD")).ToList();
            if (defaultSLKFiles.Count > 0)
            {
                var slkData = _objectDataParser.ParseObjectDataFromSLKFiles(defaultSLKFiles);
                var objectDatas = _objectDataParser.ToWar3NetObjectData(slkData);
                foreach (var objectData in objectDatas)
                {
                    _defaultSLKData.GetObjectDataByType(objectData.ObjectDataType).CoreData.BaseValues = objectData.BaseValues;
                }
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
                if (ExtractFileFromArchive(inMPQArchive, "(listfile)"))
                {
                    inMPQArchive.ProcessListFile(File.ReadAllLines(Path.Combine(DiscoveredFilesPath, "(listfile)")).ToList());
                }
                else
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                }
                if (inMPQArchive.HasFakeFiles)
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                }
                if (inMPQArchive.HasUnknownHashes)
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                }
                if (inMPQArchive.HasFilesWithWrongEncryptionKey)
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                }

                DeleteAttributeListSignatureFiles();

                if (inMPQArchive.ShouldKeepScanningForUnknowns)
                {
                    inMPQArchive.ProcessDefaultListFile();
                }

                _logEvent($"Unknown file count: {inMPQArchive.UnknownFileCount}");

                var slkFiles = Directory.GetFiles(DiscoveredFilesPath, "*.slk", SearchOption.AllDirectories).ToList();
                foreach (var slkFile in slkFiles)
                {
                    var text = File.ReadAllLines(slkFile).ToList();
                    if (text.RemoveAll(x => Regex_SLKInvalidRow().IsMatch(x)) > 0)
                    {
                        MoveExtractedFileToDeletedFolder(slkFile);
                        File.WriteAllLines(slkFile, text);
                    }
                }

                if (slkFiles.Count > 0)
                {
                    var slkRecoverableFiles = new List<string>() { "war3map.w3a", "war3map.w3b", "war3map.w3d", "war3map.w3h", "war3map.w3q", "war3map.w3t", "war3map.w3u" };
                    
                    //todo: remove SLK Recover EXE & use parsing code instead
                    _logEvent("Generating temporary map for SLK Recover: slk.w3x");
                    var slkMpqArchive = Path.Combine(SLKRecoverPath, "slk.w3x");
                    var minimumExtraRequiredFiles = Directory.GetFiles(DiscoveredFilesPath, "*.txt", SearchOption.AllDirectories).Union(Directory.GetFiles(DiscoveredFilesPath, "war3map*", SearchOption.AllDirectories)).Union(Directory.GetFiles(DiscoveredFilesPath, "war3campaign*", SearchOption.AllDirectories)).Where(x => !slkRecoverableFiles.Contains(Path.GetFileName(x))).ToList();

                    BuildW3X(slkMpqArchive, DiscoveredFilesPath, slkFiles.Union(minimumExtraRequiredFiles).ToList());
                    _logEvent("slk.w3x generated");

                    _logEvent("Running SilkObjectOptimizer");
                    var slkRecoverOutPath = Path.Combine(SLKRecoverPath, "OUT");
                    new DirectoryInfo(slkRecoverOutPath).Delete(true);
                    Directory.CreateDirectory(slkRecoverOutPath);
                    Utils.WaitForProcessToExit(Utils.ExecuteCommand(SLKRecoverEXE, "", ProcessWindowStyle.Hidden));

                    _logEvent("SilkObjectOptimizer completed");

                    var parsedSlkObjectData = _objectDataParser.ParseObjectDataFromSLKFiles(slkFiles);
                    //NOTE: SLKRecover generates corrupted files for any SLK files that are missing, this excludes them
                    var validFiles = parsedSlkObjectData.Values.Select(x => x.ObjectDataType).Distinct().Select(x => $"war3map{x.GetMPQFileExtension()}").Distinct().ToHashSet(StringComparer.InvariantCultureIgnoreCase);

                    var map = DecompileMap();
                    foreach (var fileName in slkRecoverableFiles)
                    {
                        if (!validFiles.Contains(fileName))
                        {
                            continue;
                        }

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
                                //NOTE: ObjectEditorID in BaseValues are formatted as FourCC (OldId property). NewValues are formatted as FourCC:FourCC (NewId:OldId properties). This merges & moves data after decompiling SLK because there can be data in .w3a/etc ObjectEditor files also.
                                ObjectDataType slkType = ObjectDataParser.GetTypeByWar3MapFileExtension(Path.GetExtension(fileName));
                                var objectData = map.GetObjectDataByType(slkType);
                                SetMapFile(map, recoveredFile, true);
                                var recoveredSlkObjectData = map.GetObjectDataByType(slkType);

                                foreach (var slkValue in recoveredSlkObjectData.BaseValues)
                                {
                                    var value = objectData.BaseValues.FirstOrDefault(x => x.ToString() == slkValue.ToString());
                                    if (value == null)
                                    {
                                        objectData.CoreData.BaseValues = objectData.BaseValues.Concat(new[] { slkValue }).ToList().AsReadOnly();
                                    }
                                    else
                                    {
                                        foreach (var slkModification in slkValue.Modifications)
                                        {
                                            if (!value.Modifications.Any(x => x.Id == slkModification.Id && x.Level == slkModification.Level))
                                            {
                                                value.Modifications = value.Modifications.Concat(new[] { slkModification }).ToList().AsReadOnly();
                                            }
                                        }
                                    }
                                }
                                foreach (var slkValue in recoveredSlkObjectData.NewValues)
                                {
                                    var value = objectData.NewValues.FirstOrDefault(x => x.ToString() == slkValue.ToString());
                                    if (value == null)
                                    {
                                        objectData.CoreData.NewValues = objectData.NewValues.Concat(new[] { slkValue }).ToList().AsReadOnly();
                                    }
                                    else
                                    {
                                        foreach (var slkModification in slkValue.Modifications)
                                        {
                                            if (!value.Modifications.Any(x => x.Id == slkModification.Id && x.Level == slkModification.Level))
                                            {
                                                value.Modifications = value.Modifications.Concat(new[] { slkModification }).ToList().AsReadOnly();
                                            }
                                        }
                                    }
                                }

                                var toRemoveBaseValues = new List<War3NetObjectModificationWrapper>();
                                foreach (var baseValue in objectData.BaseValues)
                                {
                                    var newValue = objectData.NewValues.FirstOrDefault(x => x.NewId == baseValue.OldId);
                                    if (newValue != null)
                                    {
                                        toRemoveBaseValues.Add(baseValue);
                                        foreach (var baseModification in baseValue.Modifications)
                                        {
                                            var newModification = newValue.Modifications.FirstOrDefault(x => x.Id == baseModification.Id && x.Level == baseModification.Level);
                                            if (newModification != null)
                                            {
                                                newValue.Modifications = newValue.Modifications.Except(new[] { newModification }).ToList().AsReadOnly();
                                            }
                                            newValue.Modifications = newValue.Modifications.Concat(new[] { baseModification }).ToList().AsReadOnly();
                                        }
                                    }
                                }
                                objectData.CoreData.BaseValues = objectData.BaseValues.Except(toRemoveBaseValues).ToList().AsReadOnly();

                                File.WriteAllBytes(Path.Combine(DiscoveredFilesPath, fileName), objectData.CoreData.Serialize());
                            }
                            else
                            {
                                File.Move(recoveredFile, Path.Combine(DiscoveredFilesPath, fileName), false);
                            }
                        }
                    }

                    foreach (var slkFile in slkFiles)
                    {
                        if (!slkFile.ToLower().Contains("\\splats\\"))
                        {
                            MoveExtractedFileToDeletedFolder(slkFile);
                        }
                    }
                }

                if (inMPQArchive.ShouldKeepScanningForUnknowns && !DebugSettings.BenchmarkUnknownRecovery)
                {
                    ProcessGlobalListFile(inMPQArchive);
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

                var discoveredFileNamesBackup = inMPQArchive.GetDiscoveredFileNames();
                var unknownFileExtensions = _commonFileExtensions.ToList();
                var possibleFileNames_parsed = new ConcurrentHashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                var deepScanDirectories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                var alreadyDiscoveredFiles = inMPQArchive.GetDiscoveredFileNames();
                possibleFileNames_parsed.UnionWith(alreadyDiscoveredFiles);
                possibleFileNames_parsed.UnionWith(alreadyDiscoveredFiles.SelectMany(x => GetAlternateUnknownRecoveryFileNames(x)));
                var alreadyDiscoveredDeepScanDirectories = inMPQArchive.DeepScan_GetDirectories(alreadyDiscoveredFiles);
                deepScanDirectories.AddRange(alreadyDiscoveredDeepScanDirectories);
                DiscoverUnknownFileNames_DeepScan(inMPQArchive, possibleFileNames_parsed.Select(x => Path.GetFileName(x).Trim('\\')).ToList(), alreadyDiscoveredDeepScanDirectories);

                if (inMPQArchive.ShouldKeepScanningForUnknowns)
                {
                    _logEvent("Scanning for unknown filenames...");
                    var map = DecompileMap();
                    var previousScannedFiles = new HashSet<string>(possibleFileNames_parsed, StringComparer.InvariantCultureIgnoreCase);
                    var scannedFiles = ParseMapForUnknowns(map, unknownFileExtensions);
                    possibleFileNames_parsed.UnionWith(scannedFiles);
                    possibleFileNames_parsed.UnionWith(scannedFiles.SelectMany(x => GetAlternateUnknownRecoveryFileNames(x)));
                    var newScannedFiles = possibleFileNames_parsed.Where(x => !previousScannedFiles.Contains(x)).ToList();
                    inMPQArchive.ProcessListFile(newScannedFiles);

                    var newDeepScanDirectories = inMPQArchive.DeepScan_GetDirectories(inMPQArchive.GetDiscoveredFileNames()).Where(x => !deepScanDirectories.Contains(x));
                    deepScanDirectories.AddRange(newDeepScanDirectories);

                    if (inMPQArchive.ShouldKeepScanningForUnknowns)
                    {
                        if (newScannedFiles.Count() > 0)
                        {
                            DiscoverUnknownFileNames_DeepScan(inMPQArchive, newScannedFiles.Select(x => Path.GetFileName(x).Trim('\\')).ToList(), deepScanDirectories.ToList());
                        }
                        if (newDeepScanDirectories.Count() > 0)
                        {
                            DiscoverUnknownFileNames_DeepScan(inMPQArchive, possibleFileNames_parsed.Select(x => Path.GetFileName(x).Trim('\\')).ToList(), newDeepScanDirectories.ToList());
                        }
                    }
                }

                var alreadyScannedMD5WithFileExtension = new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                bool keepScanning;
                do
                {
                    keepScanning = false;

                    //NOTE: MPQ files can be encrypted. The decryption key is based on the real file name. StormLibrary attempts to crack the encryption, so it sometimes can extract without a name, but other times the extraction is garbage bytes. Each time a new MD5 is discovered, re-scan.
                    if (!inMPQArchive.ShouldKeepScanningForUnknowns)
                    {
                        break;
                    }

                    var previousMD5s = new HashSet<string>(inMPQArchive.AllExtractedMD5s, StringComparer.InvariantCultureIgnoreCase);
                    var previousScannedFiles = new HashSet<string>(possibleFileNames_parsed, StringComparer.InvariantCultureIgnoreCase);
                    var previousDiscoveredFileNames = new HashSet<string>(inMPQArchive.GetDiscoveredFileNames(), StringComparer.InvariantCultureIgnoreCase);

                    var allExtractedFiles = Directory.GetFiles(ExtractedFilesPath, "*", SearchOption.AllDirectories).ToList();
                    Parallel.ForEach(allExtractedFiles, fileName =>
                    {
                        using (var stream = File.OpenRead(fileName))
                        {
                            var extension = Path.GetExtension(fileName);
                            var md5 = stream.CalculateMD5();
                            if (alreadyScannedMD5WithFileExtension.TryGetValue(md5, out var previousExtension) && extension.Equals(previousExtension, StringComparison.InvariantCultureIgnoreCase))
                            {
                                return;
                            }

                            alreadyScannedMD5WithFileExtension[md5] = extension;
                        }

                        _logEvent($"Searching {fileName} ...");
                        var scannedFiles = ParseFileToDetectPossibleUnknowns(fileName, unknownFileExtensions);
                        possibleFileNames_parsed.UnionWith(scannedFiles);
                        possibleFileNames_parsed.UnionWith(scannedFiles.SelectMany(x => GetAlternateUnknownRecoveryFileNames(x)));
                    });

                    var newScannedFiles = possibleFileNames_parsed.Where(x => !previousScannedFiles.Contains(x)).ToList();
                    inMPQArchive.ProcessListFile(newScannedFiles);

                    var newDeepScanDirectories = inMPQArchive.DeepScan_GetDirectories(inMPQArchive.GetDiscoveredFileNames()).Where(x => !deepScanDirectories.Contains(x));
                    deepScanDirectories.AddRange(newDeepScanDirectories);

                    if (inMPQArchive.ShouldKeepScanningForUnknowns)
                    {
                        if (newScannedFiles.Count() > 0)
                        {
                            DiscoverUnknownFileNames_DeepScan(inMPQArchive, newScannedFiles.Select(x => Path.GetFileName(x).Trim('\\')).ToList(), deepScanDirectories.ToList());
                        }
                        if (newDeepScanDirectories.Count() > 0)
                        {
                            DiscoverUnknownFileNames_DeepScan(inMPQArchive, possibleFileNames_parsed.Select(x => Path.GetFileName(x).Trim('\\')).ToList(), newDeepScanDirectories.ToList());
                        }
                    }

                    keepScanning = inMPQArchive.DeepScan_GetDirectories(inMPQArchive.GetDiscoveredFileNames()).Any(x => !deepScanDirectories.Contains(x)) ||
                        inMPQArchive.GetDiscoveredFileNames().Any(x => !previousDiscoveredFileNames.Contains(x)) ||
                        inMPQArchive.AllExtractedMD5s.Any(x => !previousMD5s.Contains(x));
                } while (keepScanning);

                var recoveredFileNames = inMPQArchive.GetDiscoveredFileNames().Except(discoveredFileNamesBackup, StringComparer.InvariantCultureIgnoreCase).ToList();
                AddToGlobalListFile(recoveredFileNames);
                _logEvent($"Scan completed, {recoveredFileNames.Count} filenames found");

                if (DebugSettings.BenchmarkUnknownRecovery)
                {
                    var beforeGlobalListFileCount = inMPQArchive.UnknownFileCount;
                    string globalListFileBenchmarkMessage = "";
                    var discoveredFileNamesBackup_globalListFile = inMPQArchive.GetDiscoveredFileNames();
                    ProcessGlobalListFile(inMPQArchive);

                    foreach (var fileName in inMPQArchive.GetDiscoveredFileNames())
                    {
                        VerifyActualAndPredictedExtensionsMatch(inMPQArchive, fileName);
                    }

                    var recoveredFileNames_globalListFile = inMPQArchive.GetDiscoveredFileNames().Except(discoveredFileNamesBackup_globalListFile, StringComparer.InvariantCultureIgnoreCase).ToList();
                    if (recoveredFileNames_globalListFile.Any())
                    {
                        globalListFileBenchmarkMessage = $" KnownInGlobalListFile: {recoveredFileNames_globalListFile.Count}";

                        DebugSettings.Warn("Research how to get these files!");
                    }

                    _deprotectionResult.WarningMessages.Add("Done benchmarking. Unknowns left: " + beforeGlobalListFileCount + globalListFileBenchmarkMessage);
                    return _deprotectionResult;
                }

                foreach (var fileName in inMPQArchive.GetDiscoveredFileNames())
                {
                    if (StormMPQArchiveExtensions.IsInDefaultListFile(fileName) || !inMPQArchive.DiscoverFile(fileName, out var md5Hash) || inMPQArchive.IsPseudoFileName(fileName))
                    {
                        continue;
                    }

                    var predictedExtension = inMPQArchive.GetPredictedFileExtension(md5Hash);

                    if (string.IsNullOrWhiteSpace(predictedExtension))
                    {
                        _deprotectionResult.WarningMessages.Add($"Could not verify file extension for {fileName} - It's possible it's encrypted and was recovered under a fake file name. If game can't read file, you may need to discover the real file name using 'W3X Name Scanner' tool in MPQEditor.exe");
                    }
                }

                if (inMPQArchive.ShouldKeepScanningForUnknowns)
                {
                    LiveGameScanForUnknownFiles(inMPQArchive);
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

            Map.TryOpen(DiscoveredFilesPath, out var map_ObjectDataOnly, MapFiles.AbilityObjectData | MapFiles.BuffObjectData | MapFiles.DestructableObjectData | MapFiles.DoodadObjectData | MapFiles.ItemObjectData | MapFiles.UnitObjectData | MapFiles.UpgradeObjectData);

            var perLevelCSVProperties = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "atp1", "aub1" };
            var txtFiles = Directory.GetFiles(DiscoveredFilesPath, "*.txt", SearchOption.AllDirectories).ToList();
            foreach (var txtFile in txtFiles)
            {
                var txtObjectData = _objectDataParser.ParseObjectDataFromTxtFiles(new List<string>() { txtFile });
                var unknownObjects = 0;
                var containsObjectData = false;
                foreach (var parsedObjectData in txtObjectData)
                {
                    List<War3NetObjectModificationWrapper> matchingObjects = new List<War3NetObjectModificationWrapper>();
                    foreach (var type in Enum.GetValues(typeof(ObjectDataType)).Cast<ObjectDataType>())
                    {
                        //note: sometimes ObjectDataType for txt files is misclassified so we try all values. It's safe since IDs are unique.
                        var objectData = map_ObjectDataOnly.GetObjectDataByType(type);
                        matchingObjects.AddRange(objectData.BaseValues.Where(x => x.OldId.ToRawcode() == parsedObjectData.Key).Concat(objectData.NewValues.Where(x => x.NewId.ToRawcode() == parsedObjectData.Key)));
                    }

                    if (!matchingObjects.Any())
                    {
                        unknownObjects++;
                    }

                    containsObjectData = true;

                    foreach (var record in matchingObjects)
                    {
                        foreach (var txtModification in parsedObjectData.Value.Data.OrderBy(x => string.Equals(x.Key, "levels", StringComparison.InvariantCultureIgnoreCase) ? 0 : 1))
                        {
                            var properties = _objectDataParser.ConvertToPropertyIdPossibleRawCodes(parsedObjectData.Value.ObjectDataType, txtModification.Key);
                            if (!properties.Any())
                            {
                                DebugSettings.Warn("Missing ObjectEditor Key=>ID mapping");
                            }

                            foreach (var property in properties)
                            {
                                var propertyValues = new List<object>();

                                if (perLevelCSVProperties.Contains(property))
                                {
                                    const string TEMP_CSV_REPLACEMENT = "###TEMP_CSV_REPLACEMENT###";
                                    var value = txtModification.Value.ToString();
                                    var matches = Regex_ObjectDataPropertyReferenceCSV().Matches(value);
                                    if (matches.Any())
                                    {
                                        value = matches.Select(x => x.Value.Length == 1 ? x.Value : x.Value.Replace(",", TEMP_CSV_REPLACEMENT)).Aggregate((x, y) => x + y);
                                    }
                                    var isQuoted = value.StartsWith('"') && value.EndsWith('"');
                                    var split = isQuoted ? value.Trim('"').Split("\",\"") : value.Split(',');
                                    if (split.Length > 1)
                                    {
                                        propertyValues.AddRange(split.Select(x => x.Replace(TEMP_CSV_REPLACEMENT, ",")));
                                    }
                                }
                                
                                if (propertyValues.Count == 0)
                                {
                                    if (txtModification.Value is string)
                                    {
                                        var value = txtModification.Value.ToString();
                                        var isQuoted = value.StartsWith('"') && value.EndsWith('"');
                                        propertyValues.Add(isQuoted ? value.Trim('"').Replace("\",\"", ",") : value);
                                    }
                                    else
                                    {
                                        propertyValues.Add(txtModification.Value);
                                    }
                                }

                                var matchingModifications = record.Modifications.Where(x => x.Id.ToRawcode() == property).ToList();
                                if (!matchingModifications.Any())
                                {
                                    foreach (var propertyValue in propertyValues)
                                    {
                                        var newModification = record.GetEmptyObjectDataModificationWrapper();
                                        newModification.Id = property.FromRawcode();
                                        newModification.Value = propertyValue;
                                        record.Modifications = record.Modifications.Concat(new[] { newModification }).ToList().AsReadOnly();
                                    }
                                }
                                else
                                {
                                    for (var i = 0; i < matchingModifications.Count; i++)
                                    {
                                        var modification = matchingModifications[i];
                                        var value = propertyValues.Count > i ? propertyValues[i] : modification.Value;
                                        modification.Value = value;
                                    }
                                }
                            }
                        }
                    }
                }

                if (containsObjectData)
                {
                    MoveExtractedFileToDeletedFolder(txtFile);
                }

                if (containsObjectData && unknownObjects > 0)
                {
                    DebugSettings.Warn("Unable to decompile some object data");
                }
            }

            foreach (var file in map_ObjectDataOnly.GetObjectDataFiles())
            {
                using (var stream = File.OpenWrite(Path.Combine(DiscoveredFilesPath, file.FileName)))
                {
                    file.MpqStream.CopyTo(stream);
                }
            }

            PatchW3I();

            //These are probably protected, but the only way way to verify if they aren't is to parse the script (which is probably obfuscated), but if we can sucessfully parse, then we can just re-generate them to be safe.
            MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "war3mapunits.doo"));
            MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "war3map.wct"));
            MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "war3map.wtg"));
            MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "war3map.w3r"));
            //todo: diff with decompiled versions from war3map.j so we can update _deprotectionResult.CountOfProtectionsFound

            var skinPath = Path.Combine(DiscoveredFilesPath, "war3mapSkin.txt");
            if (!File.Exists(skinPath))
            {
                File.WriteAllText(skinPath, "");
            }
            var skinParser = new FileIniDataParser(new IniParser.Parser.IniDataParser(new IniParserConfiguration() { SkipInvalidLines = true, AllowDuplicateKeys = true, AllowDuplicateSections = true, AllowKeysWithoutSection = true }));
            var skinIni = skinParser.ReadFile(skinPath, Encoding.UTF8);
            skinIni.Configuration.AssigmentSpacer = "";
            skinIni[decode("RnJhbWVEZWY=")][decode("VVBLRUVQX0hJR0g=")] = decode("REVQUk9URUNURUQ=");
            skinIni[decode("RnJhbWVEZWY=")][decode("VVBLRUVQX0xPVw==")] = decode("REVQUk9URUNURUQ=");
            skinIni[decode("RnJhbWVEZWY=")][decode("VVBLRUVQX05PTkU=")] = decode("REVQUk9URUNURUQ=");
            skinParser.WriteFile(skinPath, skinIni);

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
                    if (ReadAllText(scriptFile) != ReadAllText(basePathScriptFileName))
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
                jassScript = $"// {ATTRIB}{ReadAllText(Path.Combine(DiscoveredFilesPath, "war3map.j"))}";
                try
                {
                    jassScript = DeObfuscateJassScript(map_ObjectDataOnly, jassScript);
                    scriptMetaData = DecompileJassScriptMetaData(jassScript);
                }
                catch { }
            }

            if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.lua")))
            {
                _deprotectionResult.WarningMessages.Add("WARNING: This map was built using Lua instead of Jass. Deprotection of Lua maps is not fully supported yet. It will open in the editor, but the render screen will be missing units/items/regions/cameras/sounds.");
                luaScript = DeObfuscateLuaScript($"-- {ATTRIB}{ReadAllText(Path.Combine(DiscoveredFilesPath, "war3map.lua"))}");
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
            }

            if (string.IsNullOrWhiteSpace(luaScript) && Settings.TranspileJassToLUA && !string.IsNullOrWhiteSpace(jassScript))
            {
                _logEvent("Transpiling JASS to LUA");

                luaScript = ConvertJassToLua(jassScript);
                if (!string.IsNullOrWhiteSpace(luaScript))
                {
                    File.WriteAllText(Path.Combine(DiscoveredFilesPath, "war3map.lua"), luaScript);
                    MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "war3map.j"));
                    _logEvent("Created war3map.lua");

                    try
                    {
                        var map = DecompileMap(x =>
                        {
                            if (x.Info != null)
                            {
                                x.Info.ScriptLanguage = ScriptLanguage.Lua;
                            }
                        });
                        var mapFiles = map.GetAllFiles();

                        var fileExtensionsToReplace = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".w3i", ".wts", ".wtg", ".wct", ".lua" };
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
                    }
                    catch { }
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
                MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "war3map.wct"));
                _deprotectionResult.CriticalWarningCount++;
                _deprotectionResult.WarningMessages.Add("WARNING: triggers could not be recovered. Map will still open in WorldEditor & run, but saving in world editor will corrupt your war3map.j or war3map.lua script file.");
            }


            var extraPath = Path.Combine(DiscoveredFilesPath, "war3mapExtra.txt");
            if (!File.Exists(extraPath))
            {
                File.WriteAllText(extraPath, "");
            }
            var extraParser = new FileIniDataParser(new IniParser.Parser.IniDataParser(new IniParserConfiguration() { SkipInvalidLines = true, AllowDuplicateKeys = true, AllowDuplicateSections = true, AllowKeysWithoutSection = true }));
            var extraIni = extraParser.ReadFile(extraPath, Encoding.UTF8);
            extraIni.Configuration.AssigmentSpacer = "";
            extraIni["MapExtraInfo"]["EnableJassHelper"] = "true";
            extraParser.WriteFile(extraPath, extraIni);

            BuildImportList();

            var autoUpgradeError = false;
            try
            {
                CorrectUnitPositionZOffsets();
            }
            catch
            {
                autoUpgradeError = true;
            }
            try
            {
                MoveObjectEditorStringsToTriggerStrings();
            }
            catch
            {
                autoUpgradeError = true;
            }
            try
            {
                RefactorSkinnableProperties();
            }
            catch
            {
                autoUpgradeError = true;
            }
            try
            {
                UpgradeToLatestFileFormats();
            }
            catch
            {
                autoUpgradeError = true;
            }
            try
            {
                RepairW3XNativeFilesInEditor();
            }
            catch
            {
                autoUpgradeError = true;
            }
            if (autoUpgradeError)
            {
                _deprotectionResult.WarningMessages.Add("Failed to upgrade to latest reforged file format. Map may still load correctly in WorldEditor");
            }

            AnnotateScriptFile();

            BuildW3X(_outMapFile, DiscoveredFilesPath, Directory.GetFiles(DiscoveredFilesPath, "*", SearchOption.AllDirectories).ToList());

            //todo: open final file in WorldEditor with "Local Files" to find remaining textures/3dModels?

            _deprotectionResult.WarningMessages.Add("NOTE: You may need to fix script compiler errors before saving in world editor.");
            _deprotectionResult.WarningMessages.Add("NOTE: Objects added directly to editor render screen, like units/doodads/items/etc are stored in an editor-only file called war3mapunits.doo and converted to script code on save. Protection deletes the editor file and obfuscates the script code to make it harder to recover. Decompiling the war3map script file back into war3mapunits.doo is not 100% perfect for most maps. Please do extensive testing to ensure everything still behaves correctly, you may have to do many manual bug fixes in world editor after deprotection.");
            _deprotectionResult.WarningMessages.Add("NOTE: If deprotected map works correctly, but becomes corrupted after saving in world editor, it is due to editor-generated code in your war3map.j or war3map.lua. You should delete WorldEditor triggers, keep a backup of the war3map.j or war3map.lua script file, edit with visual studio code, and add it with MPQ tool after saving in WorldEditor. The downside to this approach is any objects you manually add to the rendering screen will get saved to the broken script file, so you will need to use a tool like WinMerge to diff the old/new script file and copy any editor-generated changes to your backup script file.");
            _deprotectionResult.WarningMessages.Add("NOTE: Editor-generated functions in trigger window have been renamed with a suffix of _old. If saving in world editor causes game to become corrupted, check the _old functions to find code that may need to be moved to an initialization script.");

            _deprotectionResult.WarningMessages = _deprotectionResult.WarningMessages.Distinct().ToList();

            return _deprotectionResult;
        }

        protected void AnnotateMap(Map map)
        {
            if (int.TryParse((map.Info.MapName ?? "").Replace("TRIGSTR_", ""), out var trgStr1))
            {
                var str = map.TriggerStrings?.Strings?.FirstOrDefault(x => x.Key == trgStr1);
                if (str != null)
                {
                    str.Value = "\u0044\u0045\u0050\u0052\u004F\u0054\u0045\u0043\u0054\u0045\u0044" + (str.Value ?? "");
                    if (str.Value.Length > 36)
                    {
                        str.Value = str.Value.Substring(0, 36);
                    }
                }
            }
            else
            {
                map.Info.MapName = "\u0044\u0045\u0050\u0052\u004F\u0054\u0045\u0043\u0054\u0045\u0044" + (map.Info.MapName ?? "");
            }

            map.Info.RecommendedPlayers = "\u0057\u0043\u0033\u004D\u0061\u0070\u0044\u0065\u0070\u0072\u006F\u0074\u0065\u0063\u0074\u006F\u0072";

            if (map.TriggerStrings?.Strings != null && int.TryParse((map.Info.MapDescription ?? "").Replace("TRIGSTR_", ""), out var trgStr2))
            {
                var str = map.TriggerStrings.Strings.FirstOrDefault(x => x.Key == trgStr2);
                if (str != null)
                {
                    str.Value = $"{str.Value ?? ""}\r\n\u004D\u0061\u0070\u0020\u0064\u0065\u0070\u0072\u006F\u0074\u0065\u0063\u0074\u0065\u0064\u0020\u0062\u0079\u0020\u0068\u0074\u0074\u0070\u0073\u003A\u002F\u002F\u0067\u0069\u0074\u0068\u0075\u0062\u002E\u0063\u006F\u006D\u002F\u0073\u0070\u0065\u0069\u0067\u0065\u002F\u0057\u0043\u0033\u004D\u0061\u0070\u0044\u0065\u0070\u0072\u006F\u0074\u0065\u0063\u0074\u006F\u0072\r\n\r\n";
                }
            }

            map.Info.CampaignBackgroundNumber = -1;
            map.Info.LoadingScreenBackgroundNumber = -1;
            map.Info.LoadingScreenPath = "\u004C\u006F\u0061\u0064\u0069\u006E\u0067\u0053\u0063\u0072\u0065\u0065\u006E\u002E\u006D\u0064\u0078";
        }

        protected void AnnotateScriptFile()
        {
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.j")))
            {
                var script = ReadAllText(Path.Combine(DiscoveredFilesPath, "war3map.j"));
                var blz = Regex_JassScriptInitBlizzard().Match(script);
                if (blz.Success)
                {
                    var bits = new byte[] { 0b_00001101, 0b_00001010, 0b_01100011, 0b_01100001, 0b_01101100, 0b_01101100, 0b_00100000, 0b_01000100, 0b_01101001, 0b_01110011, 0b_01110000, 0b_01101100, 0b_01100001, 0b_01111001, 0b_01010100, 0b_01100101, 0b_01111000, 0b_01110100, 0b_01010100, 0b_01101111, 0b_01000110, 0b_01101111, 0b_01110010, 0b_01100011, 0b_01100101, 0b_00101000, 0b_01000111, 0b_01100101, 0b_01110100, 0b_01010000, 0b_01101100, 0b_01100001, 0b_01111001, 0b_01100101, 0b_01110010, 0b_01110011, 0b_01000001, 0b_01101100, 0b_01101100, 0b_00101000, 0b_00101001, 0b_00101100, 0b_00100000, 0b_00100010, 0b_01100100, 0b_00110011, 0b_01110000, 0b_01110010, 0b_00110000, 0b_01110100, 0b_00110011, 0b_01100011, 0b_01110100, 0b_00110011, 0b_01100100, 0b_00100010, 0b_00101001, 0b_00001101, 0b_00001010 };
                    for (var idx = 0; idx < bits.Length; ++idx)
                    {
                        script = script.Insert(blz.Index + blz.Length + idx, ((char)bits[idx]).ToString());
                    }
                }

                File.WriteAllText(Path.Combine(DiscoveredFilesPath, "war3map.j"), script);
            }
            else if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.lua")))
            {
                //todo
            }
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

                transpiler.RegisterJassFile(JassSyntaxFactory.ParseCompilationUnit(ReadAllText(Path.Combine(ExeFolderPath, "common.j"))));
                transpiler.RegisterJassFile(JassSyntaxFactory.ParseCompilationUnit(ReadAllText(Path.Combine(ExeFolderPath, "blizzard.j"))));
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

        protected string RemovePathPrefix(string fileName, string pathPrefix)
        {
            if (!pathPrefix.EndsWith(@"\"))
            {
                pathPrefix += @"\";
            }

            if (fileName.StartsWith(pathPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return fileName.Substring(pathPrefix.Length);
            }

            return fileName;
        }

        protected void RefactorSkinnableProperties()
        {
            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var tempMapFileName = Path.Combine(WorkingFolderPath, "skinObjectData.w3x");
            BuildW3X(tempMapFileName, DiscoveredFilesPath, nativeFileNames.Select(x => @$"{DiscoveredFilesPath}\{x}").ToList());
            var map = Map.Open(tempMapFileName);

            var supportedFourCCAttributes = new HashSet<string>() { "ahky", "anam", "aret", "arhk", "arut", "asat", "atp1", "aub1", "atat", "auhk", "aut1", "auu1", "alig", "amat", "ansf", "aeat", "acat", "aart", "aaea", "auar", "abpx", "abpy", "arar", "arpx", "aani", "acap", "aubx", "amac", "arpy", "fnam", "ftat", "ftip", "fube", "fsat", "feat", "fnsf", "fart", "fta0", "fmat", "fspt", "fta1", "ftac", "fspd", "bgpm", "bmas", "bmis", "bnam", "bsuf", "bmmb", "bmmg", "bmmr", "bsel", "bdsn", "bfil", "bgsc", "bvar", "blit", "ides", "unam", "utip", "utub", "uhot", "ubpx", "ubpy", "iclb", "iclg", "iclr", "iico", "ifil", "isca", "uawt", "uico", "umdl", "upro", "usca", "uspa", "utpr", "unsf", "ulpz", "ua1m", "ussi", "uimz", "utaa", "ucun", "ucut", "ussc", "uble", "uma1", "usnd", "umxp", "ushu", "ushb", "uclg", "uclr", "uclb", "uubs", "ucua", "uslz", "uver", "uaap", "uerd", "upru", "ushh", "ushw", "ushx", "ushy", "umxr", "uani", "ushr", "ua2m", "ghk1", "gnam", "gtp1", "gub1", "gar1", "gbpx", "gbpy" };
            var allObjectData = map.GetAllObjectData();
            foreach ((var dataType, var objectData) in allObjectData)
            {
                foreach (var data in objectData.CoreData.BaseValues)
                {
                    var modificationsToMove = new List<War3NetObjectDataModificationWrapper>();
                    var modifications = data.Modifications;
                    foreach (var modification in modifications)
                    {
                        if (supportedFourCCAttributes.Contains(modification.ToString()) && modification.Value is string)
                        {
                            modificationsToMove.Add(modification);
                        }
                    }
                    data.Modifications = data.Modifications.Except(modificationsToMove).ToList().AsReadOnly();
                    var skinData = objectData.SkinData.BaseValues.FirstOrDefault(x => x.ToString() == data.ToString());
                    if (skinData == null)
                    {
                        skinData = ObjectDataParser.GetEmptyObjectModificationWrapper(dataType);
                        skinData.OldId = data.OldId;
                        skinData.NewId = data.NewId;
                        skinData.Unk = data.Unk;
                        objectData.SkinData.BaseValues = objectData.SkinData.BaseValues.Concat(new[] { skinData }).ToList().AsReadOnly();
                    }
                    skinData.Modifications = skinData.Modifications.Concat(modificationsToMove).ToList().AsReadOnly();
                }

                foreach (var data in objectData.CoreData.NewValues)
                {
                    var modificationsToMove = new List<War3NetObjectDataModificationWrapper>();
                    var modifications = data.Modifications;
                    foreach (var modification in modifications)
                    {
                        if (supportedFourCCAttributes.Contains(modification.ToString()) && modification.Value is string)
                        {
                            modificationsToMove.Add(modification);
                        }
                    }
                    data.Modifications = data.Modifications.Except(modificationsToMove).ToList().AsReadOnly();
                    var skinData = objectData.SkinData.NewValues.FirstOrDefault(x => x.ToString() == data.ToString());
                    if (skinData == null)
                    {
                        skinData = ObjectDataParser.GetEmptyObjectModificationWrapper(dataType);
                        skinData.OldId = data.OldId;
                        skinData.NewId = data.NewId;
                        skinData.Unk = data.Unk;
                        objectData.SkinData.NewValues = objectData.SkinData.NewValues.Concat(new[] { skinData }).ToList().AsReadOnly();
                    }
                    skinData.Modifications = skinData.Modifications.Concat(modificationsToMove).ToList().AsReadOnly();
                }
            }

            var mapFiles = map.GetAllFiles();
            foreach (var file in mapFiles)
            {
                SaveDecompiledArchiveFile(file);
            }

            try
            {
                File.Delete(tempMapFileName);
            }
            catch { }
        }

        protected void UpgradeToLatestFileFormats()
        {
            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var tempMapFileName = Path.Combine(WorkingFolderPath, "fileFormats.w3x");
            BuildW3X(tempMapFileName, DiscoveredFilesPath, nativeFileNames.Select(x => @$"{DiscoveredFilesPath}\{x}").ToList());
            var map = Map.Open(tempMapFileName);

            if (map.Cameras != null)
            {
                map.Cameras.FormatVersion = Enum.GetValues(typeof(MapCamerasFormatVersion)).Cast<MapCamerasFormatVersion>().OrderByDescending(x => x).First();
                map.Cameras.UseNewFormat = true;
            }

            if (map.CustomTextTriggers != null)
            {
                map.CustomTextTriggers.FormatVersion = Enum.GetValues(typeof(MapCustomTextTriggersFormatVersion)).Cast<MapCustomTextTriggersFormatVersion>().OrderByDescending(x => x).First();
                map.CustomTextTriggers.SubVersion = Enum.GetValues(typeof(MapCustomTextTriggersSubVersion)).Cast<MapCustomTextTriggersSubVersion>().OrderByDescending(x => x).First();
            }

            if (map.Doodads != null)
            {
                map.Doodads.FormatVersion = Enum.GetValues(typeof(MapWidgetsFormatVersion)).Cast<MapWidgetsFormatVersion>().OrderByDescending(x => x).First();
                map.Doodads.SubVersion = Enum.GetValues(typeof(MapWidgetsSubVersion)).Cast<MapWidgetsSubVersion>().OrderByDescending(x => x).First();
                map.Doodads.SpecialDoodadVersion = Enum.GetValues(typeof(SpecialDoodadVersion)).Cast<SpecialDoodadVersion>().OrderByDescending(x => x).First();
                map.Doodads.UseNewFormat = true;
                foreach (var doodad in map.Doodads.Doodads)
                {
                    doodad.SkinId = doodad.TypeId;
                }
            }

            if (map.Environment != null)
            {
                map.Environment.FormatVersion = Enum.GetValues(typeof(MapEnvironmentFormatVersion)).Cast<MapEnvironmentFormatVersion>().OrderByDescending(x => x).First();
            }

            if (map.ImportedFiles != null)
            {
                map.ImportedFiles.FormatVersion = Enum.GetValues(typeof(ImportedFilesFormatVersion)).Cast<ImportedFilesFormatVersion>().OrderByDescending(x => x).First();
            }

            if (map.Info != null)
            {
                map.Info.FormatVersion = Enum.GetValues(typeof(MapInfoFormatVersion)).Cast<MapInfoFormatVersion>().OrderByDescending(x => x).First();
                map.Info.EditorVersion = Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>().OrderByDescending(x => x).First();
                map.Info.GameDataVersion = GameDataVersion.TFT;
                map.Info.GameVersion ??= new Version(2, 0, 0, 22370);
                map.Info.SupportedModes = SupportedModes.SD | SupportedModes.HD;
            }

            if (map.PathingMap != null)
            {
                map.PathingMap.FormatVersion = Enum.GetValues(typeof(MapPathingMapFormatVersion)).Cast<MapPathingMapFormatVersion>().OrderByDescending(x => x).First();
            }

            if (map.PreviewIcons != null)
            {
                map.PreviewIcons.FormatVersion = Enum.GetValues(typeof(MapPreviewIconsFormatVersion)).Cast<MapPreviewIconsFormatVersion>().OrderByDescending(x => x).First();
            }

            if (map.Regions != null)
            {
                map.Regions.FormatVersion = Enum.GetValues(typeof(MapRegionsFormatVersion)).Cast<MapRegionsFormatVersion>().OrderByDescending(x => x).First();
            }

            if (map.Sounds != null)
            {
                map.Sounds.FormatVersion = Enum.GetValues(typeof(MapSoundsFormatVersion)).Cast<MapSoundsFormatVersion>().OrderByDescending(x => x).First();
            }

            if (map.Triggers != null)
            {
                map.Triggers.FormatVersion = Enum.GetValues(typeof(MapTriggersFormatVersion)).Cast<MapTriggersFormatVersion>().OrderByDescending(x => x).First();
                map.Triggers.SubVersion = Enum.GetValues(typeof(MapTriggersSubVersion)).Cast<MapTriggersSubVersion>().OrderByDescending(x => x).First();
            }

            if (map.Units != null)
            {
                map.Units.UseNewFormat = true;
                foreach (var unit in map.Units.Units)
                {
                    unit.SkinId = unit.TypeId;
                }
            }

            var allObjectData = map.GetAllObjectData();
            foreach ((var dataType, var objectData) in allObjectData)
            {
                objectData.FormatVersion = War3Net.Build.Object.ObjectDataFormatVersion.v3;
                var combinedObjectData = objectData.BaseValues.Concat(objectData.NewValues).ToList();
                foreach (var data in combinedObjectData)
                {
                    data.Unk = new List<int>() { 0 };
                }
            }

            var mapFiles = map.GetAllFiles();
            foreach (var file in mapFiles)
            {
                SaveDecompiledArchiveFile(file);
            }

            try
            {
                File.Delete(tempMapFileName);
            }
            catch { }
        }

        protected void MoveObjectEditorStringsToTriggerStrings()
        {
            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var tempMapFileName = Path.Combine(WorkingFolderPath, "triggerStrings.w3x");
            BuildW3X(tempMapFileName, DiscoveredFilesPath, nativeFileNames.Select(x => @$"{DiscoveredFilesPath}\{x}").ToList());
            var map = Map.Open(tempMapFileName);

            const string TRIGSTR_ = "TRIGSTR_";
            var triggerStringSupportedObjectAttributeFourCCs = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "gef1", "gcls", "gef2", "gef3", "gef4", "ahky", "anam", "aret", "arhk", "arut", "atp1", "aub1", "auhk", "aut1", "auu1", "ftip", "fube", "fnam", "bnam", "bsuf", "ides", "unam", "utip", "utub", "uhot", "uawt", "upro", "utpr", "ucun", "ucut", "ghk1", "gnam", "gtp1", "gub1" };
            
            map.TriggerStrings ??= new TriggerStrings();
            var maxTriggerKey = map.TriggerStrings.Strings.Select(x => x.Key).Concat(new uint[] { 0 }).Max();
            var allObjectData = map.GetAllObjectData();
            foreach ((var dataType, var objectData) in allObjectData)
            {
                var combinedObjectData = objectData.BaseValues.Concat(objectData.NewValues).ToList();
                foreach (var data in combinedObjectData)
                {
                    foreach (var modification in data.Modifications)
                    {
                        if (triggerStringSupportedObjectAttributeFourCCs.Contains(modification.ToString()) && modification.Value is string valueString && valueString.Length > 1)
                        {
                            if (!valueString.StartsWith(TRIGSTR_, StringComparison.InvariantCultureIgnoreCase))
                            {
                                maxTriggerKey++;
                                var triggerString = new TriggerString() { Key = maxTriggerKey, Value = valueString };
                                modification.Value = TRIGSTR_ + maxTriggerKey;
                                map.TriggerStrings.Strings.Add(triggerString);
                            }
                        }
                    }
                }
            }

            AnnotateMap(map);
            var mapFiles = map.GetAllFiles();
            foreach (var file in mapFiles)
            {
                SaveDecompiledArchiveFile(file);
            }

            try
            {
                File.Delete(tempMapFileName);
            }
            catch { }
        }

        protected void RepairW3XNativeFilesInEditor()
        {
            //NOTE: minor corruption can be repaired by opening map & saving, but need to remove all models/etc from w3x 1st or it will crash editor.
            //todo: double-check that war3map.wts doesn't have any lost trigger data after saving with baseMap triggers

            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var baseFileNames = Directory.GetFiles(BaseMapFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, BaseMapFilesPath)).ToList();
            var notRepairableFiles = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "war3map.j", "war3map.imp", "war3map.wct", "war3map.wtg" };

            var allFiles = nativeFileNames.Concat(baseFileNames).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            var repairNativesFolder = Path.Combine(TempFolderPath, "repairNatives");
            foreach (var file in allFiles)
            {
                var baseFileName = Path.Combine(BaseMapFilesPath, file);
                var realFileName = Path.Combine(DiscoveredFilesPath, file);
                var repairFileName = Path.Combine(repairNativesFolder, file);

                Directory.CreateDirectory(Path.GetDirectoryName(repairFileName));

                if (File.Exists(realFileName) && !notRepairableFiles.Contains(file))
                {
                    File.Copy(realFileName, repairFileName);
                }
                else if (File.Exists(baseFileName))
                {
                    File.Copy(baseFileName, repairFileName);
                }
            }

            var tempMapFileName = Path.Combine(repairNativesFolder, "map.w3x");
            BuildW3X(tempMapFileName, repairNativesFolder, allFiles.Select(x => @$"{repairNativesFolder}\{x}").Where(x => File.Exists(x)).ToList());

            using (var form = new frmWorldEditorInstructions())
            using (var editor = new WorldEditor())
            {
                form.Show();
                Application.DoEvents();
                editor.LoadMapFile(tempMapFileName);
                editor.SaveMap();
            }

            var map = Map.Open(tempMapFileName);
            var mapFiles = map.GetAllFiles();
            foreach (var file in mapFiles)
            {                
                if (!notRepairableFiles.Contains(file.FileName))
                {
                    SaveDecompiledArchiveFile(file);
                }
            }

            try
            {
                File.Delete(tempMapFileName);
            }
            catch { }
        }

        protected void CorrectUnitPositionZOffsets()
        {
            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var tempMapFileName = Path.Combine(WorkingFolderPath, "unitPositionZ.w3x");
            BuildW3X(tempMapFileName, DiscoveredFilesPath, nativeFileNames.Select(x => @$"{DiscoveredFilesPath}\{x}").ToList());
            var map = Map.Open(tempMapFileName);

            var tileColumns = map.Environment.Width + 1;
            var tileRows = map.Environment.Height + 1;
            var tileWidth = (uint)((map.Environment.Right - map.Environment.Left) / map.Environment.Width);
            var tileHeight = (uint)((map.Environment.Top - map.Environment.Bottom) / map.Environment.Height);
            foreach (var unit in map.Units.Units)
            {
                var tileX = Math.Clamp((uint)((unit.Position.X - map.Environment.Left) / tileWidth), 0, tileColumns - 1);
                var tileY = Math.Clamp((uint)(tileRows - (map.Environment.Top - unit.Position.Y) / tileHeight), 0, tileRows - 1);
                var tile = map.Environment.TerrainTiles[(int)(tileY * tileColumns + tileX)];
                var heightData = (ushort)typeof(TerrainTile).GetField("_heightData", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tile);
                var unitPosition = unit.Position;
                unitPosition.Z = (heightData - 8192f + (tile.CliffLevel - 2) * 512f) / 4;
                unit.Position = unitPosition;
            }

            var mapFiles = map.GetAllFiles();
            foreach (var file in mapFiles)
            {
                SaveDecompiledArchiveFile(file);
            }

            try
            {
                File.Delete(tempMapFileName);
            }
            catch { }
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

            var MPQ_CREATE_ATTRIBUTES = (uint)0x00200000;
            var maxFileCount = (uint)Math.Pow(2, (int)Math.Ceiling(Math.Log(filesToInclude.Count, 2)) + 1);
            StormLibrary.SFileCreateArchive(fileName, MPQ_CREATE_ATTRIBUTES, maxFileCount, out var archiveHandle);
            try
            {
                foreach (var file in filesToInclude)
                {
                    var shortFileName = RemovePathPrefix(file, baseFolder);
                    const uint MPQ_FILE_COMPRESS = 0x00000200;
                    const uint MPQ_COMPRESSION_ZLIB = 0x02;
                    const uint MPQ_COMPRESSION_HUFFMANN = 0x01;
                    const uint MPQ_COMPRESSION_NEXT_SAME = 0xFFFFFFFF;

                    StormLibrary.SFileAddFileEx(archiveHandle, file, shortFileName, MPQ_FILE_COMPRESS, string.Equals(Path.GetExtension(shortFileName), ".wav", StringComparison.InvariantCultureIgnoreCase) ? MPQ_COMPRESSION_HUFFMANN : MPQ_COMPRESSION_ZLIB, MPQ_COMPRESSION_NEXT_SAME);

                    _logEvent($"Added to MPQ: {file}");
                }
            }
            finally
            {
                StormLibrary.SFileCloseArchive(archiveHandle);
            }

            _logEvent($"Created MPQ with {filesToInclude.Count} files");

            if (!File.Exists(fileName))
            {
                throw new Exception("Failed to create output MPQ archive");
            }

            CopyMPQHeader(_inMapFile, fileName);
            _logEvent($"Created map '{fileName}'");
        }

        protected bool IsHM3WHeader(byte[] header)
        {
            return header[0] == 'H' && header[1] == 'M' && header[2] == '3' && header[3] == 'W';
        }

        protected void CopyMPQHeader(string copyFromMPQFileName, string copyToMPQFileName)
        {
            var copyFromHeader = new byte[512];
            using (var stream = File.OpenRead(copyFromMPQFileName))
            {
                stream.Read(copyFromHeader, 0, 512);
            }
            if (!IsHM3WHeader(copyFromHeader))
            {
                return;
            }

            _logEvent("Copying HM3W header");
            var tempFileName = Path.Combine(WorkingFolderPath, "mpqheader.w3x");
            using (var writeStream = File.OpenWrite(tempFileName))
            {
                var copyToHeader = new byte[512];
                using (var readStream = File.OpenRead(copyToMPQFileName))
                {
                    readStream.Read(copyToHeader, 0, 512);
                    writeStream.Write(copyFromHeader, 0, 512);

                    if (!IsHM3WHeader(copyToHeader))
                    {
                        writeStream.Write(copyToHeader, 0, 512);
                    }

                    readStream.CopyTo(writeStream);
                }
            }

            File.Copy(tempFileName, copyToMPQFileName, true);
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
            using (var stream = new FileStream(fileName, FileMode.Create))
            {
                mpqFile.MpqStream.CopyTo(stream);
            }

            _logEvent($"Reconstructed by parsing other map files: {fileName}");

            return true;
        }

        protected void MoveExtractedFileToDeletedFolder(string extractedFileName)
        {
            if (!File.Exists(extractedFileName))
            {
                return;
            }

            var newFileName = Path.Combine(DeletedFilesPath, Path.GetRelativePath(ExtractedFilesPath, extractedFileName));
            Directory.CreateDirectory(Path.GetDirectoryName(newFileName));
            while (File.Exists(newFileName))
            {
                newFileName = newFileName + "_old";
            }
            File.Move(extractedFileName, newFileName);
        }

        protected bool ExtractFileFromArchive(StormMPQArchive archive, string archiveFileName)
        {
            if (!archive.DiscoverFile(archiveFileName, out var md5Hash))
            {
                return false;
            }

            _logEvent($"Extracted from MPQ: {archiveFileName}");

            return true;
        }

        protected void VerifyActualAndPredictedExtensionsMatch(StormMPQArchive archive, string archiveFileName)
        {
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
                var debugIgnoreMistakenPrediction = (string.Equals(realExtension, ".ini") && string.Equals(predictedExtension, ".txt")) ||
                    (string.Equals(predictedExtension, ".ini") && string.Equals(realExtension, ".txt")) ||
                    (string.Equals(realExtension, ".wav") && string.Equals(predictedExtension, ".mp3")) ||
                    (string.Equals(predictedExtension, ".wav") && string.Equals(realExtension, ".mp3")) ||
                    (string.Equals(realExtension, ".mdx") && string.Equals(predictedExtension, ".blp")) ||
                    (string.Equals(predictedExtension, ".mdx") && string.Equals(realExtension, ".blp"));
                if (!debugIgnoreMistakenPrediction)
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
            return Directory.GetFiles(UnknownFilesPath, "*", SearchOption.TopDirectoryOnly).Select(x => RemovePathPrefix(x, UnknownFilesPath)).ToList();
        }

        protected void BruteForceUnknownFileNames(StormMPQArchive archive)
        {
            //todo: Convert to Vector<> variables to support SIMD architecture speed up
            var unknownFileCount = archive.UnknownFileCount;
            _logEvent($"unknown files remaining: {unknownFileCount}");

            var directories = archive.GetDiscoveredFileNames().Select(x => Path.GetDirectoryName(x).ToUpper()).Select(x => x.Trim('\\')).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            var extensions = GetPredictedUnknownFileExtensions();

            const int maxFileNameLength = 75;
            _logEvent($"Brute forcing filenames from length 1 to {maxFileNameLength}");

            var possibleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_!&()' .".ToUpper().ToCharArray();

            var foundFileLock = new object();
            var foundFileCount = 0;
            try
            {
                var simdLength = Vector<int>.Count;
                Parallel.ForEach(directories, new ParallelOptions() { CancellationToken = Settings.BruteForceCancellationToken.Token }, directoryName =>
                {
                    var leftDirectoryHash = new MPQPartialHash(MPQPartialHash.LEFT_OFFSET);
                    if (!leftDirectoryHash.TryAddString(directoryName + "\\"))
                    {
                        return;
                    }

                    var testCallback = (string bruteText) =>
                    {
                        //todo: refactor to save file prefix hash so it only needs to update based on the most recent character changed
                        var leftBruteTextHash = leftDirectoryHash;
                        leftBruteTextHash.AddString(bruteText);

                        foreach (var fileExtension in extensions)
                        {
                            var leftFileHash = leftBruteTextHash;
                            leftFileHash.AddString(fileExtension);
                            var leftHash = leftFileHash.Value;
                            if (archive.LeftPartialHashExists(leftHash))
                            {
                                var fileName = Path.Combine(directoryName, $"{bruteText}{fileExtension}");
                                if (MPQPartialHash.TryCalculate(fileName, MPQPartialHash.RIGHT_OFFSET, out var rightHash) && archive.RightPartialHashExists(rightHash))
                                {
                                    lock (foundFileLock)
                                    {
                                        if (ExtractFileFromArchive(archive, fileName))
                                        {
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

        protected List<HashSet<string>> _alternateUnknownRecoveryFileNames = new List<HashSet<string>>()
        {
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".w3m", ".w3x", ".mpq", ".pud" },
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".txt", ".ini" },
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".j", ".lua" },
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".wav", ".mp3", ".flac", ".aif", ".aiff", ".ogg" },
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
                var withoutPrefix = Path.GetFileName(potentialFileName);
                if (withoutPrefix.StartsWith("DISPAS", StringComparison.InvariantCultureIgnoreCase) || withoutPrefix.StartsWith("DISDIS", StringComparison.InvariantCultureIgnoreCase))
                {
                    withoutPrefix = withoutPrefix.Substring(6);
                }
                else if (withoutPrefix.StartsWith("DIS", StringComparison.InvariantCultureIgnoreCase) || withoutPrefix.StartsWith("PAS", StringComparison.InvariantCultureIgnoreCase))
                {
                    withoutPrefix = withoutPrefix.Substring(3);
                }

                result.Add($"DIS{withoutPrefix}");
                result.Add($"PAS{withoutPrefix}");
                result.Add($"DISDIS{withoutPrefix}");
                result.Add($"DISPAS{withoutPrefix}");
            }
            if (potentialFileName.EndsWith(".mdl", StringComparison.InvariantCultureIgnoreCase) || potentialFileName.EndsWith(".mdx", StringComparison.InvariantCultureIgnoreCase))
            {
                var directory = Path.GetDirectoryName(potentialFileName) ?? "";
                var withoutExtensionOrSuffix = Path.GetFileNameWithoutExtension(potentialFileName);
                if (withoutExtensionOrSuffix.EndsWith("_PORTRAIT", StringComparison.InvariantCultureIgnoreCase))
                {
                    withoutExtensionOrSuffix = withoutExtensionOrSuffix.Substring(0, withoutExtensionOrSuffix.Length - 9);
                }
                result.Add($"{Path.Combine(directory, withoutExtensionOrSuffix)}_PORTRAIT{Path.GetExtension(potentialFileName)}");
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

        protected void DiscoverUnknownFileNames_DeepScan(StormMPQArchive archive, List<string> baseFileNames, List<string> directories)
        {
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
                                if (ExtractFileFromArchive(archive, fullFileName))
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

        [GeneratedRegex(@"\$[0-9a-f]{8}", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ScriptHexObfuscatedFourCC();
        [GeneratedRegex(@"'([^']{4})'\s*\+\s*'([^']{4})'", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ScriptMathObfuscatedFourCC();

        protected string DeObfuscateFourCC(Map map_ObjectDataOnly, string script, string prefix, string suffix)
        {
            var result = Regex_ScriptHexObfuscatedFourCC().Replace(script, x =>
            {
                var intValue = Convert.ToInt32(x.Value.Substring(1), 16);
                var rawCode = intValue.ToFourCC();
                if (map_ObjectDataOnly?.GetObjectDataTypeForID(rawCode) == null || rawCode.Length != 4 || rawCode.Any(x => x < ' ' || x > '~'))
                {
                    return intValue.ToString();
                }
                return $"{prefix}{rawCode}{suffix}";
            });

            result = Regex_ScriptMathObfuscatedFourCC().Replace(result, x =>
            {
                var left = x.Groups[1].Value;
                var right = x.Groups[2].Value;
                var intValue = left.FromFourCCToInt() + right.FromFourCCToInt();
                var rawCode = intValue.ToFourCC();
                if (map_ObjectDataOnly?.GetObjectDataTypeForID(rawCode) == null || rawCode.Length != 4 || rawCode.Any(x => x < ' ' || x > '~'))
                {
                    return intValue.ToString();
                }
                return $"{prefix}{rawCode}{suffix}";
            });

            if (script != result)
            {
                _deprotectionResult.CountOfProtectionsFound++;
                _logEvent("FourCC codes de-obfuscated");
            }

            return result;
        }

        protected string DeObfuscateFourCCJass(Map map_ObjectDataOnly, string jassScript)
        {
            return DeObfuscateFourCC(map_ObjectDataOnly, jassScript, "'", "'");
        }

        protected string DeObfuscateFourCCLua(Map map_ObjectDataOnly, string jassScript)
        {
            return DeObfuscateFourCC(map_ObjectDataOnly, jassScript, "FourCC(\"", "\")");
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
                else if (x is JassIfStatementSyntax ifStatement)
                {
                    return ifStatement.Body.Statements;
                }
                else if (x is JassElseIfClauseSyntax elseIfClause)
                {
                    return elseIfClause.Body.Statements;
                }
                else if (x is JassElseClauseSyntax elseClause)
                {
                    return elseClause.Body.Statements;
                }
                else if (x is JassLoopStatementSyntax loop)
                {
                    return loop.Body.Statements;
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

        protected void SplitUserDefinedAndAutoGeneratedGlobalVariableNames(string jassScript, out List<string> userDefinedGlobals, out List<string> globalGenerateds)
        {
            var jassParsed = new IndexedJassCompilationUnitSyntax(ParseJassScript(jassScript));

            var globals = jassParsed.CompilationUnit.Declarations.OfType<JassGlobalDeclarationListSyntax>().SelectMany(x => x.Globals).Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>().ToList();

            var probablyUserDefined = new HashSet<string>();
            var mainStatements = ExtractStatements_IncludingEnteringFunctionCalls(jassParsed, "main", out var mainInlinedFunctions);
            var initBlizzardIndex = mainStatements.FindIndex(x => x is JassCallStatementSyntax callStatement && string.Equals(callStatement.IdentifierName.Name, "InitBlizzard", StringComparison.InvariantCultureIgnoreCase));

            //NOTE: Searches for contents of InitGlobals() by structure so it can still work on obfuscated code
            var idx = initBlizzardIndex;
            var initGlobalsIndex = initBlizzardIndex+1;
            var currentStreakStartIdx = initGlobalsIndex;
            var longestStreak = 0;
            foreach (var statement in mainStatements.Skip(initBlizzardIndex))
            {
                idx++;

                var isPossibleInitGlobalStatement = statement is JassLoopStatementSyntax || statement is JassExitStatementSyntax || (statement is JassSetStatementSyntax setStatement && !setStatement.Value.ToString().Contains("CreateTrigger"));
                if (!isPossibleInitGlobalStatement)
                {
                    currentStreakStartIdx = idx;
                }
                else if (idx - currentStreakStartIdx > longestStreak)
                {
                    longestStreak = idx - currentStreakStartIdx;
                    initGlobalsIndex = currentStreakStartIdx;
                }
            }

            var initGlobalsStatements = mainStatements.GetRange(initBlizzardIndex + 1, initGlobalsIndex + longestStreak - initBlizzardIndex - 1);
            foreach (var udgStatement in initGlobalsStatements)
            {
                probablyUserDefined.AddRange(War3NetExtensions.GetAllChildSyntaxNodes_Recursive(udgStatement).OfType<JassIdentifierNameSyntax>().Select(x => x.Name));
            }

            userDefinedGlobals = new List<string>();
            globalGenerateds = new List<string>();
            var possiblyAutoGeneratedTypes = new HashSet<string>() { "rect", "camerasetup", "sound", "unit", "destructable", "item", "trigger" };
            foreach (var global in globals)
            {
                var variableName = global.Declarator.IdentifierName.Name;
                var variableType = global.Declarator.Type.TypeName.Name;
                if (global.Declarator is JassArrayDeclaratorSyntax || probablyUserDefined.Contains(variableName) || !possiblyAutoGeneratedTypes.Contains(variableType))
                {
                    userDefinedGlobals.Add(variableName);
                }
                else
                {
                    globalGenerateds.Add(variableName);
                }
            }
        }

        [GeneratedRegex("^", RegexOptions.Multiline)]
        protected static partial Regex Regex_StartOfAllLines();

        [GeneratedRegex(@"^[0-9a-z]{4}$", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ScriptFourCC();

        [GeneratedRegex(@"^\s*function\s+(\w+)\s+takes", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_JassFunctionDeclaration();

        [GeneratedRegex(@"^\s*call\s+(\w+)\s*\(", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_JassFunctionCall();

        [GeneratedRegex(@"set\s+(\w+)\s*=\s*0[\r\n\s]+loop[\r\n\s]+exitwhen\s*\(\1\s*>\s*([0-9]+)\)[\r\n\s]+set\s+(\w+)\[\1\]\s*=\s*[^\r\n]*[\r\n\s]+set\s+\1\s*=\s*\1\s*\+\s*1[\r\n\s]+endloop[\r\n\s]+", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
        protected static partial Regex Regex_GetArraySize();

        protected ScriptMetaData DecompileJassScriptMetaData_Internal(string jassScript, string editorSpecificJassScript)
        {
            var result = new ScriptMetaData();
            result.CustomTextTriggers = new MapCustomTextTriggers(MapCustomTextTriggersFormatVersion.v1, MapCustomTextTriggersSubVersion.v4) { GlobalCustomScriptCode = new CustomTextTrigger() { Code = "" }, GlobalCustomScriptComment = "Deprotected global non-GUI custom script extracted from war3map.j. This may have compiler errors that need to be resolved manually. Editor-generated functions may be duplicated. If saving in world editor causes game to become corrupted, check the duplicate functions to determine the old version and comment it out (then test if any of it needs to be uncommented and moved to an initialization script)" };

            var map = DecompileMap();
            result.Info = map.Info;

            result.TriggerStrings = map.TriggerStrings;

            var mapInfoFormatVersion = Enum.GetValues(typeof(MapInfoFormatVersion)).Cast<MapInfoFormatVersion>().OrderBy(x => x == map?.Info?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault();
            if (result.Sounds != null && result.Cameras != null && result.Regions != null && result.Triggers != null && result.Units != null)
            {
                _logEvent("Decompiling script finished");
            }

            var useNewFormat = map.Info.FormatVersion >= MapInfoFormatVersion.v28;

            JassScriptDecompiler jassScriptDecompiler;
            try
            {
                _logEvent("Decompiling war3map script file");
                var tempMap = map.Clone_Shallow();
                tempMap.Script = editorSpecificJassScript;
                tempMap.Info = new MapInfo(mapInfoFormatVersion) { ScriptLanguage = ScriptLanguage.Jass };
                tempMap.ConcatObjectData(_defaultSLKData);
                jassScriptDecompiler = new JassScriptDecompiler(tempMap);
            }
            catch
            {
                return null;
            }

            if (result.Sounds == null || (result.Sounds.Sounds.Count == 1 && result.Sounds?.Sounds[0] != result.Sounds?.Sounds[0]))
            {
                _logEvent("Decompiling map sounds");
                try
                {
                    if (jassScriptDecompiler.TryDecompileMapSounds(Enum.GetValues(typeof(MapSoundsFormatVersion)).Cast<MapSoundsFormatVersion>().OrderBy(x => x == map?.Sounds?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(), out var sounds))
                    {
                        _logEvent("map sounds recovered");
                        result.Sounds = sounds;
                    }
                }
                catch { }
            }

            if (result.Cameras == null)
            {
                _logEvent("Decompiling map cameras");
                try
                {
                    if (jassScriptDecompiler.TryDecompileMapCameras(Enum.GetValues(typeof(MapCamerasFormatVersion)).Cast<MapCamerasFormatVersion>().OrderBy(x => x == map?.Cameras?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(), useNewFormat, out var cameras))
                    {                          
                        _logEvent("map cameras recovered");
                        result.Cameras = cameras;
                    }
                }
                catch { }
            }

            if (result.Regions == null)
            {
                _logEvent("Decompiling map regions");
                try
                {
                    if (jassScriptDecompiler.TryDecompileMapRegions(Enum.GetValues(typeof(MapRegionsFormatVersion)).Cast<MapRegionsFormatVersion>().OrderBy(x => x == map?.Regions?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(), out var regions))
                    {
                        _logEvent("map regions recovered");
                        result.Regions = regions;
                    }
                }
                catch { }
            }

            if (result.Triggers == null && !Settings.CreateVisualTriggers)
            {
                var triggerItemIdx = 0;
                var rootCategoryItemIdx = triggerItemIdx++;
                var triggersCategoryItemIdx = triggerItemIdx++;

                result.Triggers = new MapTriggers(MapTriggersFormatVersion.v7, MapTriggersSubVersion.v4) { GameVersion = 2 };
                result.Triggers.TriggerItems.Add(new TriggerCategoryDefinition(TriggerItemType.RootCategory) { Id = rootCategoryItemIdx, ParentId = -1, Name = "script.w3x" });

                var lines = jassScript.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

                var functionDeclarations = lines.Select((x, y) => new { lineIdx = y, match = Regex_JassFunctionDeclaration().Match(x) }).Where(x => x.match.Success).GroupBy(x => x.match.Groups[1].Value).ToDictionary(x => x.Key, x => x.ToList());
                var functionCalls = lines.Select((x, y) => new { lineIdx = y, match = Regex_JassFunctionCall().Match(x) }).Where(x => x.match.Success).GroupBy(x => x.match.Groups[1].Value).ToDictionary(x => x.Key, x => x.ToList());
                var functions = functionDeclarations.Keys.Concat(functionCalls.Keys).ToHashSet();

                //todo: decompile "CreateAllDestructables", "InitTechTree"
                var oldNativeEditorFunctionsToExecute = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "main", "InitGlobals", "InitCustomTriggers", "RunInitializationTriggers", "InitTechTree" };
                var nativeEditorFunctionIndexes = new Dictionary<string, Tuple<int, int>>();
                var nativeEditorFunctionsRenamed = new Dictionary<string, string>();
                foreach (var nativeEditorFunction in _nativeEditorFunctions)
                {
                    var renamed = nativeEditorFunction;
                    do
                    {
                        renamed += "_old";
                    } while (functions.Contains(renamed));

                    nativeEditorFunctionsRenamed[nativeEditorFunction] = renamed;

                    var executeFunction = oldNativeEditorFunctionsToExecute.Contains(nativeEditorFunction);
                    if (functionDeclarations.TryGetValue(nativeEditorFunction, out var declarationMatches))
                    {
                        foreach (var declaration in declarationMatches)
                        {
                            lines[declaration.lineIdx] = lines[declaration.lineIdx].Replace(nativeEditorFunction, renamed);

                            if (!executeFunction)
                            {
                                var idx = declaration.lineIdx;
                                while (true)
                                {
                                    var isEndFunction = lines[idx].Trim().StartsWith("endfunction");
                                    lines[idx] = "// " + lines[idx];
                                    if (isEndFunction)
                                    {
                                        break;
                                    }
                                    idx++;
                                }
                            }
                        }
                    }

                    if (functionCalls.TryGetValue(nativeEditorFunction, out var callMatches))
                    {
                        foreach (var call in callMatches)
                        {
                            if (executeFunction)
                            {
                                lines[call.lineIdx] = lines[call.lineIdx].Replace(nativeEditorFunction, renamed);
                            }
                            else
                            {
                                lines[call.lineIdx] = "// " + lines[call.lineIdx];
                            }
                        }
                    }
                }

                var startGlobalsLineIdx = lines.FindIndex(x => x.Trim() == "globals");
                var endGlobalsLineIdx = lines.FindIndex(x => x.Trim() == "endglobals");
                var globalLines = lines.Skip(startGlobalsLineIdx + 1).Take(endGlobalsLineIdx - startGlobalsLineIdx - 1).ToArray();
                var userGlobalLines = new List<string>();
                if (startGlobalsLineIdx != -1)
                {
                    foreach (var globalLine in globalLines)
                    {
                        bool userGenerated = true;

                        var match = Regex_ParseJassVariableDeclaration().Match(globalLine);
                        if (match.Success)
                        {
                            var name = (match.Groups[4].Value ?? "").Trim();
                            if (name.StartsWith("gg_", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (name.StartsWith("gg_trg_"))
                                {
                                    var editorName = name.Substring(3);
                                    var userGeneratedName = "udg_" + editorName;
                                    jassScript = jassScript.Replace(name, userGeneratedName);
                                }
                                else
                                {
                                    userGenerated = false;
                                }
                            }
                            else if (!name.StartsWith("udg_", StringComparison.InvariantCultureIgnoreCase))
                            {
                                DebugSettings.Warn("Unknown variable prefix");
                            }
                        }
                        else
                        {
                            DebugSettings.Warn("Unable to parse variable declaration");
                        }

                        if (userGenerated)
                        {
                            userGlobalLines.Add(globalLine);
                        }
                    }
                }

                lines.RemoveRange(startGlobalsLineIdx, endGlobalsLineIdx - startGlobalsLineIdx + 1);
                lines.InsertRange(startGlobalsLineIdx, userGlobalLines);
                lines.Insert(startGlobalsLineIdx, "globals");
                lines.Insert(startGlobalsLineIdx, "//If you get compiler errors, Ensure vJASS is enabled");
                lines.Insert(startGlobalsLineIdx + userGlobalLines.Count + 2, "endglobals");
                jassScript = new StringBuilder().AppendJoin("\r\n", lines.ToArray()).ToString();
                result.CustomTextTriggers = new MapCustomTextTriggers(MapCustomTextTriggersFormatVersion.v1, MapCustomTextTriggersSubVersion.v4) { GlobalCustomScriptCode = new CustomTextTrigger() { Code = jassScript.Replace("%", "%%") }, GlobalCustomScriptComment = "Deprotected global script. Please ensure JassHelper:EnableJassHelper and JassHelper:EnableVJass settings are turned on. This may have compiler errors that need to be resolved manually. Editor-generated functions have been renamed with a suffix of _old. If saving in world editor causes game to become corrupted, check the _old functions to find code that may need to be moved to an initialization script." };
                result.Triggers.TriggerItems.Add(new TriggerCategoryDefinition() { Id = triggersCategoryItemIdx, ParentId = rootCategoryItemIdx, Name = "Triggers", IsExpanded = true });
                var mainRenamed = nativeEditorFunctionsRenamed["main"];
                result.Triggers.TriggerItems.Add(new TriggerDefinition() { Id = triggerItemIdx++, ParentId = triggersCategoryItemIdx, Name = "MainDeprotected", Functions = new List<TriggerFunction>() { new TriggerFunction() { IsEnabled = true, Type = TriggerFunctionType.Event, Name = "MapInitializationEvent" }, new TriggerFunction() { IsEnabled = true, Type = TriggerFunctionType.Action, Name = "CustomScriptCode", Parameters = new List<TriggerFunctionParameter>() { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = $"call {mainRenamed}()" } } } }, IsInitiallyOn = true, IsEnabled = true, RunOnMapInit = true, Description = $"Call {mainRenamed} which was extracted from protected map in case it had extra code that failed to decompile into GUI" });
            }

            if (result.Triggers == null && Settings.CreateVisualTriggers)
            {
                _logEvent("Decompiling map triggers");
                try
                {
                    if (jassScriptDecompiler.TryDecompileMapTriggers(Enum.GetValues(typeof(MapTriggersFormatVersion)).Cast<MapTriggersFormatVersion>().OrderBy(x => x == map?.Triggers?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(), Enum.GetValues(typeof(MapTriggersSubVersion)).Cast<MapTriggersSubVersion?>().Concat(new[] { (MapTriggersSubVersion?)null }).OrderBy(x => x == map?.Triggers?.SubVersion ? 0 : 1).ThenByDescending(x => x == null ? int.MaxValue : (int)x).FirstOrDefault(), out var triggers))
                    {
                        var triggerDefinitions = triggers.TriggerItems.OfType<TriggerDefinition>().ToList();
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
                                        if (Regex_ScriptFourCC().IsMatch(stringValue) && map.GetObjectDataTypeForID(stringValue) != null)
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

                            var customScriptFunctions = triggerDefinitions.SelectMany(x => x.Functions).SelectMany(x => x.RecurseNestedTriggerFunctions()).Where(x => x.Name == "CustomScriptCode").SelectMany(x => jassScriptDecompiler.Context.FunctionDeclarations.ParseScriptForNestedFunctionCalls(x.Parameters[0].Value)).ToHashSet();
                            var nonRecursedFunctions = customScriptFunctions.ToList();
                            do
                            {
                                foreach (var function in nonRecursedFunctions.ToList())
                                {
                                    nonRecursedFunctions.Remove(function);

                                    var nestedFunctions = jassScriptDecompiler.Context.FunctionDeclarations.ParseScriptForNestedFunctionCalls(function.RenderFunctionAsString());
                                    foreach (var nestedFunction in nestedFunctions)
                                    {
                                        if (!customScriptFunctions.Contains(nestedFunction))
                                        {
                                            customScriptFunctions.Add(nestedFunction);
                                            nonRecursedFunctions.Add(nestedFunction);
                                        }
                                    }
                                }
                            } while (nonRecursedFunctions.Count > 0);

                            var compilationUnit = jassScriptDecompiler.Context.CompilationUnit;
                            if (JassSyntaxFactory.TryParseCompilationUnit(jassScript, out var originalCompilationUnit))
                            {
                                compilationUnit = originalCompilationUnit;
                            }

                            var sortedFunctionDeclarations = compilationUnit.Declarations.OfType<JassFunctionDeclarationSyntax>().OrderBy(x => compilationUnit.Declarations.IndexOf(x)).ToList();

                            var triggerNames = triggerDefinitions.Select(x => x.Name).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                            var autoGeneratedTriggerFunctions = sortedFunctionDeclarations.Where(x =>
                            {
                                var name = x.FunctionDeclarator.IdentifierName.Name;
                                name = name.TrimStart("Init");
                                name = name.TrimStart("Trig_");
                                name = name.TrimEnd("_Actions");
                                name = name.TrimEnd("_Conditions");
                                var funcIndex = name.LastIndexOf("_Func");
                                if (funcIndex != -1)
                                {
                                    name = name.Substring(0, funcIndex);
                                }
                                name = name.Replace("_", " ");
                                return triggerNames.Contains(name);
                            }).ToList();

                            var customScriptFunctionNames = customScriptFunctions.Select(x => x.FunctionDeclaration.FunctionDeclarator.IdentifierName.Name).ToHashSet();
                            var nonGuiScripts = new StringBuilder();
                            foreach (var function in sortedFunctionDeclarations)
                            {
                                var script = function.RenderFunctionAsString();
                                var functionName = function.FunctionDeclarator.IdentifierName.Name;
                                if (_nativeEditorFunctions.Contains(functionName) || (autoGeneratedTriggerFunctions.Contains(function) && !customScriptFunctionNames.Contains(functionName)))
                                {
                                    script = string.Join("\r\n", script.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Select(x => "// " + x));
                                }
                                nonGuiScripts.Append(script);
                                nonGuiScripts.AppendLine();
                                nonGuiScripts.AppendLine();
                            }


                            _logEvent("map triggers recovered");
                            result.Triggers = triggers;
                            result.CustomTextTriggers.GlobalCustomScriptCode.Code = nonGuiScripts.ToString();
                        }
                    }
                }
                catch { }

                if (result.Triggers != null)
                {
                    var sortedTriggerItems = result.Triggers.TriggerItems.OrderBy(x => x.Id).ToList();
                    result.Triggers.TriggerItems.Clear();
                    result.Triggers.TriggerItems.AddRange(sortedTriggerItems);

                    foreach (var triggerItem in result.Triggers.TriggerItems)
                    {
                        if (triggerItem is TriggerDefinition trigger)
                        {
                            var tooLong = trigger.Functions.Where(x => x.ToString().Length >= 1023).ToList();
                            foreach (var function in tooLong)
                            {
                                var index = trigger.Functions.IndexOf(function);
                                trigger.Functions.RemoveAt(index);
                                trigger.Functions.Insert(index++, new TriggerFunction() { IsEnabled = true, Name = "CustomScriptCode", Parameters = new List<TriggerFunctionParameter>() { new TriggerFunctionParameter() { Value = "** WAR3MAP.J LINE TOO LONG TO IMPORT INTO GUI MUST BE RE-CODED BY HAND **", Type = TriggerFunctionParameterType.String } }, Type = TriggerFunctionType.Action, ChildFunctions = new List<TriggerFunction>() });
                                var chunks = function.ToString().Chunk(1020).Select(x => new string(x)).ToList();
                                foreach (var chunk in chunks)
                                {
                                    trigger.Functions.Insert(index++, new TriggerFunction() { IsEnabled = true, Name = "CustomScriptCode", Parameters = new List<TriggerFunctionParameter>() { new TriggerFunctionParameter() { Value = $"// {chunk}", Type = TriggerFunctionParameterType.String } }, Type = TriggerFunctionType.Action, ChildFunctions = new List<TriggerFunction>() });
                                }

                                /*
                                //Todo: Fix this, causing editor to crash while opening
                                var GUI_MAX_LINE_LENGTH = 255;
                                var allFunctions = trigger.Functions.SelectMany(x => x.RecurseNestedTriggerFunctions()).ToList();
                                var tooLong = allFunctions.Where(x => x.ToString().Length >= GUI_MAX_LINE_LENGTH).ToList();
                                foreach (var function in tooLong)
                                {
                                    var replacementFunction = new TriggerFunction() { IsEnabled = true, Name = "CustomScriptCode", Parameters = new List<TriggerFunctionParameter>() { new TriggerFunctionParameter() { Value = "** WAR3MAP.J LINE TOO LONG TO IMPORT INTO GUI MUST BE RE-CODED BY HAND **", Type = TriggerFunctionParameterType.String } }, Type = TriggerFunctionType.Action, ChildFunctions = new List<TriggerFunction>() };
                                    var chunks = function.ToString().Chunk(GUI_MAX_LINE_LENGTH - 20).Select(x => new string(x)).Select(x => new TriggerFunction() { IsEnabled = true, Name = "CustomScriptCode", Parameters = new List<TriggerFunctionParameter>() { new TriggerFunctionParameter() { Value = $"// {x}", Type = TriggerFunctionParameterType.String } }, Type = TriggerFunctionType.Action, ChildFunctions = new List<TriggerFunction>() }).ToList();

                                    var childFunctions = trigger.Functions.Contains(function) ? trigger.Functions : allFunctions.Where(x => x.ChildFunctions.Contains(function)).Select(x => x.ChildFunctions).FirstOrDefault();
                                    if (childFunctions != null)
                                    {
                                        var index = childFunctions.IndexOf(function);
                                        childFunctions.RemoveAt(index);
                                        childFunctions.Insert(index++, replacementFunction);
                                        foreach (var chunk in chunks)
                                        {
                                            childFunctions.Insert(index++, chunk);
                                        }
                                    }
                                    else
                                    {
                                        var parameters = allFunctions.Where(x => x.Parameters.Any(y => y.Function == function)).Select(x => x.Parameters).FirstOrDefault();
                                        if (parameters != null)
                                        {
                                            var index = parameters.IndexOf(x => x.Function == function);
                                            parameters.RemoveAt(index);
                                            parameters.Insert(index++, new TriggerFunctionParameter() { Function = replacementFunction, Type = TriggerFunctionParameterType.Function });
                                            foreach (var chunk in chunks)
                                            {
                                                parameters.Insert(index++, new TriggerFunctionParameter() { Function = chunk, Type = TriggerFunctionParameterType.Function });
                                            }
                                        }
                                    }
                                }
                                */
                            }
                        }
                    }

                    foreach (var variable in result.Triggers.Variables)
                    {
                        if ((variable.Type ?? "").Trim().Equals("integer", StringComparison.InvariantCultureIgnoreCase) && variable.InitialValue != "" && !int.TryParse(variable.InitialValue, out var _))
                        {
                            var fromRawCode = variable.InitialValue.FromRawcode().ToString();
                            if (fromRawCode == "0")
                            {
                                continue;
                            }

                            var slkType = map.GetObjectDataTypeForID(variable.InitialValue);
                            switch (slkType)
                            {
                                case null:
                                    variable.InitialValue = fromRawCode;
                                    break;
                                case ObjectDataType.Ability:
                                    variable.Type = "abilcode";
                                    break;
                                case ObjectDataType.Buff:
                                    variable.Type = "buffcode";
                                    break;
                                case ObjectDataType.Destructable:
                                    variable.Type = "destructablecode";
                                    break;
                                case ObjectDataType.Doodad:
                                    variable.InitialValue = fromRawCode;
                                    break;
                                case ObjectDataType.Item:
                                    variable.Type = "itemcode";
                                    break;
                                case ObjectDataType.Unit:
                                    variable.Type = "unitcode";
                                    break;
                                case ObjectDataType.Upgrade:
                                    variable.InitialValue = fromRawCode;
                                    break;
                            }
                        }
                    }

                    bool changed;
                    do
                    {
                        changed = false;

                        foreach (var variable in result.Triggers.Variables)
                        {
                            if (variable.IsInitialized && !string.IsNullOrWhiteSpace(variable.InitialValue))
                            {
                                var otherVariable = result.Triggers.Variables.FirstOrDefault(x => x.Name == variable.InitialValue) ?? result.Triggers.Variables.FirstOrDefault(x => x.Name == $"udg_{variable.InitialValue}");
                                if (otherVariable != null)
                                {
                                    if (!otherVariable.IsInitialized || variable.Name == otherVariable.InitialValue || variable.Name == $"udg_{otherVariable.InitialValue}")
                                    {
                                        variable.IsInitialized = false;
                                        variable.InitialValue = "";
                                    }
                                    else
                                    {
                                        variable.InitialValue = otherVariable.InitialValue;
                                    }

                                    changed = true;
                                    break;
                                }
                            }
                        }
                    } while (changed);
                }
            }

            if (result.Units == null)
            {
                _logEvent("Decompiling map units");
                try
                {
                    if (jassScriptDecompiler.TryDecompileMapUnits(Enum.GetValues(typeof(MapWidgetsFormatVersion)).Cast<MapWidgetsFormatVersion>().OrderBy(x => x == map?.Doodads?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(), Enum.GetValues(typeof(MapWidgetsSubVersion)).Cast<MapWidgetsSubVersion>().OrderBy(x => x == map?.Doodads?.SubVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(), useNewFormat, out var units, out var unitsDecompiledFromVariableName) && units?.Units?.Any() == true)
                    {
                        result.Units = units;
                        result.UnitDecompilationMetaData = unitsDecompiledFromVariableName;

                        _logEvent("map units recovered");
                        if (!result.Units.Units.Any(x => x.TypeId != "sloc".FromRawcode()))
                        {
                            _deprotectionResult.WarningMessages.Add("WARNING: Only unit start locations could be recovered. Map will still open in WorldEditor & run, but units will not be visible in WorldEditor rendering and saving in world editor will corrupt your war3map.j or war3map.lua script file.");
                        }
                    }
                }
                catch { }
            }

            if (result.UnitDecompilationMetaData?.Any() == true)
            {
                result.Triggers ??= new MapTriggers(MapTriggersFormatVersion.v7, MapTriggersSubVersion.v4) { GameVersion = 2 };
                var maxTriggerItemId = result.Triggers.TriggerItems?.Any() == true ? result.Triggers.TriggerItems.Select(x => x.Id).Max()+1 : 0;
                var rootCategory = result.Triggers.TriggerItems.FirstOrDefault(x => x.Type == TriggerItemType.RootCategory);
                if (rootCategory == null)
                {
                    rootCategory = new TriggerCategoryDefinition(TriggerItemType.RootCategory) { Id = maxTriggerItemId++, ParentId = -1, Name = "script.w3x" };
                    result.Triggers.TriggerItems.Add(rootCategory);
                }
                var category = result.Triggers.TriggerItems.FirstOrDefault(x => x.Type == TriggerItemType.Category);
                if (category == null)
                {
                    category = new TriggerCategoryDefinition(TriggerItemType.Category) { Id = maxTriggerItemId++, ParentId = rootCategory.Id, Name = "Deprotect ObjectManager Variables", IsExpanded = true };
                    result.Triggers.TriggerItems.Add(category);
                }

                result.Destructables = new List<string>();
                var emptyVariableTrigger = new TriggerDefinition() { Description = "Disabled GUI trigger with fake code, just to convert ObjectManager units/items/cameras to global generated variables", Name = "GlobalGeneratedObjectManagerVariables", ParentId = category.Id, IsEnabled = true, IsInitiallyOn = false };
                var variables = (result.Units?.Units?.Select(x => x.GetVariableName_BugFixPendingPR()).ToList() ?? new List<string>()).Concat(result.Cameras?.Cameras?.Select(x => x.GetVariableName()).ToList() ?? new List<string>()).Concat(map.Doodads?.Doodads.Select(x => x.GetVariableName()).ToList() ?? new List<string>()).ToList();
                foreach (var variable in variables)
                {
                    var isUnit = variable.StartsWith("gg_unit_");
                    var isItem = variable.StartsWith("gg_item_");
                    var isDestructable = variable.StartsWith("gg_dest_");

                    var jassVariableSearchString = isUnit || isItem ? variable.Substring(0, variable.Length - 5) : variable; // Removes _#### (CreationNumber) suffix since it changes after deprotection & having extra variables won't break anything

                    if (!jassScript.Contains(jassVariableSearchString))
                    {
                        continue;
                    }

                    if (isUnit)
                    {
                        var triggerFunction = new TriggerFunction() { Name = "ResetUnitAnimation", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                    }
                    else if (isDestructable)
                    {
                        var triggerFunction = new TriggerFunction() { Name = "SetDestAnimationSpeedPercent", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = "100" } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                        result.Destructables.Add(variable);
                    }
                    else if (isItem)
                    {
                        var triggerFunction = new TriggerFunction() { Name = "UnitDropItemSlotBJ", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Function, Value = "GetLastCreatedUnit", Function = new TriggerFunction() { Name = "GetLastCreatedUnit", Type = TriggerFunctionType.Call, IsEnabled = true } } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = "1" } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                    }
                    else if (variable.StartsWith("gg_cam_"))
                    {
                        var triggerFunction = new TriggerFunction() { Name = "BlzCameraSetupGetLabel", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                    }
                }
                result.Triggers.TriggerItems.Add(emptyVariableTrigger);
            }

            if (result.Units != null && result.UnitDecompilationMetaData != null && result.Regions != null)
            {
                foreach (var unit in result.Units.Units)
                {
                    if (result.UnitDecompilationMetaData.TryGetValue(unit, out var decompilationMetaData))
                    {
                        var region = result.Regions.Regions.FirstOrDefault(x => x.GetVariableName() == decompilationMetaData.WaygateDestinationRegionName);
                        if (region != null)
                        {
                            unit.WaygateDestinationRegionId = region.CreationNumber;
                        }
                    }
                }
            }

            return result;
        }

        protected ScriptMetaData DecompileJassScriptMetaData(string jassScript)
        {
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
            var allInlinedCodeFromConfigAndMain = statements.ToImmutableArray();

            var inlined = new IndexedJassCompilationUnitSyntax(InlineJassFunctions(jassParsed.CompilationUnit, new HashSet<string>(configInlinedFunctions.Concat(mainInlinedFunctions))));

            var commentedNativeFunctions = new List<JassFunctionDeclarationSyntax>();
            string commentScript;
            using (var scriptWriter = new StringWriter())
            {
                var renderer = new JassRenderer(scriptWriter);
                foreach (var nativeEditorFunction in _nativeEditorFunctions)
                {
                    if (inlined.IndexedFunctions.TryGetValue(nativeEditorFunction, out var functionDeclaration))
                    {
                        commentedNativeFunctions.Add(functionDeclaration);
                        renderer.Render(functionDeclaration);
                        renderer.RenderNewLine();
                        renderer.RenderNewLine();
                    }
                }
                commentScript = scriptWriter.GetStringBuilder().ToString();
            }
            if (!string.IsNullOrWhiteSpace(commentScript))
            {
                commentScript = "//OLD Auto-Generated Native Editor Functions. Review for any lost code which may have occurred during deprotection. \r\n" + Regex_StartOfAllLines().Replace(commentScript, "// ");
            }

            var newDeclarations = inlined.CompilationUnit.Declarations.Except(commentedNativeFunctions).ToList();
            foreach (var nativeEditorFunction in _nativeEditorFunctions)
            {
                newDeclarations.Add(new JassFunctionDeclarationSyntax(new JassFunctionDeclaratorSyntax(new JassIdentifierNameSyntax(nativeEditorFunction), JassParameterListSyntax.Empty, JassTypeSyntax.Nothing), new JassStatementListSyntax(allInlinedCodeFromConfigAndMain)));
            }

            var editorSpecificCompilationUnit = new JassCompilationUnitSyntax(newDeclarations.ToImmutableArray());
            var editorSpecificJassScript = editorSpecificCompilationUnit.RenderScriptAsString();

            var firstPass = DecompileJassScriptMetaData_Internal(jassScript, editorSpecificJassScript);

            if (firstPass == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(commentScript))
            {
                firstPass.CustomTextTriggers.GlobalCustomScriptCode.Code += commentScript;
            }

            var correctedUnitVariableNames = new List<KeyValuePair<string, string>>();

            var globalGenerateds = jassParsed.CompilationUnit.Declarations.OfType<JassGlobalDeclarationListSyntax>().SelectMany(x => x.Globals).Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>().Select(x => x.Declarator.IdentifierName.Name).Where(x => x.StartsWith("gg_")).ToList();
            var decompiled = new List<string>();
            decompiled.AddRange(firstPass.Cameras?.Cameras?.Select(x => $"gg_cam_{x.Name}")?.ToList() ?? new List<string>());
            decompiled.AddRange(firstPass.Regions?.Regions?.Select(x => $"gg_rct_{x.Name}")?.ToList() ?? new List<string>());
            decompiled.AddRange(firstPass.Triggers?.TriggerItems?.OfType<TriggerDefinition>()?.Select(x => $"gg_trg_{x.Name}")?.ToList() ?? new List<string>());
            decompiled.AddRange(firstPass.Sounds?.Sounds?.Select(x => x.Name)?.ToList() ?? new List<string>()); //NOTE: War3Net doesn't remove gg_snd_ from name for some reason
            decompiled.AddRange(firstPass.Destructables ?? new List<string>());
            decompiled = decompiled.Select(x => x.Replace(" ", "_")).ToList();
            var notDecompiledGlobalGenerateds = globalGenerateds.Except(decompiled).ToList();
            correctedUnitVariableNames.AddRange(notDecompiledGlobalGenerateds.Select(x => new KeyValuePair<string, string>(x, "udg_" + x.Substring("gg_".Length))));
            if (firstPass.UnitDecompilationMetaData != null)
            {
                correctedUnitVariableNames.AddRange(firstPass.UnitDecompilationMetaData.Where(x => x.Value.DecompiledFromVariableName.StartsWith("gg_")).Select(x => new KeyValuePair<string, string>(x.Value.DecompiledFromVariableName, x.Key.GetVariableName_BugFixPendingPR())));
            }

            if (!correctedUnitVariableNames.Any())
            {
                return firstPass;
            }

            // _#### (CreationNumber) suffix changes for everything in ObjectManager during deprotection so we have to rename the variables in script to match
            var renamer = new JassRenamer(new Dictionary<string, JassIdentifierNameSyntax>(), correctedUnitVariableNames.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => new JassIdentifierNameSyntax(x.Last().Value)));
            if (!renamer.TryRenameCompilationUnit(jassParsed.CompilationUnit, out var secondPass) || !renamer.TryRenameCompilationUnit(editorSpecificCompilationUnit, out var editorSpecificSecondPass))
            {
                return firstPass;
            }

            _logEvent("Global generated variables renamed.");
            _logEvent("Starting decompile war3map script 2nd pass.");

            var result = DecompileJassScriptMetaData_Internal(secondPass.RenderScriptAsString(), editorSpecificSecondPass.RenderScriptAsString());
            if (result == null)
            {
                return firstPass;
            }

            if (!string.IsNullOrWhiteSpace(commentScript))
            {
                result.CustomTextTriggers.GlobalCustomScriptCode.Code += commentScript;
            }
            result.UnitDecompilationMetaData = null;
            return result;
        }

        protected ScriptMetaData DecompileLuaScriptMetaData(string luaScript)
        {
            //todo: code this!
            return new ScriptMetaData();
        }

        protected void SetMapFile(Map map, string fileName, bool overwrite = false)
        {
            var bytes = File.ReadAllBytes(fileName);
            if (bytes.Length == 0)
            {
                return;
            }

            foreach (var encoding in _allEncodings)
            {
                try
                {
                    using (var stream = new MemoryStream(bytes))
                    {
                        map.SetFile(Path.GetFileName(fileName), overwrite, stream, encoding, true);
                        break;
                    }
                }
                catch { }
            }
        }

        protected Map DecompileMap(Action<Map> forcedValueOverrides = null)
        {
            //note: the order of operations matters. For example, script file import fails if info file not yet imported. So we import each file 2x
            _logEvent("Analyzing map files");
            var mapFiles = Directory.GetFiles(DiscoveredFilesPath, "war3map*", SearchOption.AllDirectories).Union(Directory.GetFiles(DiscoveredFilesPath, "war3campaign*", SearchOption.AllDirectories)).OrderBy(x => string.Equals(x, "war3map.w3i", StringComparison.InvariantCultureIgnoreCase) ? 0 : 1).ToList();

            var map = new Map();
            for (var retry = 0; retry < 2; ++retry)
            {
                if (forcedValueOverrides != null)
                {
                    forcedValueOverrides(map);
                }

                foreach (var fileName in mapFiles)
                {
                    _logEvent($"Analyzing {fileName} ...");
                    SetMapFile(map, fileName);
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
            var functions = compilationUnit.Declarations.OfType<JassFunctionDeclarationSyntax>().GroupBy(x => x.FunctionDeclarator.IdentifierName.Name).ToDictionary(x => x.Key, x => x.First());
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
                v8.Execute(ReadAllText(Path.Combine(ExeFolderPath, "luaparse.js")));
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

        protected string DeObfuscateJassScript(Map map_ObjectDataOnly, string jassScript)
        {
            var result = DeObfuscateFourCCJass(map_ObjectDataOnly, jassScript);

            try
            {
                SplitUserDefinedAndAutoGeneratedGlobalVariableNames(result, out var userDefinedGlobals, out var globalGenerateds);
                var parsed = ParseJassScript(result);
                var formatted = "";
                using (var writer = new StringWriter())
                {
                    var globalVariableRenames = new Dictionary<string, JassIdentifierNameSyntax>();
                    var uniqueNames = new HashSet<string>(parsed.Declarations.OfType<JassGlobalDeclarationListSyntax>().SelectMany(x => x.Globals).Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>().Select(x => x.Declarator.IdentifierName.Name), StringComparer.InvariantCultureIgnoreCase);
                    foreach (var declaration in parsed.Declarations)
                    {
                        if (declaration is JassGlobalDeclarationListSyntax)
                        {
                            var globalDeclaration = (JassGlobalDeclarationListSyntax)declaration;
                            foreach (var global in globalDeclaration.Globals.OfType<JassGlobalDeclarationSyntax>())
                            {
                                var isArray = global.Declarator is JassArrayDeclaratorSyntax;
                                var originalName = global.Declarator.IdentifierName.Name;

                                var typeName = global.Declarator.Type.TypeName.Name;
                                var baseName = originalName;

                                var isGlobalGenerated = globalGenerateds.Contains(baseName);

                                if (baseName.StartsWith("udg_", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    baseName = baseName.Substring(4);
                                    isGlobalGenerated = false;
                                }
                                else if (baseName.StartsWith("gg_", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    baseName = baseName.Substring(3);
                                    isGlobalGenerated = true;
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

                                var uniqueName = $"{newName}";
                                if (originalName != uniqueName)
                                {
                                    var counter = 2;
                                    while (uniqueNames.Contains(uniqueName))
                                    {
                                        counter++;
                                        uniqueName = $"{newName}_{counter}";
                                    }
                                    uniqueNames.Add(uniqueName);
                                    globalVariableRenames[originalName] = new JassIdentifierNameSyntax(uniqueName);
                                }
                            }
                        }
                    }

                    bool renamed;
                    do
                    {
                        renamed = false;
                        var unnecessarySuffixes = uniqueNames.Where(x => x.EndsWith("_1") && !uniqueNames.Contains(x.Substring(0, x.Length - 2) + "_2")).ToList();
                        foreach (var uniqueName in unnecessarySuffixes)
                        {
                            var oldRename = globalVariableRenames.FirstOrDefault(x => string.Equals(x.Value.Name, uniqueName, StringComparison.InvariantCultureIgnoreCase));
                            if (oldRename.Key != null)
                            {
                                var newUniqueName = oldRename.Value.Name.Substring(0, uniqueName.Length - 2);
                                if (!uniqueNames.Contains(newUniqueName))
                                {
                                    uniqueNames.Remove(uniqueName);
                                    uniqueNames.Add(newUniqueName);
                                    globalVariableRenames[oldRename.Key] = new JassIdentifierNameSyntax(newUniqueName);
                                    renamed = true;
                                    break;
                                }
                            }
                        }
                    } while (renamed);

                    var redundantRenames = globalVariableRenames.Where(x => x.Key == x.Value.Name).ToList();
                    foreach (var redundant in redundantRenames)
                    {
                        globalVariableRenames.Remove(redundant.Key);
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

                    result = stringBuilder.ToString();
                }
            }
            catch (Exception e)
            {
                DebugSettings.Warn("Couldn't parse JassScript");
            }

            return result;
        }

        protected string DeObfuscateLuaScript(string luaScript)
        {
            //todo: Code this!
            return luaScript;
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

        [GeneratedRegex(@"\s*(constant\s*)?(\S+)\s+(array\s*)?([^ \t=]+)\s*(=)?\s*(.*)", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ParseJassVariableDeclaration();

        protected void WriteWtg_PlainText_Jass_Old(string jassScript)
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
            foreach (var line in jassScript.Split('\n', StringSplitOptions.RemoveEmptyEntries))
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

            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, "war3map.wtg"), FileMode.Create))
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
            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, "war3map.wct"), FileMode.Create))
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
                MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "(attributes)"));
            }
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "(listfile)")))
            {
                _logEvent("Deleting (listfile)...");
                MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "(listfile)"));
            }
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "(signature)")))
            {
                _logEvent("Deleting (signature)...");
                MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, "(signature)"));
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

        [GeneratedRegex(@"^war3(map|campaign)(skin)?((\.(w[a-zA-Z0-9]{2}|doo|shd|mmp|j|imp))|misc\.txt|\.txt|map\.blp|units\.doo|extra\.txt)$", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_NonImportedNativeEditorFileName();

        protected void BuildImportList()
        {
            _logEvent("Building war3map.imp...");
            var files = Directory.GetFiles(DiscoveredFilesPath, "*", SearchOption.AllDirectories).ToList();
            var newfiles = new List<string>();
            foreach (var file in files.Select(x => RemovePathPrefix(x, DiscoveredFilesPath)))
            {
                if (!Regex_NonImportedNativeEditorFileName().IsMatch(file))
                {
                    newfiles.Add($"{file}\x00");
                }
            }
            newfiles.Add("\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76\x00");
            newfiles = newfiles.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, "war3map.imp"), FileMode.Create))
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

        [GeneratedRegex(@"\\{2,}", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_MultipleSequentialPathSeparators();
        protected List<string> CleanScannedUnknownFileNames(List<string> scannedFileNames)
        {
            var invalidPathChars = new HashSet<char>(Path.GetInvalidPathChars());
            var invalidFileNameChars = new HashSet<char>(Path.GetInvalidFileNameChars());

            HashSet<string> result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            result.AddRange(scannedFileNames.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x =>
            {
                try
                {
                    var withoutExcessSeparators = Regex_MultipleSequentialPathSeparators().Replace(x.Trim(Path.DirectorySeparatorChar), @"\");
                    var withoutInvalidCharacters = new StringBuilder();
                    var path = Path.GetDirectoryName(withoutExcessSeparators);
                    var fileName = Path.GetFileName(withoutExcessSeparators);
                    if (path == null || fileName == null)
                    {
                        return x;
                    }

                    path = path.Trim(Path.DirectorySeparatorChar);
                    foreach (var character in path.Where(x => !invalidPathChars.Contains(x)))
                    {
                        withoutInvalidCharacters.Append(character);
                    }
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        withoutInvalidCharacters.Append(Path.DirectorySeparatorChar);
                    }
                    foreach (var character in fileName.Where(x => !invalidFileNameChars.Contains(x)))
                    {
                        withoutInvalidCharacters.Append(character);
                    }
                    return withoutInvalidCharacters.ToString();
                }
                catch { }

                return x;
            }));

            return result.ToList();
        }

        protected List<string> _objectDataKeysWithFileReferences = new List<string>() { "aaea", "aart", "acat", "aeat", "aefs", "amat", "aord", "arar", "asat", "atat", "auar", "bfil", "bnam", "bptx", "btxf", "dfil", "dptx", "fart", "feat", "fsat", "ftat", "gar1", "ifil", "iico", "ucua", "uico", "umdl", "upat", "uspa", "ussi" };
        protected List<string> ParseMapForUnknowns(Map map, List<string> unknownFileExtensions)
        {
            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (map == null)
            {
                return new List<string>();
            }

            _logEvent("Scanning Map ObjectData");
            if (map.ImportedFiles?.Files != null)
            {
                result.AddRange(map.ImportedFiles.Files.Select(x => x.FullPath));
            }

            if (!string.IsNullOrWhiteSpace(map.Info?.LoadingScreenPath))
            {
                result.Add(map.Info.LoadingScreenPath);
            }

            var allObjectValues = map.GetObjectDataStringValues();
            var stringsWithFileExtensions = allObjectValues.SelectMany(x => SplitTextByFileExtensionLocations(x, unknownFileExtensions)).ToList();
            result.AddRange(stringsWithFileExtensions);
            result.AddRange(stringsWithFileExtensions.SelectMany(x => ScanTextForPotentialUnknownFileNames_SLOW(x, unknownFileExtensions)));

            var commonFileReferenceObjectValues = map.GetObjectDataStringValues(_objectDataKeysWithFileReferences);
            result.AddRange(commonFileReferenceObjectValues);
            result.AddRange(AddCommonModelAndTextureFileExtensions(commonFileReferenceObjectValues));

            return CleanScannedUnknownFileNames(result.ToList());
        }

        protected List<string> ParseFileToDetectPossibleUnknowns(string fileName, List<string> unknownFileExtensions)
        {
            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            var extension = Path.GetExtension(fileName);
            var bytes = File.ReadAllBytes(fileName);
            var allLines = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var encoding in _allEncodings)
            {
                try
                {
                    allLines.AddRange(encoding.GetString(bytes).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
                }
                catch { }
            }
            /*
            try
            {
                allLines.AddRange(ConvertBytesToAscii(bytes).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }
            catch { }
            */

            var stringsWithFileExtensions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            stringsWithFileExtensions.AddRange(ScanBytesForReadableAsciiStrings(bytes).SelectMany(x => SplitTextByFileExtensionLocations(x, unknownFileExtensions)));
            stringsWithFileExtensions.AddRange(allLines.SelectMany(x => SplitTextByFileExtensionLocations(x, unknownFileExtensions)));
            result.AddRange(stringsWithFileExtensions);

            if (extension.Equals(".toc", StringComparison.InvariantCultureIgnoreCase) || extension.Equals(".imp", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(allLines);
            }

            if (extension.Equals(".txt", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(ScanINIForPossibleFileNames(fileName, unknownFileExtensions));
            }

            if (extension.Equals(".mdx", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(ScanMDXForPossibleFileNames(fileName));
            }

            if (extension.Equals(".mdl", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(ScanMDLForPossibleFileNames(fileName));
            }

            if (extension.Equals(".j", StringComparison.InvariantCultureIgnoreCase) || extension.Equals(".lua", StringComparison.InvariantCultureIgnoreCase) || extension.Equals(".slk", StringComparison.InvariantCultureIgnoreCase) || extension.Equals(".txt", StringComparison.InvariantCultureIgnoreCase) || extension.Equals(".fdf", StringComparison.InvariantCultureIgnoreCase))
            {
                var quotedStrings = allLines.SelectMany(x => ParseQuotedStringsFromCode(x)).ToList();
                result.AddRange(AddCommonModelAndTextureFileExtensions(quotedStrings));
            }

            if (extension.Equals(".slk", StringComparison.InvariantCultureIgnoreCase))
            {
                try
                {
                    var slkTable = new SylkParser().Parse(File.OpenRead(fileName));
                    var columns = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
                    for (var idx = 0; idx < slkTable.Width; idx++)
                    {
                        var columnName = slkTable[idx, 0]?.ToString();
                        if (!string.IsNullOrWhiteSpace(columnName))
                        {
                            columns[columnName] = idx;
                        }
                    }
                    var files = new List<string>();
                    if (columns.TryGetValue("dir", out var directoryColumn) && columns.TryGetValue("file", out var fileColumn))
                    {
                        for (var row = 1; row < slkTable.Rows; row++)
                        {
                            try
                            {
                                var directory = slkTable[directoryColumn, row]?.ToString();
                                var file = slkTable[fileColumn, row]?.ToString();
                                if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(file))
                                {
                                    files.Add(Path.Combine(directory, file));
                                }
                            }
                            catch { }
                        }
                        result.AddRange(AddCommonModelAndTextureFileExtensions(files));
                    }
                }
                catch { }
            }

            //todo: [LowPriority] add real FDF parser? (none online, would need to build my own based on included fdf.g grammar file)

            if (extension.Equals(".lua", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(allLines.SelectMany(x => ParseQuotedStringsFromCode(x, '\'', "\\'")));
                var requiredFiles = allLines.Where(x => x.Contains("require", StringComparison.InvariantCultureIgnoreCase)).SelectMany(x =>
                {
                    var requireDoubleQuotedStrings = ParseQuotedStringsFromCode(x);
                    var requireSingleQuotedStrings = ParseQuotedStringsFromCode(x, '\'', "\\'");
                    var allRequiredFiles = requireDoubleQuotedStrings.Concat(requireSingleQuotedStrings).ToList();
                    return allRequiredFiles.Concat(allRequiredFiles.Select(x => $"{x}.lua")).ToList();
                }).ToList();
                result.AddRange(requiredFiles);
            }

            result.AddRange(result.SelectMany(y => ScanTextForPotentialUnknownFileNames_SLOW(y, unknownFileExtensions)).ToList());

            return CleanScannedUnknownFileNames(result.ToList());
        }

        protected List<string> ScanBytesForReadableAsciiStrings(byte[] bytes)
        {
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
            var matches = Regex.Matches(escapingRemoved, $"{quoteCharacter}[^${quoteCharacter}]*{quoteCharacter}").Cast<Match>().ToList();
            foreach (var match in matches)
            {
                result.Add(match.Value.Trim(quoteCharacter).Replace(temporaryEscapeReplacement, quoteCharacter.ToString()));
            }

            return result;
        }

        protected List<string> SplitTextByFileExtensionLocations(string text, List<string> unknownFileExtensions)
        {
            var splitByNull = text.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (splitByNull.Count > 1)
            {
                return splitByNull.SelectMany(x => SplitTextByFileExtensionLocations(x, unknownFileExtensions)).ToList();
            }

            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            var unknownExtensionsWithoutPeriod = unknownFileExtensions.Select(x => x.Trim('.')).ToList();
            var splitLocations = new List<Tuple<int, int>>();
            for (var idx = 0; idx < text.Length; idx++)
            {
                var character = text[idx];
                if (character == '.')
                {
                    foreach (var extension in unknownExtensionsWithoutPeriod)
                    {
                        if (extension.Length + idx < text.Length && text.Substring(idx + 1, extension.Length).Equals(extension, StringComparison.InvariantCulture))
                        {
                            splitLocations.Add(new Tuple<int, int>(idx, extension.Length + 1));
                        }
                    }
                }
            }

            var distinctSplitLocations = splitLocations.GroupBy(x => x.Item1).ToDictionary(x => x.Key, x => x.Select(y => y.Item2).OrderByDescending(y => y).First());
            var startIndex = 0;
            foreach (var split in distinctSplitLocations)
            {
                var idx = split.Key;
                if (idx < startIndex)
                {
                    continue;
                }

                var extensionLength = split.Value;
                var endIdx = idx + extensionLength;
                var length = Math.Min(1000, endIdx - startIndex);
                result.Add(text.Substring(endIdx - length, length));
                var newStartIndex = idx + extensionLength;
                while (distinctSplitLocations.TryGetValue(newStartIndex, out var nextExtensionLength))
                {
                    var nextIdx = newStartIndex;
                    var nextEndIdx = nextIdx + nextExtensionLength;
                    var nextLength = Math.Min(1000, nextEndIdx - startIndex);
                    result.Add(text.Substring(nextEndIdx - nextLength, nextLength));
                    newStartIndex = nextIdx + nextExtensionLength;
                }
                startIndex = newStartIndex;
            }

            return result.ToList();
        }

        protected List<string> ScanTextForPotentialUnknownFileNames_SLOW(string text, List<string> unknownFileExtensions)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            var splitText = SplitTextByFileExtensionLocations(text, unknownFileExtensions);

            if (splitText.Count == 0)
            {
                return new List<string>();
            }

            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            void AddPotentialStrings(List<string> strings)
            {
                result.AddRange(strings.Where(x => unknownFileExtensions.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase))).Select(x => x.Trim()));
            }

            var nonVisibleSeparators = Enumerable.Range((int)'\0', (int)' ' - (int)'\0').Concat(Enumerable.Range(127, 256 - 127)).Select(x => (char)x).ToArray();
            var visibleSeparators = Enumerable.Range(0, 256).Select(x => (char)x).Where(x => !(x >= 'a' && x <= 'z') && !(x >= 'A' && x <= 'Z') && !(x >= '0' && x <= '9')).Except(nonVisibleSeparators).ToList();

            AddPotentialStrings(splitText.SelectMany(x => x.Split(nonVisibleSeparators, StringSplitOptions.RemoveEmptyEntries)).ToList());

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

        protected List<string> AddCommonModelAndTextureFileExtensions(List<string> strings)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            result.AddRange(strings);
            var shortStrings = strings.Where(x => x.Length <= 150).ToList();
            result.AddRange(_modelAndTextureFileExtensions.SelectMany(ext => shortStrings.Select(fileName => $"{fileName}{ext}")));
            result.AddRange(_modelAndTextureFileExtensions.SelectMany(ext => shortStrings.Select(fileName => Path.ChangeExtension(fileName, ext))));
            return result.ToList();
        }

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
                        result.AddRange(AddCommonModelAndTextureFileExtensions(textures));
                    }
                    if (model.Nodes != null)
                    {
                        var nodes = model.Nodes.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        result.AddRange(AddCommonModelAndTextureFileExtensions(nodes));
                    }
                    result.Add($"{model.Name}.mdl");
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
                        result.AddRange(AddCommonModelAndTextureFileExtensions(textures));
                    }
                    if (model.Nodes != null)
                    {
                        var nodes = model.Nodes.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        result.AddRange(AddCommonModelAndTextureFileExtensions(nodes));
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
                    result.AddRange(AddCommonModelAndTextureFileExtensions(textures));
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
                        if (key.KeyName.EndsWith("art", StringComparison.InvariantCultureIgnoreCase) || key.KeyName.EndsWith("name", StringComparison.InvariantCultureIgnoreCase))
                        {
                            result.AddRange(AddCommonModelAndTextureFileExtensions(new List<string>() { key.Value }));
                        }

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

        protected List<string> ScanFileForUnknowns_Regex(string text, List<string> unknownFileExtensions)
        {
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

            return regexFoundFiles;
        }

        public void Dispose()
        {
            CleanTemp();
        }
    }
}