﻿using CSharpLua;
using IniParser;
using IniParser.Model.Configuration;
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
using War3Net.Common.Extensions;
using War3Net.IO.Mpq;
using System.Numerics;
using FastMDX;
using System.Collections.Concurrent;
using Microsoft.Win32;
using System.Diagnostics;
using War3Net.Build.Import;
using War3Net.IO.Slk;
using Jass2Lua;

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
        protected const string ATTRIB = "Map deprotected by WC3MapDeprotector https://github.com/speige/WC3MapDeprotector\r\n\r\n";
        protected readonly HashSet<string> _commonFileExtensions = new HashSet<string>((new[] { "pcx", "gif", "cel", "dc6", "cl2", "ogg", "smk", "bik", "avi", "lua", "ai", "asi", "ax", "blp", "ccd", "clh", "css", "dds", "dll", "dls", "doo", "exe", "exp", "fdf", "flt", "gid", "html", "ifl", "imp", "ini", "j", "jpg", "js", "log", "m3d", "mdl", "mdx", "mid", "mmp", "mp3", "mpq", "mrf", "pld", "png", "shd", "slk", "tga", "toc", "ttf", "otf", "woff", "txt", "url", "w3a", "w3b", "w3c", "w3d", "w3e", "w3g", "w3h", "w3i", "w3m", "w3n", "w3f", "w3v", "w3z", "w3q", "w3r", "w3s", "w3t", "w3u", "w3x", "wai", "wav", "wct", "wpm", "wpp", "wtg", "wts", "mgv", "mg", "sav" }).Select(x => $".{x.Trim('.')}"), StringComparer.InvariantCultureIgnoreCase);

        protected string _inMapFile;
        protected string _outMapFile;
        public DeprotectionSettings Settings { get; private set; }
        protected readonly Action<string> _logEvent;
        protected DeprotectionResult _deprotectionResult;
        protected ObjectEditor _objectEditor;

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
                Utils.SafeDeleteFile(InstallerListFileName);
                Utils.SafeDeleteFile(Path.Combine(extractedListFileFolder, "listfile.txt"));
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

        protected static string BlankMapFilesPath
        {
            get
            {
                return Path.Combine(Utils.ExeFolderPath, "BlankMapFiles");
            }
        }

        protected static string GameDataFilesPath
        {
            get
            {
                return Path.Combine(Utils.ExeFolderPath, "GameDataFiles");
            }
        }

        protected static string InstallerListFileName
        {
            get
            {

                return Path.Combine(Utils.ExeFolderPath, "listfile.zip");
            }
        }

        protected static string WorkingListFileName
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WC3MapDeprotector", "listfile.txt");
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

        protected readonly string[] ObjectEditor_BaseGameDataFiles_SLKTXTFolders = new[] { "units", "doodads" };
        protected const string ObjectEditor_BaseGameDataFiles_SelectedLocaleFolder = @"_locales\enus.w3mod"; // todo: allow user to select other locales? [will need to set locale in final MPQ]

        protected string ObjectEditorDataFilesFolder
        {
            get
            {
                return Path.Combine(WorkingFolderPath, "ObjectEditorDataFiles");
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

        protected string decode(string encoded)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }

        protected Process LaunchWC3(string mapFileName)
        {
            return Utils.ExecuteCommand(UserSettings.WC3ExePath, $"-launch -nowfpause -loadfile \"{mapFileName}\"");
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
                File.Copy(_inMapFile, tempMapLocation, true);

                var war3mapj = (new[] { JassMapScript.FileName, JassMapScript.FullName }).Select(x => Path.Combine(DiscoveredFilesPath, x)).Where(x => File.Exists(x)).FirstOrDefault();
                if (File.Exists(war3mapj))
                {
                    var deobfuscated = DeObfuscateFourCCJass(Utils.ReadFile_NoEncoding(war3mapj)); // necessary because some international FourCC codes will crash game if not converted to ints
                    var tempScriptFileName = Path.GetTempFileName();
                    Utils.WriteFile_NoEncoding(tempScriptFileName, deobfuscated);
                    MapUtils.RemoveFile(tempMapLocation, Path.Combine(DiscoveredFilesPath, JassMapScript.FileName));
                    MapUtils.RemoveFile(tempMapLocation, Path.Combine(DiscoveredFilesPath, JassMapScript.FullName));
                    MapUtils.AddFile(tempMapLocation, tempScriptFileName, Path.Combine(DiscoveredFilesPath, JassMapScript.FileName));
                    Utils.SafeDeleteFile(tempScriptFileName);
                }

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
                    //wait for process to end so file can unlock
                    Thread.Sleep(15 * 1000);
                    Utils.SafeDeleteFile(tempMapLocation);
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

        protected string RemoveNonVisibleAsciiChars(string text)
        {
            return new string(text.Where(x => x >= ' ' && x <= '~').ToArray());
        }

        public async Task<DeprotectionResult> Deprotect()
        {
            _deprotectionResult = new DeprotectionResult();
            var oldInMapFile = _inMapFile;
            _inMapFile = RemoveNonVisibleAsciiChars(_inMapFile);
            if (_inMapFile != oldInMapFile)
            {
                _inMapFile = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(_inMapFile));
                File.Copy(oldInMapFile, _inMapFile);
            }

            var oldOutMapFile = _outMapFile;
            _outMapFile = RemoveNonVisibleAsciiChars(_outMapFile);
            if (oldOutMapFile != _outMapFile)
            {
                _deprotectionResult.WarningMessages.Add("non-ascii character in output file name. changed to: " + _outMapFile);
            }

            while (WorldEditor.GetRunningInstanceOfEditor() != null)
            {
                MessageBox.Show("A running \"World Editor.exe\" process has been detected. Please close it while performing deprotection");

                var process = WorldEditor.GetRunningInstanceOfEditor();
                if (process != null)
                {
                    DialogResult dialogResult = MessageBox.Show($"Running Editor still detected. Force close? You will lose any unsaved work. The Exe that will be closed is: {UserSettings.WorldEditExePath}", "Force Close Editor?", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        process.Kill();
                    }
                }
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

            _deprotectionResult.WarningMessages.Add($"NOTE: This tool is a work in progress. Deprotection does not work perfectly on every map. You will need to make fixes manually. Click the \"Help\" button for instructions. You can also get help from my YouTube channel or report defects by clicking the \"Bug Report\" button.");

            var blankMapFilesZip = Path.Combine(Utils.ExeFolderPath, "BlankMapFiles_2.0.0.22389.zip");
            if (!Directory.Exists(BlankMapFilesPath) && File.Exists(blankMapFilesZip))
            {
                ZipFile.ExtractToDirectory(blankMapFilesZip, BlankMapFilesPath, true);
            }

            var gameDataFilesZip = Path.Combine(Utils.ExeFolderPath, "GameDataFiles_2.0.0.22389.zip");
            if (!Directory.Exists(GameDataFilesPath) && File.Exists(gameDataFilesZip))
            {
                ZipFile.ExtractToDirectory(gameDataFilesZip, GameDataFilesPath, true);
            }

            foreach (var folder in ObjectEditor_BaseGameDataFiles_SLKTXTFolders)
            {
                CopyDirectory(Path.Combine(GameDataFilesPath, folder), Path.Combine(ObjectEditorDataFilesFolder, folder));
                CopyDirectory(Path.Combine(GameDataFilesPath, ObjectEditor_BaseGameDataFiles_SelectedLocaleFolder, folder), Path.Combine(ObjectEditorDataFilesFolder, folder));
            }
            _objectEditor = new ObjectEditor(ObjectEditorDataFilesFolder);

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

            Map map_ObjectDataCollectionOnly;
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
                if (inMPQArchive.ShouldKeepScanningForUnknowns)
                {
                    _deprotectionResult.CountOfProtectionsFound++;
                }

                DeleteAttributeListSignatureFiles();

                if (inMPQArchive.ShouldKeepScanningForUnknowns)
                {
                    inMPQArchive.ProcessDefaultListFile();
                }

                _logEvent($"Unknown file count: {inMPQArchive.UnknownFileCount}");

                if (!Map.TryOpen(DiscoveredFilesPath, out map_ObjectDataCollectionOnly, MapFiles.AbilityObjectData | MapFiles.BuffObjectData | MapFiles.DestructableObjectData | MapFiles.DoodadObjectData | MapFiles.ItemObjectData | MapFiles.UnitObjectData | MapFiles.UpgradeObjectData))
                {
                    DebugSettings.Warn("Can't open map object editor files");
                }

                var slkFiles = Directory.GetFiles(DiscoveredFilesPath, "*.slk", SearchOption.AllDirectories).ToList();
                var txtFiles = Directory.GetFiles(DiscoveredFilesPath, "*.txt", SearchOption.AllDirectories).ToList();

                foreach (var fileName in slkFiles.Concat(txtFiles).ToList())
                {
                    /*
                        NOTE: TXT files must come last for proper merging of ObjectData
                            Merging only overwrites PropertyValues, not Parent or ObjectDataType
                            1) TXT PropertyValues (like Name) have precedence. Example, SLK may have hpea:name=custom_hpea and TXT may have [hpea]:Name=Peasant.
                            2) SLK Parent takes precedence. (only AbilityData.SLK has it, no other SLK/TXT does. Otherwise, we predict best Parent)
                            3) SLK ObjectDataType takes precence. TXT can combine multiple object types (Example: CommonAbilityStrings.txt & ItemAbilityStrings.txt have FourCC for Abilities & Buffs).
                    */
                    if (!File.Exists(Path.Combine(ObjectEditorDataFilesFolder, RemovePathPrefix(fileName, DiscoveredFilesPath))))
                    {
                        continue;
                    }

                    _logEvent($"Parsing ObjectEditor data file: {fileName}");

                    _objectEditor.ImportObjectDataCollectionFromFile(fileName);
                    MoveExtractedFileToDeletedFolder(fileName);
                }
                _logEvent($"Cleaning up imported ObjectEditor data");
                _objectEditor.RepairInvalidData();

                _objectEditor.ImportObjectDataCollectionFromMap(map_ObjectDataCollectionOnly);
                _objectEditor.ForceUnitsPlaceableInEditor();

                _logEvent($"Exporting ObjectEditor data to WorldEditor binary format");
                _objectEditor.SetWar3NetObjectFiles(map_ObjectDataCollectionOnly);

                foreach (var file in map_ObjectDataCollectionOnly.GetObjectDataFiles())
                {
                    var fileName = Path.Combine(DiscoveredFilesPath, file.FileName);
                    MoveExtractedFileToDeletedFolder(fileName);

                    using (var stream = File.Create(fileName))
                    {
                        file.MpqStream.CopyTo(stream);
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
                    var map = SetNativeFiles(DiscoveredFilesPath);
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

                    var discoveredFilePaths = Directory.GetFiles(DiscoveredFilesPath, "*", SearchOption.AllDirectories).ToList();
                    var unknownFilePaths = inMPQArchive.GetUnknownPseudoFileNames().Select(x => Path.Combine(UnknownFilesPath, x)).ToList();
                    var allExtractedFiles = discoveredFilePaths.Concat(unknownFilePaths).ToList();
                    Parallel.ForEach(allExtractedFiles, fileName =>
                    {
                        if (!File.Exists(fileName))
                        {
                            return;
                        }

                        var fileContents = File.ReadAllBytes(fileName);
                        var extension = Path.GetExtension(fileName);
                        var md5 = fileContents.CalculateMD5();
                        if (alreadyScannedMD5WithFileExtension.TryGetValue(md5, out var previousExtension) && extension.Equals(previousExtension, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return;
                        }

                        alreadyScannedMD5WithFileExtension[md5] = extension;

                        _logEvent($"Searching {fileName} ...");
                        var scannedFiles = ParseFileToDetectPossibleUnknowns(fileContents, Path.GetExtension(fileName), unknownFileExtensions);
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
                        _deprotectionResult.WarningMessages.Add($"Could not verify file extension for {fileName} - It's possible it's encrypted and was recovered under a fake file name. See \"Unknown Files\" in Help document");
                    }
                }

                if (inMPQArchive.ShouldKeepScanningForUnknowns && !DebugSettings.BulkDeprotect)
                {
                    LiveGameScanForUnknownFiles(inMPQArchive);
                }

                if (Settings.BruteForceUnknowns && inMPQArchive.ShouldKeepScanningForUnknowns)
                {
                    BruteForceUnknownFileNames(inMPQArchive);
                }

                _deprotectionResult.UnknownFileCount = inMPQArchive.UnknownFileCount;
                if (_deprotectionResult.UnknownFileCount > 0)
                {
                    _deprotectionResult.WarningMessages.Add($"WARNING: {_deprotectionResult.UnknownFileCount} files have unresolved names. See \"Unknown Files\" in Help document");
                }
            }

            PatchW3I();

            //These are probably protected, but the only way way to verify if they aren't is to parse the script (which is probably obfuscated), but if we can sucessfully parse, then we can just re-generate them to be safe.
            MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, MapUnits.FileName));
            MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, MapCustomTextTriggers.FileName));
            MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, MapTriggers.FileName));
            MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, MapRegions.FileName));
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
            File.Copy(Path.Combine(BlankMapFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x33\x77\x69"), Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x74\x67\x61"), true);
            var replace2 = "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78";
            while (File.Exists(Path.Combine(DiscoveredFilesPath, replace2)))
            {
                replace2 = replace2.Insert(replace2.Length - 4, "_old");
            }
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78")))
            {
                File.Move(Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78"), Path.Combine(DiscoveredFilesPath, replace2), true);
            }
            File.Copy(Path.Combine(BlankMapFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x33\x77\x6D"), Path.Combine(DiscoveredFilesPath, "\x4C\x6F\x61\x64\x69\x6E\x67\x53\x63\x72\x65\x65\x6E\x2E\x6D\x64\x78"), true);

            var scriptFiles = Directory.GetFiles(DiscoveredFilesPath, "war3map.j", SearchOption.AllDirectories).Union(Directory.GetFiles(DiscoveredFilesPath, "war3map.lua", SearchOption.AllDirectories)).ToList();
            foreach (var scriptFile in scriptFiles)
            {
                var basePathScriptFileName = Path.Combine(DiscoveredFilesPath, Path.GetFileName(scriptFile));
                if (File.Exists(basePathScriptFileName) && !string.Equals(scriptFile, basePathScriptFileName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (Utils.ReadFile_NoEncoding(scriptFile) != Utils.ReadFile_NoEncoding(basePathScriptFileName))
                    {
                        _deprotectionResult.CriticalWarningCount++;
                        _deprotectionResult.WarningMessages.Add("WARNING: Multiple possible script files found. Please review TempFiles to see which one is correct and copy/paste directly into trigger editor.");
                        _deprotectionResult.WarningMessages.Add($"TempFilePath: {WorkingFolderPath}");
                    }
                }
                File.Move(scriptFile, basePathScriptFileName, true);
                _logEvent($"Moving '{scriptFile}' to '{basePathScriptFileName}'");
            }

            File.Copy(Path.Combine(BlankMapFilesPath, "war3map.3ws"), Path.Combine(DiscoveredFilesPath, "\x77\x61\x72\x33\x6D\x61\x70\x2E\x77\x61\x76"), true);

            string jassScript = null;
            string luaScript = null;
            ScriptMetaData scriptMetaData = null;
            if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.j")))
            {
                jassScript = $"// {ATTRIB}{Utils.ReadFile_NoEncoding(Path.Combine(DiscoveredFilesPath, "war3map.j"))}";
                try
                {
                    jassScript = DeObfuscateJassScript(map_ObjectDataCollectionOnly, jassScript);
                    scriptMetaData = DecompileJassScriptMetaData(jassScript);
                }
                catch { }
            }

            if (File.Exists(Path.Combine(DiscoveredFilesPath, "war3map.lua")))
            {
                _deprotectionResult.WarningMessages.Add("WARNING: This map was built using Lua instead of Jass. Deprotection of Lua maps is not fully supported yet. See \"Object Manager\" in Help document");
                luaScript = DeObfuscateLuaScript($"-- {ATTRIB}{Utils.ReadFile_NoEncoding(Path.Combine(DiscoveredFilesPath, "war3map.lua"))}");
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
                using (var decompiledFiles = new DisposableCollection<MpqKnownFile>(scriptMetaData.ConvertToFiles().ToList().AsReadOnly()))
                {
                    foreach (var file in decompiledFiles.Collection)
                    {
                        if (!File.Exists(Path.Combine(DiscoveredFilesPath, file.FileName)))
                        {
                            _deprotectionResult.CountOfProtectionsFound++;
                        }

                        SaveDecompiledArchiveFile(file);
                    }
                }
            }

            if (!File.Exists(Path.Combine(DiscoveredFilesPath, MapUnits.FileName)))
            {
                _deprotectionResult.CountOfProtectionsFound++;
                File.Copy(Path.Combine(BlankMapFilesPath, MapUnits.FileName), Path.Combine(DiscoveredFilesPath, MapUnits.FileName), true);
                _deprotectionResult.CriticalWarningCount++;
                _deprotectionResult.WarningMessages.Add("WARNING: war3mapunits.doo could not be recovered. See \"Object Manager\" in Help document");
            }

            if (!File.Exists(Path.Combine(DiscoveredFilesPath, MapTriggers.FileName)))
            {
                _deprotectionResult.CountOfProtectionsFound++;
                File.Copy(Path.Combine(BlankMapFilesPath, MapTriggers.FileName), Path.Combine(DiscoveredFilesPath, MapTriggers.FileName), true);
                MoveExtractedFileToDeletedFolder(Path.Combine(DiscoveredFilesPath, MapCustomTextTriggers.FileName));
                _deprotectionResult.CriticalWarningCount++;
                _deprotectionResult.WarningMessages.Add("WARNING: triggers could not be recovered. Please review TempFiles to see which one is correct and copy/paste directly into trigger editor.");
                _deprotectionResult.WarningMessages.Add($"TempFilePath: {WorkingFolderPath}");
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
                _deprotectionResult.CriticalWarningCount++;
                _deprotectionResult.WarningMessages.Add("WARNING: Failed to upgrade to latest reforged file format. Map may still load correctly in WorldEditor, but not very likely. Please click \"Bug Report\" and send me the map file to research");
            }

            AnnotateScriptFile();

            BuildW3X(_outMapFile, DiscoveredFilesPath, Directory.GetFiles(DiscoveredFilesPath, "*", SearchOption.AllDirectories).ToList());

            //todo: open final file in WorldEditor with "Local Files" to find remaining textures/3dModels?

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
                var script = Utils.ReadFile_NoEncoding(Path.Combine(DiscoveredFilesPath, "war3map.j"));
                var blz = Regex_JassScriptInitBlizzard().Match(script);
                if (blz.Success)
                {
                    var bits = new byte[] { 0b_00001101, 0b_00001010, 0b_01100011, 0b_01100001, 0b_01101100, 0b_01101100, 0b_00100000, 0b_01000100, 0b_01101001, 0b_01110011, 0b_01110000, 0b_01101100, 0b_01100001, 0b_01111001, 0b_01010100, 0b_01100101, 0b_01111000, 0b_01110100, 0b_01010100, 0b_01101111, 0b_01000110, 0b_01101111, 0b_01110010, 0b_01100011, 0b_01100101, 0b_00101000, 0b_01000111, 0b_01100101, 0b_01110100, 0b_01010000, 0b_01101100, 0b_01100001, 0b_01111001, 0b_01100101, 0b_01110010, 0b_01110011, 0b_01000001, 0b_01101100, 0b_01101100, 0b_00101000, 0b_00101001, 0b_00101100, 0b_00100000, 0b_00100010, 0b_01100100, 0b_00110011, 0b_01110000, 0b_01110010, 0b_00110000, 0b_01110100, 0b_00110011, 0b_01100011, 0b_01110100, 0b_00110011, 0b_01100100, 0b_00100010, 0b_00101001, 0b_00001101, 0b_00001010 };
                    for (var idx = 0; idx < bits.Length; ++idx)
                    {
                        script = script.Insert(blz.Index + blz.Length + idx, ((char)bits[idx]).ToString());
                    }
                }

                Utils.WriteFile_NoEncoding(Path.Combine(DiscoveredFilesPath, "war3map.j"), script);
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
            var config = ast.body.Where(x => x.type == LuaASTType.FunctionDeclaration && x.name == "config").FirstOrDefault();
            var main = ast.body.Where(x => x.type == LuaASTType.FunctionDeclaration && x.name == "main").FirstOrDefault();

            return luaScript;
        }

        protected void CopyDirectory(string sourcePath, string destinationPath)
        {
            foreach (var localFile in Directory.GetFiles(sourcePath))
            {
                var newPath = Path.Combine(destinationPath, RemovePathPrefix(localFile, sourcePath));
                Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                File.Copy(localFile, newPath);
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

        protected void UpgradeToLatestFileFormats()
        {
            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var tempMapFileName = Path.Combine(WorkingFolderPath, "fileFormats.w3x");
            BuildW3X(tempMapFileName, DiscoveredFilesPath, nativeFileNames.Select(x => @$"{DiscoveredFilesPath}\{x}").ToList());
            var map = War3NetExtensions.OpenMap_WithoutCorruptingInternationalCharactersInScript(tempMapFileName);

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

            var objectDataCollection = map.GetObjectDataCollection_War3Net();
            foreach ((var dataType, var objectData) in objectDataCollection)
            {
                objectData.FormatVersion = War3Net.Build.Object.ObjectDataFormatVersion.v3;
                var combinedObjectData = objectData.OriginalOverrides.Concat(objectData.CustomOverrides).ToList();
                foreach (var data in combinedObjectData)
                {
                    data.Unk = new List<int>() { 0 };
                }
            }

            using (var mapFiles = new DisposableCollection<MpqKnownFile>(map.GetAllNativeFiles().AsReadOnly()))
            {
                foreach (var file in mapFiles.Collection)
                {
                    using (file)
                    {
                        SaveDecompiledArchiveFile(file);
                    }
                }
            }

            Utils.SafeDeleteFile(tempMapFileName);
        }

        protected string GenerateLiveGameScanningMap()
        {
            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var baseFileNames = Directory.GetFiles(BlankMapFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, BlankMapFilesPath)).ToList();

            var allFiles = nativeFileNames.Concat(baseFileNames).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            var liveGameScanningFolder = Path.Combine(TempFolderPath, "liveGameScanning");
            foreach (var file in allFiles)
            {
                if (MapUtils.WorldEditorMapFileNames.Contains(file))
                {
                    continue;
                }

                var fileName = Path.Combine(DiscoveredFilesPath, file);
                var tempFileName = Path.Combine(liveGameScanningFolder, file);

                if (File.Exists(fileName))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(tempFileName));
                    File.Copy(fileName, tempFileName, true);
                }
                else 
                {
                    fileName = Path.Combine(BlankMapFilesPath, file);
                    if (File.Exists(fileName))
                    {
                        File.Copy(fileName, tempFileName, true);
                    }
                }
            }

            var mapFileName = Path.Combine(liveGameScanningFolder, "map.w3x");
            BuildW3X(mapFileName, liveGameScanningFolder, allFiles.Select(x => @$"{liveGameScanningFolder}\{x}").Where(x => File.Exists(x)).ToList());

            if (!MapUtils.IsLuaMap(mapFileName))
            {
                var luaMapFileName = Path.Combine(liveGameScanningFolder, "map_lua.w3x");
                MapUtils.ConvertJassToLua(mapFileName, luaMapFileName);
                mapFileName = luaMapFileName;
            }

            //todo: add lua introspection code
            //prevent quitting due to single-player (or for any reason)
            //monkey patch all functions to log in/out params
            //monitor all functions for potential encryption & relay messages to main process
            //relay any native functions during config/main for ObjectManager decompilation
            //prefix all Preload with a subdirectory to avoid overwriting saved games
            //execute all _G functions with no params

            return mapFileName;
        }

        protected void RepairW3XNativeFilesInEditor()
        {
            //NOTE: minor corruption can be repaired by opening map & saving, but need to remove all models/etc from w3x 1st or it will crash editor.
            //todo: double-check that war3map.wts doesn't have any lost trigger data after saving with baseMap triggers

            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var baseFileNames = Directory.GetFiles(BlankMapFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, BlankMapFilesPath)).ToList();
            var notRepairableFiles = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "war3map.j", ImportedFiles.MapFileName, MapCustomTextTriggers.FileName, MapTriggers.FileName };

            var allFiles = nativeFileNames.Concat(baseFileNames).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            var repairNativesFolder = Path.Combine(TempFolderPath, "repairNatives");
            foreach (var file in allFiles)
            {
                var baseFileName = Path.Combine(BlankMapFilesPath, file);
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

            var map = War3NetExtensions.OpenMap_WithoutCorruptingInternationalCharactersInScript(tempMapFileName);
            using (var mapFiles = new DisposableCollection<MpqKnownFile>(map.GetAllNativeFiles().AsReadOnly()))
            {
                foreach (var file in mapFiles.Collection)
                {
                    using (file)
                    {
                        if (!notRepairableFiles.Contains(file.FileName))
                        {
                            SaveDecompiledArchiveFile(file);
                        }
                    }
                }
            }

            Utils.SafeDeleteFile(tempMapFileName);
        }

        protected void CorrectUnitPositionZOffsets()
        {
            var nativeFileNames = Directory.GetFiles(DiscoveredFilesPath, "*.*", SearchOption.AllDirectories).Select(x => RemovePathPrefix(x, DiscoveredFilesPath)).Where(x => StormMPQArchiveExtensions.IsInDefaultListFile(x)).ToList();
            var tempMapFileName = Path.Combine(WorkingFolderPath, "unitPositionZ.w3x");
            BuildW3X(tempMapFileName, DiscoveredFilesPath, nativeFileNames.Select(x => @$"{DiscoveredFilesPath}\{x}").ToList());
            var map = War3NetExtensions.OpenMap_WithoutCorruptingInternationalCharactersInScript(tempMapFileName);

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

            using (var mapFiles = new DisposableCollection<MpqKnownFile>(map.GetAllNativeFiles().AsReadOnly()))
            {
                foreach (var file in mapFiles.Collection)
                {
                    using (file)
                    {
                        SaveDecompiledArchiveFile(file);
                    }
                }
            }

            Utils.SafeDeleteFile(tempMapFileName);
        }

        [GeneratedRegex(@"\s+call\s+InitBlizzard\s*\(\s*\)\s*", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_JassScriptInitBlizzard();

        protected void BuildW3X(string fileName, string baseFolder, List<string> filesToInclude)
        {
            _logEvent("Building map archive...");
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
            using (var writeStream = File.Create(tempFileName))
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

                                                if (!archive.ShouldKeepScanningForUnknowns)
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
        [GeneratedRegex(@"'([^']*)'", RegexOptions.Multiline)]
        protected static partial Regex Regex_ScriptPossibleFourCC();

        protected string DeObfuscateFourCC(string script, string prefix, string suffix)
        {
            var result = Regex_ScriptHexObfuscatedFourCC().Replace(script, x =>
            {
                var intValue = Convert.ToInt32(x.Value.Substring(1), 16);
                var rawCode = intValue.ToFourCC();

                if (!_objectEditor.ObjectIDExists(rawCode) || rawCode.Length != 4 || rawCode.Any(x => x < ' ' || x > '~'))
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
                if (!_objectEditor.ObjectIDExists(rawCode) || rawCode.Length != 4 || rawCode.Any(x => x < ' ' || x > '~'))
                {
                    return intValue.ToString();
                }
                return $"{prefix}{rawCode}{suffix}";
            });

            result = Regex_ScriptPossibleFourCC().Replace(result, x =>
            {
                var code = x.Groups[1].Value;
                if (code.Length == 4 && code.Any(x => x < ' ' || x > '~'))
                {
                    var intValue = code.FromFourCCToInt();
                    return intValue.ToString();
                }

                return "'" + code + "'";
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

        protected void SplitUserDefinedAndAutoGeneratedGlobalVariableNames(string jassScript, out List<string> userDefinedGlobals, out List<string> globalGenerateds)
        {
            var jassParsed = JassSyntaxFactory.ParseCompilationUnit(jassScript);
            var decompiler = new JassScriptDecompiler(jassParsed);

            var globals = jassParsed.Declarations.OfType<JassGlobalDeclarationListSyntax>().SelectMany(x => x.Globals).Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>().ToList();

            var probablyUserDefined = new HashSet<string>();
            var mainStatements = decompiler.GetFunctionStatements_EnteringCalls("main");
            var initBlizzardIndex = mainStatements.FindIndex(x => x.GetChildren_RecursiveDepthFirst().OfType<IInvocationSyntax>().Any(y => string.Equals(y.IdentifierName.Name, "InitBlizzard", StringComparison.InvariantCultureIgnoreCase)));

            //NOTE: Searches for contents of InitGlobals() by structure so it can still work on obfuscated code
            var idx = initBlizzardIndex;
            var initGlobalsIndex = initBlizzardIndex + 1;
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
            foreach (var udgStatement in initGlobalsStatements.OfType<JassSetStatementSyntax>())
            {
                probablyUserDefined.Add(udgStatement.IdentifierName.Name);
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

        [GeneratedRegex(@"set\s+(\w+)\s*=\s*0[\r\n\s]+loop[\r\n\s]+exitwhen\s*\(\1\s*>\s*([0-9]+)\)[\r\n\s]+set\s+(\w+)\[\1\]\s*=\s*[^\r\n]*[\r\n\s]+set\s+\1\s*=\s*\1\s*\+\s*1[\r\n\s]+endloop[\r\n\s]+", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
        protected static partial Regex Regex_GetArraySize();

        protected ScriptMetaData DecompileJassScriptMetaData_Internal(JassCompilationUnitSyntax jassParsed, out DecompilationContext decompilationMetaData)
        {
            var result = new ScriptMetaData();

            var map = SetNativeFiles(DiscoveredFilesPath);
            result.Info = map?.Info;
            result.TriggerStrings = map?.TriggerStrings;
            result.CustomTextTriggers = new MapCustomTextTriggers(MapCustomTextTriggersFormatVersion.v1, MapCustomTextTriggersSubVersion.v4) { GlobalCustomScriptCode = new CustomTextTrigger() { Code = "" }, GlobalCustomScriptComment = "Deprotected global script. Please ensure JassHelper:EnableJassHelper and JassHelper:EnableVJass settings are turned on. Auto-generated code has been renamed with a suffix of _old and/or commented out. There may be compiler errors or bugs that need to be resolved manually." };

            var mapInfoFormatVersion = Enum.GetValues(typeof(MapInfoFormatVersion)).Cast<MapInfoFormatVersion>().OrderBy(x => x == map?.Info?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault();
            var useNewFormat = map?.Info?.FormatVersion >= MapInfoFormatVersion.v28;

            try
            {
                _logEvent("Decompiling ObjectManager data from war3map script file");
                var options = new DecompileOptions()
                {
                    mapSoundsFormatVersion = Enum.GetValues(typeof(MapSoundsFormatVersion)).Cast<MapSoundsFormatVersion>().OrderBy(x => x == map?.Sounds?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(),
                    mapCamerasFormatVersion = Enum.GetValues(typeof(MapCamerasFormatVersion)).Cast<MapCamerasFormatVersion>().OrderBy(x => x == map?.Cameras?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(),
                    mapCamerasUseNewFormat = useNewFormat,
                    mapRegionsFormatVersion = Enum.GetValues(typeof(MapRegionsFormatVersion)).Cast<MapRegionsFormatVersion>().OrderBy(x => x == map?.Regions?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(),
                    mapWidgetsFormatVersion = Enum.GetValues(typeof(MapWidgetsFormatVersion)).Cast<MapWidgetsFormatVersion>().OrderBy(x => x == map?.Units?.FormatVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(),
                    mapWidgetsSubVersion = Enum.GetValues(typeof(MapWidgetsSubVersion)).Cast<MapWidgetsSubVersion>().OrderBy(x => x == map?.Units?.SubVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault(),
                    mapWidgetsUseNewFormat = useNewFormat,
                    specialDoodadVersion = Enum.GetValues(typeof(SpecialDoodadVersion)).Cast<SpecialDoodadVersion>().OrderBy(x => x == map?.Doodads?.SpecialDoodadVersion ? 0 : 1).ThenByDescending(x => x).FirstOrDefault()
                };
                var decompiler = new JassScriptDecompiler(jassParsed, options, map?.Info);
                var decompiledMap = decompiler.DecompileObjectManagerData();

                result.Cameras = decompiledMap.Cameras;
                result.Regions = decompiledMap.Regions;
                result.Sounds = decompiledMap.Sounds;
                result.Units = decompiledMap.Units;
                //result.Doodads = decompiledMap.Doodads; // No decompiler for Doodads yet
                result.Doodads = map.Doodads;

                decompilationMetaData = decompiler.Context;

                if (!result.Units.Units.Any(x => x.TypeId != "sloc".FromRawcode()))
                {
                    _deprotectionResult.WarningMessages.Add("WARNING: Only unit start locations could be recovered. See \"Object Manager\" in Help document");
                }
            }
            catch
            {
                _deprotectionResult.WarningMessages.Add("WARNING: Unable to decompiled ObjectManager data");
                decompilationMetaData = null;
                return null;
            }

            SetTriggersFromDecompiledJass(result, map, jassParsed, decompilationMetaData);
            return result;
        }

        public void SetTriggersFromDecompiledJass(ScriptMetaData result, Map map, JassCompilationUnitSyntax jassParsed, DecompilationContext decompilationMetaData)
        {
            //todo: review all non-commented lines in _old functions after deprotection to find pieces I'm not decompiling (example: "CreateAllDestructables", "InitTechTree")

            var jassScript = jassParsed.RenderScriptAsString();
            var clonedJassParsed = JassSyntaxFactory.ParseCompilationUnit(jassScript);

            var allChildren = jassParsed.GetChildren_RecursiveDepthFirst().ToArray();
            var allClonedChildren = clonedJassParsed.GetChildren_RecursiveDepthFirst().ToArray();

            if (allChildren.Length == allClonedChildren.Length)
            {
                var toCloneMapping = allChildren.Select((value, index) => new KeyValuePair<IJassSyntaxToken, IJassSyntaxToken>(value, allClonedChildren[index])).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.First().Value);

                //comments out anything that would be auto-generated by editor on save (CreateUnit/etc)
                var astNodeToParent = JassTriggerExtensions.JassAST_CreateChildToParentMapping(jassParsed);
                var allDecompiledFromStatements = decompilationMetaData.HandledStatements.ToList();
                foreach (var statement in allDecompiledFromStatements)
                {
                    if (!astNodeToParent.TryGetValue(statement, out var parent))
                    {
                        continue;
                    }

                    var playerInvocation = statement.GetChildren_RecursiveDepthFirst().OfType<IInvocationSyntax>().Where(x => x.IdentifierName.Name == "Player").FirstOrDefault();
                    if (playerInvocation != null)
                    {
                        if (astNodeToParent.TryGetValue(playerInvocation, out var playerInvocationParent) && playerInvocationParent is JassEqualsValueClauseSyntax)
                        {
                            continue;
                        }
                    }

                    var clonedStatement = toCloneMapping[statement];
                    var clonedParent = toCloneMapping[parent];

                    if (clonedStatement is JassLocalVariableDeclarationStatementSyntax localSyntax && localSyntax.Declarator is JassVariableDeclaratorSyntax variableSyntax && variableSyntax.Value != null)
                    {
                        var newLocalSyntax = new JassLocalVariableDeclarationStatementSyntax(new JassVariableDeclaratorSyntax(variableSyntax.Type, variableSyntax.IdentifierName, null));
                        var newSetSyntax = new JassSetStatementSyntax(variableSyntax.IdentifierName, null, variableSyntax.Value);

                        using (var writer = new StringWriter())
                        {
                            var renderer = new JassRenderer(writer);
                            renderer.Render(newSetSyntax);
                            var setStatementAsString = writer.GetStringBuilder().ToString();

                            JassTriggerExtensions.JassASTNode_ReplaceChild(clonedParent, clonedStatement, newLocalSyntax, new JassCommentSyntax(setStatementAsString));
                        }
                    }
                    else
                    {
                        using (var writer = new StringWriter())
                        {
                            var renderer = new JassRenderer(writer);
                            renderer.Render(statement);
                            var statementAsString = writer.GetStringBuilder().ToString();

                            JassTriggerExtensions.JassASTNode_ReplaceChild(clonedParent, clonedStatement, new JassCommentSyntax(statementAsString));
                        }
                    }
                }

                jassScript = clonedJassParsed.RenderScriptAsString();
            }
            else
            {
                _deprotectionResult.WarningMessages.Add("Decompiled ObjectManager Triggers could not be commented out due to bug in Jass Pidgin parser. Please click \"Bug Report\" button and upload the w3x file.");
                JassTriggerExtensions.FindFirstMismatchedChildDueToJassPidginParsingBug(jassParsed, clonedJassParsed);
            }

            //todo: re-code this string manipulation to use AST
            var lines = jassScript.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

            var functionDeclarations = lines.Select((x, y) => new { lineIdx = y, match = JassTriggerExtensions.Regex_JassFunctionDeclaration().Match(x) }).Where(x => x.match.Success).GroupBy(x => x.match.Groups[1].Value).ToDictionary(x => x.Key, x => x.ToList());
            var functionCalls = lines.Select((x, y) => new { lineIdx = y, match = JassTriggerExtensions.Regex_JassFunctionCall().Match(x) }).Where(x => x.match.Success).GroupBy(x => x.match.Groups[1].Value).ToDictionary(x => x.Key, x => x.ToList());
            var functions = functionDeclarations.Keys.Concat(functionCalls.Keys).ToHashSet();

            var unitDropItemNatives = result.Units?.Units?.Where(x => x.HasItemTable()).Select(x => x.GetDropItemsFunctionName(x.CreationNumber)).ToList() ?? new List<string>();
            var destructableDropItemNatives = result.Doodads?.Doodads?.Where(x => x.HasItemTable()).Select(x => x.GetDropItemsFunctionName(x.CreationNumber)).ToList() ?? new List<string>();
            //var specialDestructableDropItemNatives = result.Destructables?.SpecialDoodads?.Select(x => x.GetDropItemsFunctionName()).ToList();
            var allAutoGenerateds = JassTriggerExtensions.AutoGeneratedEditorFunctions.Concat(unitDropItemNatives).Concat(destructableDropItemNatives).ToList();
            var nativeEditorFunctionIndexes = new Dictionary<string, Tuple<int, int>>();
            var nativeEditorFunctionsRenamed = new Dictionary<string, string>();
            foreach (var nativeEditorFunction in allAutoGenerateds)
            {
                var renamed = nativeEditorFunction;
                do
                {
                    renamed += "_old";
                } while (functions.Contains(renamed));

                nativeEditorFunctionsRenamed[nativeEditorFunction] = renamed;

                if (functionDeclarations.TryGetValue(nativeEditorFunction, out var declarationMatches))
                {
                    foreach (var declaration in declarationMatches)
                    {
                        lines[declaration.lineIdx] = lines[declaration.lineIdx].Replace(nativeEditorFunction, renamed);
                    }
                }

                if (functionCalls.TryGetValue(nativeEditorFunction, out var callMatches))
                {
                    foreach (var call in callMatches)
                    {
                        lines[call.lineIdx] = lines[call.lineIdx].Replace(nativeEditorFunction, renamed);
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

                    var match = JassTriggerExtensions.Regex_ParseJassVariableDeclaration().Match(globalLine);
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

            var triggerItemIdx = 0;
            var rootCategoryItemIdx = triggerItemIdx++;
            var triggersCategoryItemIdx = triggerItemIdx++;

            result.Triggers = new MapTriggers(MapTriggersFormatVersion.v7, MapTriggersSubVersion.v4) { GameVersion = 2 };
            result.Triggers.TriggerItems.Add(new TriggerCategoryDefinition(TriggerItemType.RootCategory) { Id = rootCategoryItemIdx, ParentId = -1, Name = "script.w3x" });

            result.Triggers.TriggerItems.Add(new TriggerCategoryDefinition() { Id = triggersCategoryItemIdx, ParentId = rootCategoryItemIdx, Name = "Triggers", IsExpanded = true });
            var mainRenamed = nativeEditorFunctionsRenamed["main"];
            result.Triggers.TriggerItems.Add(new TriggerDefinition() { Id = triggerItemIdx++, ParentId = triggersCategoryItemIdx, Name = "MainDeprotected", Functions = new List<TriggerFunction>() { new TriggerFunction() { IsEnabled = true, Type = TriggerFunctionType.Event, Name = "MapInitializationEvent" }, new TriggerFunction() { IsEnabled = true, Type = TriggerFunctionType.Action, Name = "CustomScriptCode", Parameters = new List<TriggerFunctionParameter>() { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = $"call {mainRenamed}()" } } } }, IsInitiallyOn = true, IsEnabled = true, RunOnMapInit = true, Description = $"Call {mainRenamed} which was extracted from protected map in case it had extra code that failed to decompile into GUI" });

            var units = decompilationMetaData.GetAll<UnitData>().ToList();
            if (units.Count > 0)
            {
                result.Triggers ??= new MapTriggers(MapTriggersFormatVersion.v7, MapTriggersSubVersion.v4) { GameVersion = 2 };
                var maxTriggerItemId = result.Triggers.TriggerItems?.Any() == true ? result.Triggers.TriggerItems.Select(x => x.Id).Max() + 1 : 0;
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

                var emptyVariableTrigger = new TriggerDefinition() { Description = "Disabled GUI trigger with fake code, just to convert ObjectManager units/items/cameras to global generated variables", Name = "GlobalGeneratedObjectManagerVariables", ParentId = category.Id, IsEnabled = true, IsInitiallyOn = false };
                var variables = (result.Units?.Units?.Select(x => x.GetVariableName()).ToList() ?? new List<string>()).Concat(result.Cameras?.Cameras?.Select(x => x.GetVariableName()).ToList() ?? new List<string>()).Concat(map.Doodads?.Doodads.Select(x => x.GetVariableName()).ToList() ?? new List<string>()).ToList();
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
                    }
                    else if (isItem)
                    {
                        var triggerFunction = new TriggerFunction() { Name = "SetItemVisibleBJ", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Preset, Value = "ShowHideShow" } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                    }
                    else if (variable.StartsWith("gg_cam_"))
                    {
                        var triggerFunction = new TriggerFunction() { Name = "CameraSetupApplyForPlayer", Type = TriggerFunctionType.Action, IsEnabled = true };
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Preset, Value = "CameraApplyNoPan" } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Variable, Value = variable } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.Preset, Value = "PlayerNP" } });
                        triggerFunction.Parameters.AddRange(new[] { new TriggerFunctionParameter() { Type = TriggerFunctionParameterType.String, Value = "0" } });
                        emptyVariableTrigger.Functions.Add(triggerFunction);
                    }
                }
                result.Triggers.TriggerItems.Add(emptyVariableTrigger);
            }

            jassScript = JassSyntaxFactory.ParseCompilationUnit(jassScript).RenderScriptAsString();
            result.CustomTextTriggers.GlobalCustomScriptCode.Code = jassScript.Replace("%", "%%");
        }

        protected ScriptMetaData DecompileJassScriptMetaData(string jassScript)
        {
            /*
                Algorithm Note: decompiler looks for patterns of specific function names. Obfuscation may have moved the code around, but call stack must start from main or config function or game would break.
                Do DepthFirstSearch of all recursive lines of code executing from config/main (ignoring undefined functions assuming they're blizzard natives) & copy/paste them at the bottom of each native editor function
                It doesn't matter that code would crash, because we only need decompiler to find patterns and generate editor-specific UI files.
                Don't save this as new war3map script file, because editor will re-create any editor native functions from editor-specific UI files, however rename native editor functions to _old in visual trigger file to avoid conflict when saving.
                Comment code that was reverse-engineered into ObjectManager data so it doesn't duplicate code on next editor save. Could delete instead of comment, but it's useful if programmer needs it as reference for bug fixes.
            */

            var jassParsed = JassSyntaxFactory.ParseCompilationUnit(jassScript);
            var firstPass = DecompileJassScriptMetaData_Internal(jassParsed, out var decompilationMetaData);

            if (firstPass == null)
            {
                return null;
            }

            var correctedUnitVariableNames = new List<KeyValuePair<string, string>>();

            var globalGenerateds = jassParsed.Declarations.OfType<JassGlobalDeclarationListSyntax>().SelectMany(x => x.Globals).Where(x => x is JassGlobalDeclarationSyntax).Cast<JassGlobalDeclarationSyntax>().Select(x => x.Declarator.IdentifierName.Name).Where(x => x.StartsWith("gg_")).ToList();
            var decompiled = new List<string>();
            decompiled.AddRange(firstPass.Cameras?.Cameras?.Select(x => $"gg_cam_{x.Name}")?.ToList() ?? new List<string>());
            decompiled.AddRange(firstPass.Regions?.Regions?.Select(x => $"gg_rct_{x.Name}")?.ToList() ?? new List<string>());
            decompiled.AddRange(firstPass.Triggers?.TriggerItems?.OfType<TriggerDefinition>()?.Select(x => $"gg_trg_{x.Name}")?.ToList() ?? new List<string>());
            decompiled.AddRange(firstPass.Sounds?.Sounds?.Select(x => x.Name)?.ToList() ?? new List<string>()); //NOTE: War3Net doesn't remove gg_snd_ from name for some reason
            decompiled.AddRange(firstPass.Doodads?.Doodads?.Select(x => x.GetVariableName()) ?? new List<string>());
            decompiled = decompiled.Where(x => x != null).Select(x => x.Replace(" ", "_")).ToList();
            var notDecompiledGlobalGenerateds = globalGenerateds.Except(decompiled).ToList();
            correctedUnitVariableNames.AddRange(notDecompiledGlobalGenerateds.Select(x => new KeyValuePair<string, string>(x, "udg_" + x.Substring("gg_".Length))));
            var units = decompilationMetaData.GetAll<UnitData>().ToList();
            if (units.Count > 0)
            {                
                correctedUnitVariableNames.AddRange(units.Select(x => new KeyValuePair<string, string>(decompilationMetaData.GetVariableName(x), x.GetVariableName())).Where(x => x.Key?.StartsWith("gg_") == true));
            }

            if (!correctedUnitVariableNames.Any())
            {
                return firstPass;
            }

            // _#### (CreationNumber) suffix changes for everything in ObjectManager during deprotection so we have to rename the variables in script to match & then decompile a 2nd time
            var renamer = new JassRenamer(new Dictionary<string, JassIdentifierNameSyntax>(), correctedUnitVariableNames.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => new JassIdentifierNameSyntax(x.Last().Value)));
            if (!renamer.TryRenameCompilationUnit(jassParsed, out var jassParsed_2ndPass))
            {
                return firstPass;
            }

            _logEvent("Global generated variables renamed.");
            _logEvent("Starting decompile war3map script 2nd pass.");

            var result = DecompileJassScriptMetaData_Internal(jassParsed_2ndPass, out var _);
            if (result == null)
            {
                return firstPass;
            }

            return result;
        }

        protected ScriptMetaData DecompileLuaScriptMetaData(string luaScript)
        {
            //todo: code this!
            return new ScriptMetaData();
        }

        protected Map SetNativeFiles(string folder, Action<Map> forcedValueOverrides = null)
        {
            //note: the order of operations matters. For example, script file import fails if info file not yet imported. So we import each file 2x
            _logEvent("Analyzing map files");
            var mapFiles = Directory.GetFiles(folder, "war3map*", SearchOption.AllDirectories).Union(Directory.GetFiles(folder, "war3campaign*", SearchOption.AllDirectories)).OrderBy(x => string.Equals(x, MapInfo.FileName, StringComparison.InvariantCultureIgnoreCase) ? 0 : 1).ToList();

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
                    map.SetNativeFile(fileName, Path.GetFileName(fileName));
                }
            }

            _logEvent("Done analyzing map files");

            return map;
        }

        protected string RenderLuaAST(LuaAST luaAST)
        {
            return LuaParser.RenderLuaAST(luaAST);
        }

        protected LuaAST ParseLuaScript(string luaScript)
        {
            return LuaParser.ParseScript(luaScript);
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
            var result = DeObfuscateFourCCJass(jassScript);

            try
            {
                SplitUserDefinedAndAutoGeneratedGlobalVariableNames(result, out var userDefinedGlobals, out var globalGenerateds);
                var parsed = JassSyntaxFactory.ParseCompilationUnit(result);
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
            var w3ipath = Path.Combine(DiscoveredFilesPath, MapInfo.FileName);
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
            using (var stream = new FileStream(Path.Combine(DiscoveredFilesPath, ImportedFiles.MapFileName), FileMode.Create))
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
        protected List<string> CleanScannedUnknownFileNames(HashSet<string> scannedFileNames)
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

        protected HashSet<FourCC> _objectDataPropertiesWithFileReferences = new HashSet<FourCC>() { "aaea", "aart", "acat", "aeat", "aefs", "amat", "aord", "arar", "asat", "atat", "auar", "bfil", "bnam", "bptx", "btxf", "dfil", "dptx", "fart", "feat", "fsat", "ftat", "gar1", "ifil", "iico", "ucua", "uico", "umdl", "upat", "uspa", "ussi" };
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

            var stringValues = _objectEditor.ExportAllStringValues();
            var stringsWithFileExtensions = stringValues.SelectMany(x => SplitTextByFileExtensionLocations(x, unknownFileExtensions)).ToList();
            result.AddRange(stringsWithFileExtensions);
            result.AddRange(stringsWithFileExtensions.SelectMany(x => ScanTextForPotentialUnknownFileNames_SLOW(x, unknownFileExtensions)));

            var fileReferences = _objectEditor.ExportAllStringValues(_objectDataPropertiesWithFileReferences);
            result.AddRange(fileReferences);
            result.AddRange(AddCommonModelAndTextureFileExtensions(fileReferences));

            return CleanScannedUnknownFileNames(result);
        }

        protected List<string> ParseFileToDetectPossibleUnknowns(byte[] fileContents, string fileExtension, List<string> unknownFileExtensions)
        {
            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            var allLines = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            try
            {
                //todo: test with no global list file & use Encoding.UTF8 in addition to see if it captures any additional unknowns that weren't found otherwise
                allLines.AddRange(Utils.NO_ENCODING.GetString(fileContents).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }
            catch { }

            var stringsWithFileExtensions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            stringsWithFileExtensions.AddRange(ScanBytesForReadableAsciiStrings(fileContents).SelectMany(x => SplitTextByFileExtensionLocations(x, unknownFileExtensions)));
            stringsWithFileExtensions.AddRange(allLines.SelectMany(x => SplitTextByFileExtensionLocations(x, unknownFileExtensions)));
            result.AddRange(stringsWithFileExtensions);

            if (fileExtension.Equals(".toc", StringComparison.InvariantCultureIgnoreCase) || fileExtension.Equals(".imp", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(allLines);
            }

            if (fileExtension.Equals(".txt", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(ScanINIForPossibleFileNames(fileContents, unknownFileExtensions));
            }

            if (fileExtension.Equals(".mdx", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(ScanMDXForPossibleFileNames(fileContents));
            }

            if (fileExtension.Equals(".mdl", StringComparison.InvariantCultureIgnoreCase))
            {
                result.AddRange(ScanMDLForPossibleFileNames(fileContents));
            }

            if (fileExtension.Equals(".j", StringComparison.InvariantCultureIgnoreCase) || fileExtension.Equals(".lua", StringComparison.InvariantCultureIgnoreCase) || fileExtension.Equals(".slk", StringComparison.InvariantCultureIgnoreCase) || fileExtension.Equals(".txt", StringComparison.InvariantCultureIgnoreCase) || fileExtension.Equals(".fdf", StringComparison.InvariantCultureIgnoreCase))
            {
                var quotedStrings = new HashSet<string>(allLines.SelectMany(x => ParseQuotedStringsFromCode(x)), StringComparer.InvariantCultureIgnoreCase);
                result.AddRange(AddCommonModelAndTextureFileExtensions(quotedStrings));
            }

            if (fileExtension.Equals(".slk", StringComparison.InvariantCultureIgnoreCase))
            {
                try
                {
                    using (var stream = new MemoryStream(fileContents))
                    {
                        var slkTable = new SylkParser().Parse(stream);
                        var columns = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
                        for (var x = 0; x < slkTable.Width; x++)
                        {
                            var columnName = slkTable[x, 0]?.ToString();
                            if (!string.IsNullOrWhiteSpace(columnName))
                            {
                                columns[columnName] = x;
                            }
                        }
                        var files = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                        if (columns.TryGetValue("dir", out var directoryColumn) && columns.TryGetValue("file", out var fileColumn))
                        {
                            for (var y = 1; y < slkTable.Height; y++)
                            {
                                try
                                {
                                    var directory = slkTable[directoryColumn, y]?.ToString();
                                    var file = slkTable[fileColumn, y]?.ToString();
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
                }
                catch { }
            }

            //todo: [LowPriority] add real FDF parser? (none online, would need to build my own based on included fdf.g grammar file)

            if (fileExtension.Equals(".lua", StringComparison.InvariantCultureIgnoreCase))
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

            return CleanScannedUnknownFileNames(result);
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

        protected HashSet<string> AddCommonModelAndTextureFileExtensions(HashSet<string> strings)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            result.AddRange(strings);
            var shortStrings = strings.Where(x => x.Length <= 150).ToList();
            result.AddRange(_modelAndTextureFileExtensions.SelectMany(ext => shortStrings.Select(fileName => $"{fileName}{ext}")));
            result.AddRange(_modelAndTextureFileExtensions.SelectMany(ext => shortStrings.Select(fileName => Path.ChangeExtension(fileName, ext))));
            return result;
        }

        protected List<string> ScanMDLForPossibleFileNames(byte[] fileContents)
        {
            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var lines = fileContents.ToString_NoEncoding().Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var commentLines = lines.Where(x => x.Trim().StartsWith("//")).Select(x => x.Trim().TrimStart('/')).ToList();
            result.AddRange(commentLines.SelectMany(x => ScanTextForPotentialUnknownFileNames_SLOW(x, _modelAndTextureFileExtensions.ToList())));
            result.AddRange(lines.Where(x => _modelAndTextureFileExtensions.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase))).SelectMany(x => ScanTextForPotentialUnknownFileNames_SLOW(x, _modelAndTextureFileExtensions.ToList())));

            try
            {
                var model = new MdxLib.Model.CModel();
                using (var stream = new MemoryStream(fileContents))
                {
                    var loader = new MdxLib.ModelFormats.CMdl();
                    loader.Load("EMPTY_FILE_NAME.mdl", stream, model);
                }
                if (model.Textures != null)
                {
                    var textures = new HashSet<string>(model.Textures.Select(x => x.FileName).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.InvariantCultureIgnoreCase);
                    result.AddRange(AddCommonModelAndTextureFileExtensions(textures));
                }
                if (model.Nodes != null)
                {
                    var nodes = new HashSet<string>(model.Nodes.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.InvariantCultureIgnoreCase);
                    result.AddRange(AddCommonModelAndTextureFileExtensions(nodes));
                }
                result.Add($"{model.Name}.mdl");
            }
            catch { }

            result.AddRange(result.SelectMany(x => _modelAndTextureFileExtensions.Select(ext => Path.ChangeExtension(x, ext))).ToList());
            return result.ToList();
        }

        protected List<string> ScanMDXForPossibleFileNames_FastMDX(byte[] fileContents)
        {
            //todo: fix bug when parsing Glow.mdx from ZombieVillager (crashes rather than just returning the data it found) [low priority since we probably delete this library and only use MDXLib]
            try
            {
                var model = new MdxLib.Model.CModel();
                using (var stream = new MemoryStream(fileContents))
                {
                    var loader = new MdxLib.ModelFormats.CMdx();
                    loader.Load("EMPTY_FILE_NAME.mdx", stream, model);
                }
                var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                if (model.Textures != null)
                {
                    var textures = new HashSet<string>(model.Textures.Select(x => x.FileName).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.InvariantCultureIgnoreCase);
                    result.AddRange(AddCommonModelAndTextureFileExtensions(textures));
                }
                if (model.Nodes != null)
                {
                    var nodes = new HashSet<string>(model.Nodes.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.InvariantCultureIgnoreCase);
                    result.AddRange(AddCommonModelAndTextureFileExtensions(nodes));
                }
                result.AddRange(_modelAndTextureFileExtensions.Select(ext => $"{model.Name}{ext}"));
                return result.ToList();
            }
            catch { }

            _logEvent($"Error parsing file with FastMDX Library");
            return new List<string>();
        }

        protected List<string> ScanMDXForPossibleFileNames_MDXLib(byte[] fileContents)
        {
            //todo: fix bug when parsing Glow.mdx from ZombieVillager (crashes rather than just returning the data it found)
            try
            {
                using (var stream = new MemoryStream(fileContents))
                {
                    var mdx = new MDX(stream);
                    var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    if (mdx.Textures != null)
                    {
                        var textures = new HashSet<string>(mdx.Textures.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.InvariantCultureIgnoreCase);
                        result.AddRange(AddCommonModelAndTextureFileExtensions(textures));
                    }
                    result.AddRange(_modelAndTextureFileExtensions.Select(ext => $"{mdx.Info.Name}{ext}"));
                    return result.ToList();
                }
            }
            catch
            {
            }

            _logEvent($"Error parsing file with MDXLib Library");
            return new List<string>();
        }

        [GeneratedRegex(@"^MDLXVERS.*?MODLt.*?([a-z0-9 _-]+)", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ScanMDX();
        protected List<string> ScanMDXForPossibleFileNames_Regex(byte[] fileContents)
        {
            var mdxMatch = Regex_ScanMDX().Match(fileContents.ToString_NoEncoding());
            if (mdxMatch.Success)
            {
                return _modelAndTextureFileExtensions.Select(ext => $"{mdxMatch.Groups[1].Value}{ext}").ToList();
            }

            return new List<string>();
        }

        protected List<string> ScanMDXForPossibleFileNames(byte[] fileContents)
        {
            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            result.AddRange(ScanMDXForPossibleFileNames_FastMDX(fileContents));
            result.AddRange(ScanMDXForPossibleFileNames_MDXLib(fileContents));
            result.AddRange(ScanMDXForPossibleFileNames_Regex(fileContents));
            result.AddRange(result.SelectMany(x => _modelAndTextureFileExtensions.Select(ext => Path.ChangeExtension(x, ext))).ToList());
            return result.ToList();
        }

        protected List<string> ScanINIForPossibleFileNames(byte[] fileContents, List<string> unknownFileExtensions)
        {
            var result = new List<string>();
            try
            {
                var parser = new StreamIniDataParser(new IniParser.Parser.IniDataParser(new IniParserConfiguration() { SkipInvalidLines = true, AllowDuplicateKeys = true, AllowDuplicateSections = true, AllowKeysWithoutSection = true }));
                using (var stream = new MemoryStream(fileContents))
                using (var reader = new StreamReader(stream))
                {
                    var ini = parser.ReadData(reader);
                    foreach (var section in ini.Sections)
                    {
                        foreach (var key in section.Keys)
                        {
                            if (key.KeyName.EndsWith("art", StringComparison.InvariantCultureIgnoreCase) || key.KeyName.EndsWith("name", StringComparison.InvariantCultureIgnoreCase))
                            {
                                result.AddRange(AddCommonModelAndTextureFileExtensions(new HashSet<string>() { key.Value }));
                            }

                            result.AddRange(ScanTextForPotentialUnknownFileNames_SLOW(key.Value, unknownFileExtensions));
                        }
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