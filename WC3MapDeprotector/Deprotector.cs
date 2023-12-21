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

    public partial class Deprotector
    {
        protected readonly List<string> _nativeEditorFunctions = new List<string>() { "config", "main", "CreateAllUnits", "CreateAllItems", "CreateNeutralPassiveBuildings", "CreatePlayerBuildings", "CreatePlayerUnits", "InitCustomPlayerSlots", "InitGlobals", "InitCustomTriggers", "RunInitializationTriggers", "CreateRegions", "CreateCameras", "InitSounds", "InitCustomTeams", "InitAllyPriorities", "CreateNeutralPassive", "CreateNeutralHostile" };

        protected const string ATTRIB = "Map deprotected by WC3MapDeprotector https://github.com/speige/WC3MapDeprotector\r\n\r\n";
        protected readonly List<string> _commonFileExtensions = new List<string>() { "lua", "ai", "asi", "ax", "blp", "ccd", "clh", "css", "dds", "dll", "dls", "doo", "exe", "exp", "fdf", "flt", "gid", "html", "ifl", "imp", "ini", "j", "jpg", "js", "log", "m3d", "mdl", "mdx", "mid", "mmp", "mp3", "mpq", "mrf", "pld", "png", "shd", "slk", "tga", "toc", "ttf", "txt", "url", "w3a", "w3b", "w3c", "w3d", "w3e", "w3g", "w3h", "w3i", "w3m", "w3n", "w3q", "w3r", "w3s", "w3t", "w3u", "w3x", "wai", "wav", "wct", "wpm", "wpp", "wtg", "wts" }.Distinct().ToList();

        protected string _inMapFile;
        protected readonly string _outMapFile;
        public DeprotectionSettings Settings { get; private set; }
        protected readonly Action<string> _logEvent;
        protected HashSet<string> _extractedMapFiles;
        protected DeprotectionResult _deprotectionResult;

        public Deprotector(string inMapFile, string outMapFile, DeprotectionSettings settings, Action<string> logEvent)
        {
            _inMapFile = inMapFile;
            _outMapFile = outMapFile;
            Settings = settings;
            _logEvent = logEvent;
        }

        protected string ExeFolderPath
        {
            get
            {
                return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            }
        }

        protected string BaseMapFilesPath
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
                return Path.Combine(ExeFolderPath, "SilkObjectOptimizer");
            }
        }

        protected string SLKRecoverEXE
        {
            get
            {
                return Path.Combine(SLKRecoverPath, "Silk Object Optimizer.exe");
            }
        }

        protected string InstallerListFileName
        {
            get
            {

                return Path.Combine(ExeFolderPath, "listfile.zip");
            }
        }

        protected string WorkingListFileName
        {
            get
            {

                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WC3MapDeprotector", "listfile.txt");
            }
        }

        protected string WorkingFolderPath
        {
            get
            {
                //todo: move to different location so it doesn't mess up install
                return Path.Combine(ExeFolderPath, $"{Path.GetFileName(_inMapFile)}.work");
            }
        }

        protected string TempFolderPath
        {
            get
            {
                return Path.Combine(Path.GetTempPath(), "WC3MapDeprotector");
            }
        }

        protected string MapFilesPath
        {
            get
            {
                return Path.Combine(WorkingFolderPath, "files");
            }
        }

        protected string UnknownFilesPath
        {
            get
            {
                return Path.Combine(MapFilesPath, "unknowns");
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

        protected async Task LiveGameScanForUnknownFiles(IMPQArchive archive, DeprotectionResult deprotectionResult)
        {
            var process = ExecuteCommand(Settings.WC3ExePath, $"-launch -loadfile \"{_inMapFile}\" -mapdiff 1 -testmapprofile WorldEdit -fixedseed 1");
            //Thread.Sleep(15 * 1000); // wait for WC3 to load

            var directories = new HashSet<string>();
            directories.AddRange(_extractedMapFiles.Select(x => Path.GetDirectoryName(x.ToUpper().Replace("/", "\\"))).Where(x => x != null));
            var originalDirectories = directories.ToList();
            foreach (var directory in originalDirectories)
            {
                var split = directory.Split('\\');
                for (int i = 1; i <= split.Length; i++)
                {
                    directories.Add(split.Take(i).Aggregate((x, y) => $"{x}\\{y}"));
                }
            }

            //var extensions = _commonFileExtensions.Select(x => "." + x).Distinct().ToList();
            var extensions = GetUnknownFileNames().Select(x => Path.GetExtension(x).ToUpper()).Distinct().ToList();
            var cheatEngineForm = new frmLiveGameScanner(scannedFileName =>
            {
                var filesToTest = new List<string>() { scannedFileName };
                var baseFileName = Path.GetFileName(scannedFileName);
                foreach (var directory in directories)
                {
                    filesToTest.Add(directory + "\\" + baseFileName);
                }

                foreach (var filename in filesToTest)
                {
                    if (!_extractedMapFiles.Contains(filename.ToUpper()) && archive.DiscoverFile(filename))
                    {
                        ExtractFileFromArchive(archive, filename);
                        _extractedMapFiles.Add(filename.ToUpper());

                        File.AppendAllLines(WorkingListFileName, new string[] { filename });
                        deprotectionResult.NewListFileEntriesFound++;
                        _logEvent($"added to global listfile: {filename}");

                        if (archive.UnknownFileNameHashes.Count == 0)
                        {
                            process.Kill();
                            return;
                        }
                    }
                }
            }, process, extensions);
            cheatEngineForm.ShowDialog();
        }

        public async unsafe Task<DeprotectionResult> Deprotect()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_outMapFile));
                File.Delete(_outMapFile);
            }
            catch
            {
                throw new Exception($"Output Map File is locked. Please close any MPQ programs, WC3 Game, & WorldEditor and try again. File: {_outMapFile}");
            }

            _extractedMapFiles = new HashSet<string>();
            _deprotectionResult = new DeprotectionResult();
            _deprotectionResult.WarningMessages.Add($"NOTE: This tool is a work in progress. Deprotection does not work perfectly on every map. If objects are missing or script has compilation errors, you will need to fix these by hand. You can get help from my YouTube channel or report defects by clicking the bug icon.");

            Directory.CreateDirectory(Path.GetDirectoryName(_outMapFile));

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

            if (!File.Exists(_inMapFile))
            {
                throw new FileNotFoundException($"Cannot find source map file: {_inMapFile}");
            }
            _logEvent($"Processing map: {MapBaseName}");
            CleanTemp();

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
                File.WriteAllLines(WorkingListFileName, extractedListFileEntries.Concat(existingListFileEntries).OrderBy(x => x).Distinct());
                File.Delete(InstallerListFileName);
                File.Delete(Path.Combine(extractedListFileFolder, "listfile.txt"));
            }

            var originalListFile = File.ReadAllLines(WorkingListFileName);
            var listFile = new List<string>(originalListFile.OrderBy(x => x).Distinct());
            if (originalListFile.Length != listFile.Count)
            {
                File.WriteAllLines(WorkingListFileName, listFile);
            }

            if (!Directory.Exists(WorkingFolderPath))
            {
                Directory.CreateDirectory(WorkingFolderPath);
            }
            if (!Directory.Exists(MapFilesPath))
            {
                Directory.CreateDirectory(MapFilesPath);
            }

            using (var inMPQArchive = new MPQArchiveWrapper(_inMapFile))
            {
                if (ExtractFileFromArchive(inMPQArchive, "(listfile)"))
                {
                    inMPQArchive.ProcessListFile(File.ReadAllLines(Path.Combine(MapFilesPath, "(listfile)")).ToList());
                }

                if (inMPQArchive.UnknownFileNameHashes.Count > 0)
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                }

                try
                {
                    inMPQArchive.ProcessDefaultListFile();
                    inMPQArchive.ProcessListFile(listFile);
                }
                catch (Exception e)
                {
                    _logEvent(e.Message);
                    throw new Exception("MPQ Header corrupted, unable to repair.");
                }

                var initialMpqFileHashes = inMPQArchive.AllFileNameHashes.ToList();
                foreach (var hash in initialMpqFileHashes)
                {
                    var success = ExtractFileFromArchive(inMPQArchive, hash);

                    if (success && inMPQArchive.DiscoveredFileNames.TryGetValue(hash, out var fileName))
                    {
                        _extractedMapFiles.Add(fileName.ToUpper());
                    }
                }

                _logEvent($"Unknown file count: {inMPQArchive.UnknownFileNameHashes.Count}");

                var slkFiles = Directory.GetFiles(MapFilesPath, "*.slk", SearchOption.AllDirectories);
                if (slkFiles.Length > 0)
                {
                    _logEvent("Generating temporary map for SLK Recover: slk.w3x");
                    var slkMpqArchive = Path.Combine(SLKRecoverPath, "slk.w3x");
                    var excludedFileNames = new string[] { "war3map.w3a", "war3map.w3b", "war3map.w3d" }; // can't be in slk.w3x or it will crash
                    var minimumExtraRequiredFiles = Directory.GetFiles(MapFilesPath, "war3map*", SearchOption.AllDirectories).Union(Directory.GetFiles(MapFilesPath, "war3campaign*", SearchOption.AllDirectories)).Where(x => !excludedFileNames.Contains(Path.GetFileName(x).ToUpper())).ToList();

                    BuildW3X(slkMpqArchive, MapFilesPath, slkFiles.Union(minimumExtraRequiredFiles).ToList());
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
                            if (!File.Exists(Path.Combine(MapFilesPath, fileName)))
                            {
                                _deprotectionResult.CountOfProtectionsFound++;
                            }

                            File.Copy(recoveredFile, Path.Combine(MapFilesPath, fileName), true);
                        }
                    }
                }

                if (inMPQArchive.UnknownFileNameHashes.Count > 0)
                {
                    var map = DecompileMap();
                    var externalReferencedFiles = ScanMapForExternalFileReferences(map, _commonFileExtensions.Select(x => "." + x).Concat(GetUnknownFileNames()).Select(x => Path.GetExtension(x)).ToList());
                    ScanForUnknownFiles(inMPQArchive, externalReferencedFiles.ToList(), _deprotectionResult);
                }

                if (inMPQArchive.UnknownFileNameHashes.Count > 0)
                {
                    _logEvent("Scanning for possible filenames...");
                    var externalReferencedFiles = ScanFilesForExternalFileReferences(_extractedMapFiles.Select(x => Path.Combine(MapFilesPath, x)).Union(GetUnknownFileNames().Select(x => Path.Combine(UnknownFilesPath, x))).ToList());
                    ScanForUnknownFiles(inMPQArchive, externalReferencedFiles.ToList(), _deprotectionResult);
                }

                if (inMPQArchive.UnknownFileNameHashes.Count > 0)
                {
                    //todo: add checkbox to disable this option?
                    //var cancellationToken = new CancellationToken();
                    //await LiveGameScanForUnknownFiles(inMPQArchive, _deprotectionResult);
                    //WaitForProcessToExit(process, cancellationToken);
                }

                if (Settings.BruteForceUnknowns && inMPQArchive.UnknownFileNameHashes.Count > 0)
                {
                    BruteForceUnknownFileNames(inMPQArchive, _deprotectionResult);
                }

                _deprotectionResult.UnknownFileCount = inMPQArchive.UnknownFileNameHashes.Count;
                if (_deprotectionResult.UnknownFileCount > 0)
                {
                    _deprotectionResult.WarningMessages.Add($"WARNING: {_deprotectionResult.UnknownFileCount} files have unresolved names");
                    _deprotectionResult.WarningMessages.Add("These files will be lost and deprotected map may be incomplete or even unusable!");
                    _deprotectionResult.WarningMessages.Add("You can try fixing by searching online for listfile.txt, using 'Brute force unknown files (SLOW)' option, or by using 'W3X Name Scanner' tool in MPQEditor.exe");
                }
            }

            DeleteAls();
            PatchW3I(_deprotectionResult);

            //These are probably protected, but the only way way to verify if they aren't is to parse the script (which is probably obfuscated), but if we can sucessfully parse, then we can just re-generate them to be safe.
            File.Delete(Path.Combine(MapFilesPath, "war3mapunits.doo"));
            File.Delete(Path.Combine(MapFilesPath, "war3map.wct"));
            File.Delete(Path.Combine(MapFilesPath, "war3map.wtg"));
            File.Delete(Path.Combine(MapFilesPath, "war3map.w3r"));
            //todo: keep a copy of these & diff with decompiled versions from war3map.j so we can update _deprotectionResult.CountOfProtectionsFound

            var skinPath = Path.Combine(MapFilesPath, "war3mapSkin.txt");
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

            if (!File.Exists(Path.Combine(MapFilesPath, "war3map.j")) && !File.Exists(Path.Combine(MapFilesPath, "war3map.lua")))
            {
                _deprotectionResult.CountOfProtectionsFound++;
            }

            var unknownFiles = GetUnknownFileNames();
            if (!File.Exists(Path.Combine(MapFilesPath, "war3map.j")))
            {
                foreach (var unknownFile in unknownFiles)
                {
                    var text = File.ReadAllText(Path.Combine(UnknownFilesPath, unknownFile));
                    if (Regex.IsMatch(text, "function\\s+config\\s+takes\\s+nothing\\s+returns\\s+nothing", RegexOptions.IgnoreCase))
                    {
                        File.Copy(Path.Combine(MapFilesPath, unknownFile), Path.Combine(MapFilesPath, "war3map.j"), true);
                        break;
                    }
                }
            }

            if (!File.Exists(Path.Combine(MapFilesPath, "war3map.lua")))
            {
                foreach (var unknownFile in unknownFiles)
                {
                    var text = File.ReadAllText(Path.Combine(UnknownFilesPath, unknownFile));
                    if (Regex.IsMatch(text, "function\\s+config\\s*\\(\\)", RegexOptions.IgnoreCase))
                    {
                        File.Copy(Path.Combine(MapFilesPath, unknownFile), Path.Combine(MapFilesPath, "war3map.lua"), true);
                        break;
                    }
                }
            }

            var replace = "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61";
            while (File.Exists(Path.Combine(MapFilesPath, replace)))
            {
                replace = replace.Insert(replace.Length - 4, "_old");
            }
            if (File.Exists(Path.Combine(MapFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61")))
            {
                File.Move(Path.Combine(MapFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61"), Path.Combine(MapFilesPath, replace), true);
            }
            File.Copy(Path.Combine(BaseMapFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x33\x77\x69"), Path.Combine(MapFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61"), true);
            var replace2 = "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78";
            while (File.Exists(Path.Combine(MapFilesPath, replace2)))
            {
                replace2 = replace2.Insert(replace2.Length - 4, "_old");
            }
            if (File.Exists(Path.Combine(MapFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78")))
            {
                File.Move(Path.Combine(MapFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78"), Path.Combine(MapFilesPath, replace2), true);
            }
            File.Copy(Path.Combine(BaseMapFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x33\x77\x6D"), Path.Combine(MapFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78"), true);

            var scriptFiles = Directory.GetFiles(MapFilesPath, "war3map.j", SearchOption.AllDirectories).Union(Directory.GetFiles(MapFilesPath, "war3map.lua", SearchOption.AllDirectories)).ToList();
            foreach (var scriptFile in scriptFiles)
            {
                var baseFilePath = Path.Combine(MapFilesPath, Path.GetFileName(scriptFile).ToUpper());
                var basePathScriptFileName = Path.Combine(MapFilesPath, Path.GetFileName(scriptFile));
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
                _logEvent($"Moving '{scriptFile}' to '{baseFilePath}'");
            }

            File.Copy(Path.Combine(BaseMapFilesPath, "war3map.3ws"), Path.Combine(MapFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76"), true);

            string jassScript = null;
            string luaScript = null;
            ScriptMetaData scriptMetaData = null;
            if (File.Exists(Path.Combine(MapFilesPath, "war3map.j")))
            {
                jassScript = $"// {ATTRIB}{File.ReadAllText(Path.Combine(MapFilesPath, "war3map.j"))}";
                try
                {
                    jassScript = DeObfuscateJassScript(jassScript);
                    scriptMetaData = DecompileJassScriptMetaData(jassScript);
                }
                catch { }
            }

            if (File.Exists(Path.Combine(MapFilesPath, "war3map.lua")))
            {
                _deprotectionResult.WarningMessages.Add("WARNING: This map was built using Lua instead of Jass. Deprotection of Lua maps is not fully supported yet. It will open in the editor, but the render screen will be missing units/items/regions/cameras/sounds.");
                luaScript = DeObfuscateLuaScript($"-- {ATTRIB}{File.ReadAllText(Path.Combine(MapFilesPath, "war3map.lua"))}");
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
                    if (!File.Exists(Path.Combine(MapFilesPath, file.FileName)))
                    {
                        _deprotectionResult.CountOfProtectionsFound++;
                    }

                    SaveDecompiledArchiveFile(file);
                }

                if (Settings.CreateVisualTriggers)
                {
                    if (File.Exists(Path.Combine(MapFilesPath, "war3map.wtg")))
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
                    File.Delete(Path.Combine(MapFilesPath, "war3map.wct"));
                    File.Delete(Path.Combine(MapFilesPath, "war3map.wtg"));
                }
            }

            if (string.IsNullOrWhiteSpace(luaScript) && Settings.TranspileJassToLUA && !string.IsNullOrWhiteSpace(jassScript))
            {
                _logEvent("Transpiling JASS to LUA");

                luaScript = ConvertJassToLua(jassScript);
                if (!string.IsNullOrWhiteSpace(luaScript))
                {
                    File.WriteAllText(Path.Combine(MapFilesPath, "war3map.lua"), luaScript);
                    File.Delete(Path.Combine(MapFilesPath, "war3map.j"));
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

                        var fileExtensionsToReplace = new string[] { ".w3i", ".wts", ".wtg", ".wct" };
                        var filesToReplace = mapFiles.Where(x => fileExtensionsToReplace.Contains(Path.GetExtension(x.FileName).ToUpper())).ToList();
                        foreach (var file in filesToReplace)
                        {
                            SaveDecompiledArchiveFile(file);
                        }

                        if (Settings.CreateVisualTriggers)
                        {
                            if (File.Exists(Path.Combine(MapFilesPath, "war3map.wtg")))
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
                            File.Delete(Path.Combine(MapFilesPath, "war3map.wct"));
                            File.Delete(Path.Combine(MapFilesPath, "war3map.wtg"));
                        }

                        File.Delete(Path.Combine(MapFilesPath, "war3map.j"));
                    }
                    catch { }
                }
            }

            if (luaScript != null)
            {
                if (!File.Exists(Path.Combine(MapFilesPath, "war3map.wtg")))
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                    CreateLuaCustomTextVisualTriggerFile(luaScript);
                }
            }
            else if (jassScript != null)
            {
                if (!File.Exists(Path.Combine(MapFilesPath, "war3map.wtg")))
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                    WriteWtg_PlainText_Jass(jassScript);
                    //CreateJassCustomTextVisualTriggerFile(jassScript); // unfinished & buggy [causing compiler errors]
                }
            }

            if (!File.Exists(Path.Combine(MapFilesPath, "war3mapunits.doo")))
            {
                _deprotectionResult.CountOfProtectionsFound++;
                File.Copy(Path.Combine(BaseMapFilesPath, "war3mapunits.doo"), Path.Combine(MapFilesPath, "war3mapunits.doo"), true);
                _deprotectionResult.CriticalWarningCount++;
                _deprotectionResult.WarningMessages.Add("WARNING: war3mapunits.doo could not be recovered. Map will still open in WorldEditor & run, but units will not be visible in WorldEditor rendering and saving in world editor will corrupt your war3map.j or war3map.lua script file.");
            }

            if (!File.Exists(Path.Combine(MapFilesPath, "war3map.wtg")))
            {
                _deprotectionResult.CountOfProtectionsFound++;
                File.Copy(Path.Combine(BaseMapFilesPath, "war3map.wtg"), Path.Combine(MapFilesPath, "war3map.wtg"), true);
                File.Delete(Path.Combine(MapFilesPath, "war3map.wct"));
                _deprotectionResult.CriticalWarningCount++;
                _deprotectionResult.WarningMessages.Add("WARNING: triggers could not be recovered. Map will still open in WorldEditor & run, but saving in world editor will corrupt your war3map.j or war3map.lua script file.");
            }

            BuildImportList();

            var unknownFilesHash = new HashSet<string>(unknownFiles.Select(x => Path.Combine(UnknownFilesPath, x).Trim().ToUpper()));
            var withUnknowns = Directory.GetFiles(MapFilesPath, "*", SearchOption.AllDirectories).ToList();
            var finalFiles = withUnknowns.Where(x => !unknownFilesHash.Contains(x.Trim().ToUpper())).ToList();

            BuildW3X(_outMapFile, MapFilesPath, finalFiles);

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
                var blz = Regex.Match(script, "\\s+call\\s+InitBlizzard\\s*\\(\\s*\\)\\s*", RegexOptions.IgnoreCase);
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
                var shortFileName = file.Replace($"{baseFolder}{Path.DirectorySeparatorChar}", "");
                var mpqFile = MpqFile.New(File.OpenRead(file), shortFileName);
                mpqFile.CompressionType = MpqCompressionType.ZLib;
                /* // not supported by War3Net yet
                if (Path.GetExtension(shortFileName).ToUpper() == ".WAV")
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
            var fileName = Path.Combine(MapFilesPath, mpqFile.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            using (var stream = new FileStream(fileName, FileMode.OpenOrCreate))
            {
                mpqFile.MpqStream.CopyTo(stream);
            }

            _logEvent($"Extracted from MPQ: {fileName}");

            return true;
        }

        protected bool ExtractFileFromArchive(IMPQArchive archive, string archiveFileName)
        {
            if (!archive.DiscoverFile(archiveFileName))
            {
                return false;
            }

            return ExtractFileFromArchive(archive, archive.DiscoveredFileNames.First(x => string.Equals(x.Value, archiveFileName, StringComparison.InvariantCultureIgnoreCase)).Key);
        }

        protected Dictionary<ulong, string> GetUnknownFileName_Memoized = new Dictionary<ulong, string>();
        protected bool ExtractFileFromArchive(IMPQArchive archive, ulong archiveFileHash)
        {
            var tempFileName = Path.GetTempFileName();
            string extractedFileName;
            if (archive.DiscoveredFileNames.TryGetValue(archiveFileHash, out var fileName))
            {
                extractedFileName = Path.Combine(MapFilesPath, fileName);
            }
            else
            {
                extractedFileName = tempFileName;
            }

            if (!archive.ExtractFile(archiveFileHash, extractedFileName))
            {
                return false;
            }

            if (extractedFileName == tempFileName)
            {
                if (!GetUnknownFileName_Memoized.TryGetValue(archiveFileHash, out var unknownName))
                {
                    using (var stream = File.OpenRead(extractedFileName))
                    {
                        unknownName = archiveFileHash + IMPQArchiveExtensions.PredictUnknownFileExtension(stream);
                    }
                    GetUnknownFileName_Memoized[archiveFileHash] = unknownName;
                }

                var unknownNameWithPath = Path.Combine(UnknownFilesPath, unknownName);
                if (File.Exists(unknownNameWithPath))
                {
                    File.Delete(unknownNameWithPath);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(unknownNameWithPath));
                File.Move(tempFileName, unknownNameWithPath);
            }

            _logEvent($"Extracted from MPQ: {fileName}");

            return true;
        }

        protected List<string> GetUnknownFileNames()
        {
            Directory.CreateDirectory(UnknownFilesPath);
            return Directory.GetFiles(UnknownFilesPath, "*", SearchOption.TopDirectoryOnly).Select(x => x.Replace($"{UnknownFilesPath}{Path.DirectorySeparatorChar}", "")).ToList();
        }

        protected void BruteForceUnknownFileNames(IMPQArchive archive, DeprotectionResult deprotectionResult)
        {
            //todo: Convert to Vector<> variables to support SIMD architecture speed up
            var unknownFileCount = archive.UnknownFileNameHashes.Count;
            _logEvent($"unknown files remaining: {unknownFileCount}");

            var directories = archive.DiscoveredFileNames.Values.Select(x => Path.GetDirectoryName(x).ToUpper()).Select(x => x.EndsWith(Path.DirectorySeparatorChar) ? x : x + Path.DirectorySeparatorChar).Select(x => x.TrimStart(Path.DirectorySeparatorChar)).Distinct().ToList();
            var extensions = GetUnknownFileNames().Select(x => Path.GetExtension(x).ToUpper()).Distinct().ToList();

            const int maxFileNameLength = 75;
            _logEvent($"Brute forcing filenames from length 1 to {maxFileNameLength}");

            var possibleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_!&()' .".ToUpper().ToCharArray();

            var unknownHashes = archive.UnknownFileNameHashes;

            var foundFileLock = new object();
            var foundFileCount = 0;
            Settings.BruteForceCancellationToken = new CancellationTokenSource();
            try
            {
                var simdLength = Vector<int>.Count;
                Parallel.ForEach(directories, new ParallelOptions() { CancellationToken = Settings.BruteForceCancellationToken.Token }, directoryName =>
                {
                    MPQHashing.StartHash(out var leftDirectoryHash, out var leftDirectorySeed, out var rightDirectoryHash, out var rightDirectorySeed);
                    for (var i = 0; i < directoryName.Length; i++)
                    {
                        MPQHashing.AddCharToHash(leftDirectoryHash, leftDirectorySeed, rightDirectoryHash, rightDirectorySeed, directoryName[i], out leftDirectoryHash, out leftDirectorySeed, out rightDirectoryHash, out rightDirectorySeed);
                    }

                    var testCallback = (string bruteText) =>
                    {
                        //todo: refactor to save file prefix hash so it only needs to update based on the most recent character changed
                        var leftBruteTextHash = leftDirectoryHash;
                        var rightBruteTextHash = rightDirectoryHash;
                        var leftBruteTextSeed = leftDirectorySeed;
                        var rightBruteTextSeed = rightDirectorySeed;
                        for (var i = 0; i < bruteText.Length; i++)
                        {
                            MPQHashing.AddCharToHash(leftBruteTextHash, leftBruteTextSeed, rightBruteTextHash, rightBruteTextSeed, bruteText[i], out leftBruteTextHash, out leftBruteTextSeed, out rightBruteTextHash, out rightBruteTextSeed);
                        }

                        foreach (var fileExtension in extensions)
                        {
                            var leftFileExtensionHash = leftBruteTextHash;
                            var rightFileExtensionHash = rightBruteTextHash;
                            var leftFileExtensionSeed = leftBruteTextSeed;
                            var rightFileExtensionSeed = rightBruteTextSeed;
                            for (var i = 0; i < fileExtension.Length; i++)
                            {
                                MPQHashing.AddCharToHash(leftFileExtensionHash, leftFileExtensionSeed, rightFileExtensionHash, rightFileExtensionSeed, fileExtension[i], out leftFileExtensionHash, out leftFileExtensionSeed, out rightFileExtensionHash, out rightFileExtensionSeed);
                            }
                            var finalHash = MPQHashing.FinalizeHash(leftFileExtensionHash, rightFileExtensionHash);
                            if (unknownHashes.Contains(finalHash))
                            {
                                lock (foundFileLock)
                                {
                                    var fileName = Path.Combine(directoryName, $"{bruteText}{fileExtension}");
                                    if (!_extractedMapFiles.Contains(fileName.ToUpper()) && archive.DiscoverFile(fileName))
                                    {
                                        ExtractFileFromArchive(archive, finalHash);
                                        File.AppendAllLines(WorkingListFileName, new string[] { fileName });
                                        deprotectionResult.NewListFileEntriesFound++;
                                        _logEvent($"added to global listfile: {fileName}");

                                        var newUnknownCount = archive.UnknownFileNameHashes.Count;
                                        if (unknownFileCount != newUnknownCount)
                                        {
                                            foundFileCount++;
                                            unknownFileCount = newUnknownCount;
                                            _logEvent($"unknown files remaining: {unknownFileCount}");

                                            if (unknownFileCount == 0)
                                            {
                                                Settings.BruteForceCancellationToken.Cancel();
                                                return;
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

        protected void ScanForUnknownFiles(IMPQArchive archive, List<string> fileNamesToTest, DeprotectionResult deprotectionResult)
        {
            var filesFound = 0;
            try
            {
                _logEvent("Performing deep scan for unknown files ...");

                var baseFileNames = new HashSet<string>(fileNamesToTest.Select(x => Path.GetFileName(x).ToUpper().Trim('\\')));

                foreach (var fileName in baseFileNames.Where(x => x.EndsWith(".BLP") || x.EndsWith(".TGA")).ToList())
                {
                    baseFileNames.Add($"DIS{Path.GetFileName(fileName)}");
                }
                foreach (var fileName in baseFileNames.Where(x => x.EndsWith(".MDL") || x.EndsWith(".MDX")).ToList())
                {
                    baseFileNames.Add($"{Path.GetFileNameWithoutExtension(fileName)}_PORTRAIT{Path.GetExtension(fileName)}");
                }

                foreach (var fileName in baseFileNames.Where(x => x.EndsWith(".BLP")).ToList())
                {
                    baseFileNames.Add(Path.ChangeExtension(fileName, ".TGA"));
                }
                foreach (var fileName in baseFileNames.Where(x => x.EndsWith(".TGA")).ToList())
                {
                    baseFileNames.Add(Path.ChangeExtension(fileName, ".BLP"));
                }
                foreach (var fileName in baseFileNames.Where(x => x.EndsWith(".MDL")).ToList())
                {
                    baseFileNames.Add($"{Path.GetFileNameWithoutExtension(fileName)}.MDX");
                }
                foreach (var fileName in baseFileNames.Where(x => x.EndsWith(".MDX")).ToList())
                {
                    baseFileNames.Add($"{Path.GetFileNameWithoutExtension(fileName)}.MDL");
                }

                var directories = new HashSet<string>();
                directories.Add(@"REPLACEABLETEXTURES\COMMANDBUTTONSDISABLED");
                directories.AddRange(archive.DiscoveredFileNames.Values.Select(x => Path.GetDirectoryName(x).ToUpper()).Select(x => x.EndsWith(Path.DirectorySeparatorChar) ? x : x + Path.DirectorySeparatorChar).Select(x => x.TrimStart(Path.DirectorySeparatorChar)).Distinct());
                directories.AddRange(fileNamesToTest.Select(x => Path.GetDirectoryName(x.ToUpper().Replace("/", "\\").Trim('\\'))).Where(x => x != null));
                foreach (var directory in directories.ToList())
                {
                    var split = directory.Split('\\');
                    for (int i = 1; i <= split.Length; i++)
                    {
                        directories.Add(split.Take(i).Aggregate((x, y) => $"{x}\\{y}"));
                    }
                }

                var allFileNamePermutations = new HashSet<string>();
                foreach (var directory in directories)
                {
                    _logEvent($"Deep scanning - {directory}");

                    foreach (var fileName in baseFileNames)
                    {
                        var scannedFileName = directory + "\\" + fileName;
                        if (!_extractedMapFiles.Contains(scannedFileName.ToUpper()) && archive.DiscoverFile(scannedFileName))
                        {
                            ExtractFileFromArchive(archive, scannedFileName);
                            _extractedMapFiles.Add(scannedFileName.ToUpper());
                            filesFound++;

                            File.AppendAllLines(WorkingListFileName, new string[] { scannedFileName });
                            deprotectionResult.NewListFileEntriesFound++;
                            _logEvent($"added to global listfile: {scannedFileName}");

                            if (archive.UnknownFileNameHashes.Count == 0)
                            {
                                return;
                            }
                        }
                    }
                }
            }
            finally
            {
                _logEvent($"Deep Scan completed, {filesFound} filenames found");
            }
        }

        protected string DeObfuscateFourCCJass(string jassScript)
        {
            var result = Regex.Replace(jassScript, "\\$[0-9a-fA-F]{8}", x =>
            {
                var byte1 = Convert.ToByte(x.Value.Substring(1, 2), 16);
                var byte2 = Convert.ToByte(x.Value.Substring(3, 2), 16);
                var byte3 = Convert.ToByte(x.Value.Substring(5, 2), 16);
                var byte4 = Convert.ToByte(x.Value.Substring(7, 2), 16);
                return $"'{Encoding.ASCII.GetString(new byte[] { byte1, byte2, byte3, byte4 })}'";
            });

            if (jassScript != result)
            {
                _deprotectionResult.CountOfProtectionsFound++;
                _logEvent("FourCC codes de-obfuscated");
            }

            return result;
        }

        protected string DeObfuscateFourCCLua(string jassScript)
        {
            var result = Regex.Replace(jassScript, "\\$[0-9a-f]{8}", x =>
            {
                var byte1 = Convert.ToByte(x.Value.Substring(1, 2), 16);
                var byte2 = Convert.ToByte(x.Value.Substring(3, 2), 16);
                var byte3 = Convert.ToByte(x.Value.Substring(5, 2), 16);
                var byte4 = Convert.ToByte(x.Value.Substring(7, 2), 16);
                return $"FourCC(\"{Encoding.ASCII.GetString(new byte[] { byte1, byte2, byte3, byte4 })}\")";
            });

            if (jassScript != result)
            {
                _deprotectionResult.CountOfProtectionsFound++;
                _logEvent("FourCC codes de-obfuscated");
            }

            return result;
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
                                                        if (Regex.IsMatch(stringValue, "^[0-9a-zA-Z]{4}$") && !string.Equals(stringValue, "true", StringComparison.InvariantCultureIgnoreCase))
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
            var mapFiles = Directory.GetFiles(MapFilesPath, "war3map*", SearchOption.AllDirectories).Union(Directory.GetFiles(MapFilesPath, "war3campaign*", SearchOption.AllDirectories)).OrderBy(x => x.ToUpper() == "WAR3MAP.W3i" ? 0 : 1).ToList();

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

        protected string DeObfuscateJassScript(string jassScript)
        {
            SplitUserDefinedAndGlobalGeneratedGlobalVariableNames(jassScript, out var userDefinedGlobals, out var globalGenerateds);

            var deObfuscated = DeObfuscateFourCCJass(jassScript);

            var parsed = ParseJassScript(deObfuscated);
            var formatted = "";
            using (var writer = new StringWriter())
            {
                var globalVariableRenames = new Dictionary<string, JassIdentifierNameSyntax>();
                var uniqueNames = new HashSet<string>(parsed.Declarations.Where(x => x is JassGlobalDeclarationListSyntax).Cast<JassGlobalDeclarationListSyntax>().SelectMany(x => x.Globals).Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>().Select(x => x.Declarator.IdentifierName.Name));
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

                            var shortTypes = new Dictionary<string, string> { { "rect", "rct" }, { "sound", "snd" }, { "trigger", "trg" }, { "unit", "unit" }, { "destructable", "dest" }, { "camerasetup", "cam" }, { "item", "item" }, { "integer", "int" }, { "boolean", "bool" } };
                            var shortTypeName = typeName;
                            if (shortTypes.ContainsKey(typeName.ToUpper()))
                            {
                                shortTypeName = shortTypes[typeName.ToUpper()];
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
                            uniqueNames.Add(uniqueName.ToUpper());
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
                var match = Regex.Match(stringBuilder.ToString(), "\\s+endglobals\\s+", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var beforeByteCorrections = "13,10,116,114,105,103,103,101,114,32,103,103,95,116,114,103,95,119,97,114,51,109,97,112,32,61,32,110,117,108,108,13,10".Split(',').ToList();
                    for (var i = 0; i < beforeByteCorrections.Count; i++)
                    {
                        var correction = beforeByteCorrections[i];
                        stringBuilder.Insert(match.Index + i, (char)byte.Parse(correction));
                    }
                }

                match = Regex.Match(stringBuilder.ToString(), "\\s+function\\s+main\\s+takes\\s+nothing\\s+returns\\s+nothing\\s+((local\\s+|//).*\\s*)*", RegexOptions.IgnoreCase);
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

            using (var stream = new FileStream(Path.Combine(MapFilesPath, "war3map.wtg"), FileMode.OpenOrCreate))
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
            using (var stream = new FileStream(Path.Combine(MapFilesPath, "war3map.wct"), FileMode.OpenOrCreate))
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

            using (var stream = new FileStream(Path.Combine(MapFilesPath, "war3map.wtg"), FileMode.OpenOrCreate))
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
            using (var stream = new FileStream(Path.Combine(MapFilesPath, "war3map.wct"), FileMode.OpenOrCreate))
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

        protected void CleanTemp()
        {
            var tempDir = WorkingFolderPath;
            if (Directory.Exists(tempDir))
            {
                _logEvent("Deleting temporary files...");
                Directory.Delete(tempDir, true);
            }
        }

        protected void DeleteAls()
        {
            if (File.Exists(Path.Combine(MapFilesPath, "(attributes)")))
            {
                _logEvent("Deleting (attributes)...");
                File.Delete(Path.Combine(MapFilesPath, "(attributes)"));
            }
            if (File.Exists(Path.Combine(MapFilesPath, "(listfile)")))
            {
                _logEvent("Deleting (listfile)...");
                File.Delete(Path.Combine(MapFilesPath, "(listfile)"));
            }
            if (File.Exists(Path.Combine(MapFilesPath, "(signature)")))
            {
                _logEvent("Deleting (signature)...");
                File.Delete(Path.Combine(MapFilesPath, "(signature)"));
            }
        }

        protected void PatchW3I(DeprotectionResult deprotectionResult)
        {
            var w3ipath = Path.Combine(MapFilesPath, "war3map.w3i");
            _logEvent("Patching war3map.w3i...");
            using (var w3i = File.Open(w3ipath, FileMode.Open, FileAccess.ReadWrite))
            {
                w3i.Seek(-1, SeekOrigin.End);
                var c = new byte[1];
                w3i.Read(c, 0, 1);
                if (c[0] == 0xFF)
                {
                    deprotectionResult.CountOfProtectionsFound++;
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

        protected void BuildImportList()
        {
            _logEvent("Building war3map.imp...");
            var files = Directory.GetFiles(MapFilesPath, "*", SearchOption.AllDirectories).ToList();
            var newfiles = new List<string>();
            foreach (var file in files.Select(x => x.Replace($"{MapFilesPath}{Path.DirectorySeparatorChar}", "")))
            {
                if (!Regex.IsMatch(file, @"^war3(map|campaign)(\.(w[a-zA-Z0-9]{2}|doo|shd|mmp|j|imp)|misc\.txt|skin\.txt|map\.blp|units\.doo|extra\.txt)$", RegexOptions.IgnoreCase))
                {
                    newfiles.Add($"{file}\x00");
                }
            }
            newfiles.Add("\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76\x00");
            newfiles = newfiles.Distinct().ToList();
            using (var stream = new FileStream(Path.Combine(MapFilesPath, "war3map.imp"), FileMode.OpenOrCreate))
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

        protected HashSet<string> ScanMapForExternalFileReferences(Map map, List<string> fileExtensions)
        {
            HashSet<string> result = new HashSet<string>();

            if (map == null)
            {
                return result;
            }

            if (map.ImportedFiles?.Files != null)
            {
                result.AddRange(map.ImportedFiles.Files.Select(x => x.FullPath));
            }

            var objectDataJson = map.GetAllObjectData_JSON();
            var deserialized = (JObject)JsonConvert.DeserializeObject(objectDataJson);
            var extensions = new HashSet<string>(fileExtensions.Select(x => x.ToUpper()));
            result.AddRange(deserialized.DescendantsAndSelf().Where(x => x is JValue).Cast<JValue>().Where(x => x.Type == JTokenType.String).Select(x => (string)x.Value).Where(x => extensions.Contains(Path.GetExtension(x).ToUpper())));
            return result;
        }

        [GeneratedRegex(@"^MDLXVERS.*?MODLt.*?([a-zA-Z0-9 _-]+)", RegexOptions.IgnoreCase)]
        protected static partial Regex RegexScan_MDX();

        protected HashSet<string> ScanFilesForExternalFileReferences(List<string> filenames)
        {
            var extensionsToSkip = new HashSet<string>(".mp3,.blp,.tga,.bmp,.jpg,.dds,.wav".Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToUpper()));

            var result = new HashSet<string>();
            Parallel.ForEach(filenames, filename =>
            {
                if (!File.Exists(filename) || extensionsToSkip.Contains(Path.GetExtension(filename).ToUpper()))
                {
                    return;
                }

                _logEvent($"scanning {filename}");
                using (var file = new StreamReader(filename))
                {
                    var line = file.ReadToEnd();

                    var mdxMatch = RegexScan_MDX().Match(line);
                    if (mdxMatch.Success)
                    {
                        result.Add($"{mdxMatch.Groups[1].Value}.mdx".ToUpper());
                    }

                    var fileExtensions = _commonFileExtensions.Aggregate((x, y) => $"{x}|{y}");
                    var matches = Regex.Matches(line, @"([ -~]{1,1000}?)\.(" + fileExtensions + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled).Concat(Regex.Matches(line, @"([ -~]{1,1000})\.(" + fileExtensions + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled)).ToList();
                    foreach (Match match in matches)
                    {
                        var path = match.Groups[1].Value;
                        var ext = match.Groups[2].Value.ToUpper();

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
                            result.Add($"{path.Substring(path.IndexOf("=") + 1)}.{ext}".ToUpper());
                        }

                        path = path.Replace("\\\\", "\\").Trim();
                        result.Add($"{path}.{ext}".ToUpper());

                        var basename = path.Substring(path.LastIndexOf("\\") + 1).Trim();
                        while (basename.Length > 0 && !((basename[0] >= 'A' && basename[0] <= 'Z') || (basename[0] >= 'a' && basename[0] <= 'z')))
                        {
                            basename = basename.Substring(1);
                            result.Add($"{basename}.{ext}".ToUpper());
                        }
                    }

                    var matches2 = Regex.Matches(line, @"([\)\(\\\/a-zA-Z_0-9. -]{1,1000})\.(" + fileExtensions + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    foreach (Match match in matches2)
                    {
                        var path = match.Groups[1].Value;
                        var ext = match.Groups[2].Value.ToUpper();
                        path = path.Replace("\\\\", "\\").Trim();
                        result.Add($"{path}.{ext}".ToUpper());
                    }
                }
            });

            return result;
        }
    }
}
