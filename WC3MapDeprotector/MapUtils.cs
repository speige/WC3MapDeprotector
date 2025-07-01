using Jass2Lua;
using System.Text;
using War3Net.Build;
using War3Net.Build.Audio;
using War3Net.Build.Environment;
using War3Net.Build.Extensions;
using War3Net.Build.Import;
using War3Net.Build.Info;
using War3Net.Build.Object;
using War3Net.Build.Script;
using War3Net.Build.Widget;
using War3Net.IO.Mpq;

namespace WC3MapDeprotector
{
    public static class MapUtils
    {
        public static bool ExtractFile(string mpqFileName, string archivePath, string localDiskFileName)
        {
            if (!StormLibrary.SFileOpenArchive(mpqFileName, 0, StormLibrary.SFileOpenArchiveFlags.AccessReadOnly, out var archiveHandle))
            {
                throw new Exception("Unable to open MPQ Archive. File may be corrupt or locked by another program.");
            }

            try
            {
                var result = StormLibrary.SFileExtractFile(archiveHandle, archivePath, localDiskFileName, 0);
                result = result && File.Exists(localDiskFileName) && File.ReadAllBytes(localDiskFileName).Length > 0;
                return result;
            }
            finally
            {
                StormLibrary.SFileCloseArchive(archiveHandle);
            }
        }

        public static bool AddFile(string mpqFileName, string localDiskFileName, string archivePath)
        {
            //todo: increase file count if needed
            if (!StormLibrary.SFileOpenArchive(mpqFileName, 0, StormLibrary.SFileOpenArchiveFlags.AccessReadWriteShare, out var archiveHandle))
            {
                throw new Exception("Unable to open MPQ Archive. File may be corrupt or locked by another program.");
            }

            try
            {
                const uint MPQ_FILE_COMPRESS = 0x00000200;
                const uint MPQ_COMPRESSION_ZLIB = 0x02;
                const uint MPQ_COMPRESSION_HUFFMANN = 0x01;
                const uint MPQ_COMPRESSION_NEXT_SAME = 0xFFFFFFFF;
                if (!StormLibrary.SFileAddFileEx(archiveHandle, localDiskFileName, archivePath, MPQ_FILE_COMPRESS, string.Equals(Path.GetExtension(archivePath), ".wav", StringComparison.InvariantCultureIgnoreCase) ? MPQ_COMPRESSION_HUFFMANN : MPQ_COMPRESSION_ZLIB, MPQ_COMPRESSION_NEXT_SAME))
                {
                    return false;
                }
            }
            finally
            {
                StormLibrary.SFileCloseArchive(archiveHandle);
            }

            var tempFile = Path.GetTempFileName();
            try
            {
                if (!ExtractFile(mpqFileName, archivePath, tempFile))
                {
                    return false;
                }

                if (!File.ReadAllBytes(localDiskFileName).SequenceEqual(File.ReadAllBytes(tempFile)))
                {
                    return false;
                }
            }
            finally
            {
                Utils.SafeDeleteFile(tempFile);
            }

            return true;
        }

        public static bool RemoveFile(string mpqFileName, string archivePath)
        {
            if (!StormLibrary.SFileOpenArchive(mpqFileName, 0, StormLibrary.SFileOpenArchiveFlags.AccessReadWriteShare, out var archiveHandle))
            {
                throw new Exception("Unable to open MPQ Archive. File may be corrupt or locked by another program.");
            }

            try
            {
                if (!StormLibrary.SFileRemoveFile(archiveHandle, archivePath, 0))
                {
                    return false;
                }
            }
            finally
            {
                StormLibrary.SFileCloseArchive(archiveHandle);
            }

            var tempFile = Path.GetTempFileName();
            try
            {
                ExtractFile(mpqFileName, archivePath, tempFile);

                if (File.ReadAllBytes(tempFile).Length > 0)
                {
                    return false;
                }
            }
            finally
            {
                Utils.SafeDeleteFile(tempFile);
            }

            return true;
        }

        public static bool IsLuaMap(string mapFileName)
        {
            var tempInfoFileName = Path.GetTempFileName();
            try
            {
                if (!ExtractFile(mapFileName, MapInfo.FileName, tempInfoFileName))
                {
                    throw new Exception(MapInfo.FileName + " missing from MPQ Archive");
                }

                var map = new Map();
                if (!map.SetNativeFile(tempInfoFileName, MapInfo.FileName))
                {
                    throw new Exception("Could not parse map info .w3i file");
                }

                return map.Info.ScriptLanguage == ScriptLanguage.Lua;
            }
            finally
            {
                Utils.SafeDeleteFile(tempInfoFileName);
            }
        }

        public static void ConvertJassToLua(string mapFileName_Jass, string mapFileName_Lua)
        {
            var tempInfoFileName = Path.GetTempFileName();
            if (!ExtractFile(mapFileName_Jass, MapInfo.FileName, tempInfoFileName))
            {
                throw new Exception(MapInfo.FileName + " missing from MPQ Archive");
            }

            var map = new Map();
            try
            {
                if (!map.SetNativeFile(tempInfoFileName, MapInfo.FileName))
                {
                    throw new Exception("Could not parse map info .w3i file");
                }
            }
            catch (Exception e)
            {
                throw;
                //todo: use hex editor to set lua flag?
            }

            if (map.Info.ScriptLanguage == ScriptLanguage.Lua)
            {
                throw new Exception("Map is already lua!");
            }

            var tempScriptFileName = Path.GetTempFileName();
            if (ExtractFile(mapFileName_Jass, LuaMapScript.FileName, tempScriptFileName) || ExtractFile(mapFileName_Jass, LuaMapScript.FullName, tempScriptFileName))
            {
                throw new Exception("Map has both a jass and lua file. Probably using map hack which is not supported in reforged. Needs to be converted manually.");
            }

            if (!ExtractFile(mapFileName_Jass, JassMapScript.FileName, tempScriptFileName))
            {
                if (!ExtractFile(mapFileName_Jass, JassMapScript.FullName, tempScriptFileName))
                {
                    throw new Exception("jass script file not found in MPQ Archive");
                }
            }

            var tempCustomTextTriggersFileName = Path.GetTempFileName();
            if (ExtractFile(mapFileName_Jass, MapCustomTextTriggers.FileName, tempCustomTextTriggersFileName))
            {
                try
                {
                    map.SetNativeFile(tempCustomTextTriggersFileName, MapCustomTextTriggers.FileName);
                    if (map.CustomTextTriggers?.CustomTextTriggers != null)
                    {
                        if (!string.IsNullOrWhiteSpace(map.CustomTextTriggers?.GlobalCustomScriptCode?.Code))
                        {
                            map.CustomTextTriggers.GlobalCustomScriptCode.Code = ConvertJassToLua(map.CustomTextTriggers.GlobalCustomScriptCode.Code.Replace("%%", "%")).Replace("%", "%%");
                        }

                        foreach (var textTrigger in map.CustomTextTriggers.CustomTextTriggers)
                        {
                            textTrigger.Code = ConvertJassToLua(textTrigger.Code);
                        }
                    }
                }
                catch
                {
                    // swallow exceptions (probably a blank/corrupt file)
                }
            }

            var tempTriggersFileName = Path.GetTempFileName();
            if (ExtractFile(mapFileName_Jass, MapTriggers.FileName, tempTriggersFileName))
            {
                try
                {
                    map.SetNativeFile(tempTriggersFileName, MapTriggers.FileName);
                    if (map.Triggers?.TriggerItems != null)
                    {
                        foreach (var trigger in map.Triggers.TriggerItems.OfType<TriggerDefinition>())
                        {
                            if (trigger.Functions != null)
                            {
                                foreach (var function in trigger.Functions.Where(x => string.Equals(x.Name, "CustomScriptCode")))
                                {
                                    if (!string.IsNullOrWhiteSpace(function.Parameters?[0]?.Value))
                                    {
                                        function.Parameters[0].Value = ConvertJassToLua(function.Parameters[0].Value, new Jass2LuaTranspiler.Options() { AddGithubAttributionLink = false, PrependTranspilerWarnings = false });
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // swallow exceptions (probably a blank/corrupt file)
                }
            }

            map.Info.ScriptLanguage = ScriptLanguage.Lua;
            map.Info.FormatVersion = Enum.GetValues(typeof(MapInfoFormatVersion)).Cast<MapInfoFormatVersion>().OrderByDescending(x => x).First();
            map.Info.GameDataVersion = GameDataVersion.TFT;
            map.Info.GameVersion ??= new Version(2, 0, 0, 22370);
            map.Info.SupportedModes = SupportedModes.SD | SupportedModes.HD;
            map.Script = ConvertJassToLua(Utils.ReadFile_NoEncoding(tempScriptFileName));

            var tempLuaMapFileName = Path.GetTempFileName();
            File.Copy(mapFileName_Jass, tempLuaMapFileName, true);
            RemoveFile(tempLuaMapFileName, MapInfo.FileName);
            using (var mpqStream = map.GetNativeFile(MapInfo.FileName))
            using (var fileStream = File.Create(tempInfoFileName))
            {
                mpqStream.MpqStream.CopyTo(fileStream);
            }
            if (!AddFile(tempLuaMapFileName, tempInfoFileName, MapInfo.FileName))
            {
                throw new Exception("Unable to modify MPQ. It may be a protected map. Please de-protect first.");
            }

            RemoveFile(tempLuaMapFileName, JassMapScript.FileName);
            RemoveFile(tempLuaMapFileName, JassMapScript.FullName);
            using (var mpqStream = map.GetNativeFile(LuaMapScript.FileName))
            using (var fileStream = File.Create(tempScriptFileName))
            {
                mpqStream.MpqStream.CopyTo(fileStream);
            }
            if (!AddFile(tempLuaMapFileName, tempScriptFileName, LuaMapScript.FileName))
            {
                throw new Exception("Unknown error adding lua script to MPQ");
            }

            RemoveFile(tempLuaMapFileName, MapCustomTextTriggers.FileName);
            if (map.CustomTextTriggers != null)
            {
                using (var mpqStream = map.GetNativeFile(MapCustomTextTriggers.FileName))
                using (var fileStream = File.Create(tempCustomTextTriggersFileName))
                {
                    mpqStream.MpqStream.CopyTo(fileStream);
                }
                if (!AddFile(tempLuaMapFileName, tempCustomTextTriggersFileName, MapCustomTextTriggers.FileName))
                {
                    throw new Exception("Unknown error updating custom text triggers file");
                }
            }

            RemoveFile(tempLuaMapFileName, MapTriggers.FileName);
            if (map.Triggers != null)
            {
                using (var mpqStream = map.GetNativeFile(MapTriggers.FileName))
                using (var fileStream = File.Create(tempTriggersFileName))
                {
                    mpqStream.MpqStream.CopyTo(fileStream);
                }
                if (!AddFile(tempLuaMapFileName, tempTriggersFileName, MapTriggers.FileName))
                {
                    throw new Exception("Unknown error updating GUI triggers file");
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(mapFileName_Lua));
            File.Move(tempLuaMapFileName, mapFileName_Lua);

            Utils.SafeDeleteFile(tempInfoFileName);
            Utils.SafeDeleteFile(tempScriptFileName);
            Utils.SafeDeleteFile(tempCustomTextTriggersFileName);
            Utils.SafeDeleteFile(tempTriggersFileName);
        }

        public static string ConvertJassToLua(string jassScript, Jass2LuaTranspiler.Options options = null)
        {
            var transpiler = new Jass2LuaTranspiler(options);
            return transpiler.Transpile(jassScript, out var warnings, out var sourceMap);
        }

        public static byte[] ToByteArray(this MpqFile file)
        {
            using (var stream = new MemoryStream())
            {
                file.MpqStream.Position = 0;
                file.MpqStream.CopyTo(stream);
                file.MpqStream.Position = 0;
                return stream.ToArray();
            }
        }

        public static Encoding GetCorrectFileEncoding(string mpqFileName)
        {
            mpqFileName = Path.GetFileName((mpqFileName ?? "").Trim());
            if (JassMapScript.FileName.Equals(mpqFileName, StringComparison.InvariantCultureIgnoreCase) || LuaMapScript.FileName .Equals(mpqFileName, StringComparison.InvariantCultureIgnoreCase))
            {
                return Utils.NO_ENCODING;
            }

            return Encoding.UTF8;
        }

        public static readonly HashSet<string> WorldEditorMapFileNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ImportedFiles.MapFileName, MapCameras.FileName, MapCustomTextTriggers.FileName, MapDoodads.FileName, MapRegions.FileName, MapSounds.FileName, MapTriggers.FileName, MapUnits.FileName };
        public static readonly HashSet<string> AllNativeMapFileNames = new HashSet<string>(WorldEditorMapFileNames, StringComparer.InvariantCultureIgnoreCase) { AbilityObjectData.MapFileName, AbilityObjectData.MapSkinFileName, BuffObjectData.MapFileName, BuffObjectData.MapSkinFileName, DestructableObjectData.MapFileName, DestructableObjectData.MapSkinFileName, DoodadObjectData.MapFileName, DoodadObjectData.MapSkinFileName, ItemObjectData.MapFileName, ItemObjectData.MapSkinFileName, JassMapScript.FileName, JassMapScript.FullName, LuaMapScript.FileName, LuaMapScript.FullName, MapEnvironment.FileName, MapInfo.FileName, MapPathingMap.FileName, MapPreviewIcons.FileName, MapShadowMap.FileName, TriggerStrings.MapFileName, UnitObjectData.MapFileName, UnitObjectData.MapSkinFileName, UpgradeObjectData.MapFileName, UpgradeObjectData.MapSkinFileName };
        private static readonly Dictionary<string, Func<Map, MpqFile>> NativeFileNameToGetMpqFileMethod;

        static MapUtils()
        {
            KeyValuePair<string, Func<Map, MpqFile>> CreateMapping(string fileName, Func<Map, Encoding, MpqFile> action)
            {
                var encoding = GetCorrectFileEncoding(fileName);
                Func<Map, MpqFile> curriedAction = map => action(map, encoding);
                return new KeyValuePair<string, Func<Map, MpqFile>>(fileName.Trim(), curriedAction);
            }
            var a = new []
            {
                CreateMapping(MapSounds.FileName, (map, encoding) => map.GetSoundsFile(encoding)),
                CreateMapping(MapCameras.FileName, (map, encoding) => map.GetCamerasFile(encoding)),
                CreateMapping(MapEnvironment.FileName, (map, encoding) => map.GetEnvironmentFile(encoding)),
                CreateMapping(MapPathingMap.FileName, (map, encoding) => map.GetPathingMapFile(encoding)),
                CreateMapping(MapPreviewIcons.FileName, (map, encoding) => map.GetPreviewIconsFile(encoding)),
                CreateMapping(MapRegions.FileName, (map, encoding) => map.GetRegionsFile(encoding)),
                CreateMapping(MapShadowMap.FileName, (map, encoding) => map.GetShadowMapFile(encoding)),
                CreateMapping(ImportedFiles.MapFileName, (map, encoding) => map.GetImportedFilesFile(encoding)),
                CreateMapping(MapInfo.FileName, (map, encoding) => map.GetInfoFile(encoding)),
                CreateMapping(AbilityObjectData.MapFileName, (map, encoding) => map.GetAbilityObjectDataFile(encoding)),
                CreateMapping(BuffObjectData.MapFileName, (map, encoding) => map.GetBuffObjectDataFile(encoding)),
                CreateMapping(DestructableObjectData.MapFileName, (map, encoding) => map.GetDestructableObjectDataFile(encoding)),
                CreateMapping(DoodadObjectData.MapFileName, (map, encoding) => map.GetDoodadObjectDataFile(encoding)),
                CreateMapping(ItemObjectData.MapFileName, (map, encoding) => map.GetItemObjectDataFile(encoding)),
                CreateMapping(UnitObjectData.MapFileName, (map, encoding) => map.GetUnitObjectDataFile(encoding)),
                CreateMapping(UpgradeObjectData.MapFileName, (map, encoding) => map.GetUpgradeObjectDataFile(encoding)),
                CreateMapping(AbilityObjectData.MapSkinFileName, (map, encoding) => map.GetAbilitySkinObjectDataFile(encoding)),
                CreateMapping(BuffObjectData.MapSkinFileName, (map, encoding) => map.GetBuffSkinObjectDataFile(encoding)),
                CreateMapping(DestructableObjectData.MapSkinFileName, (map, encoding) => map.GetDestructableSkinObjectDataFile(encoding)),
                CreateMapping(DoodadObjectData.MapSkinFileName, (map, encoding) => map.GetDoodadSkinObjectDataFile(encoding)),
                CreateMapping(ItemObjectData.MapSkinFileName, (map, encoding) => map.GetItemSkinObjectDataFile(encoding)),
                CreateMapping(UnitObjectData.MapSkinFileName, (map, encoding) => map.GetUnitSkinObjectDataFile(encoding)),
                CreateMapping(UpgradeObjectData.MapSkinFileName, (map, encoding) => map.GetUpgradeSkinObjectDataFile(encoding)),
                CreateMapping(MapCustomTextTriggers.FileName, (map, encoding) => map.GetCustomTextTriggersFile(encoding)),
                CreateMapping(MapTriggers.FileName, (map, encoding) => map.GetTriggersFile(encoding)),
                CreateMapping(TriggerStrings.MapFileName, (map, encoding) => map.GetTriggerStringsFile(encoding)),
                CreateMapping(MapDoodads.FileName, (map, encoding) => map.GetDoodadsFile(encoding)),
                CreateMapping(MapUnits.FileName, (map, encoding) => map.GetUnitsFile(encoding)),
                CreateMapping(JassMapScript.FileName, (map, encoding) => map?.Info?.ScriptLanguage == ScriptLanguage.Jass ? map.GetScriptFile(encoding) : null),
                CreateMapping(JassMapScript.FullName, (map, encoding) => map?.Info?.ScriptLanguage == ScriptLanguage.Jass ? map.GetScriptFile(encoding) : null),
                CreateMapping(LuaMapScript.FileName, (map, encoding) => map?.Info?.ScriptLanguage == ScriptLanguage.Lua ? map.GetScriptFile(encoding) : null),
                CreateMapping(LuaMapScript.FullName, (map, encoding) => map?.Info?.ScriptLanguage == ScriptLanguage.Lua ? map.GetScriptFile(encoding) : null)
            };
            NativeFileNameToGetMpqFileMethod = a.ToDictionary(x => x.Key, x => x.Value, StringComparer.InvariantCultureIgnoreCase);
        }

        public static MpqFile GetNativeFile(this Map map, string fileName)
        {
            try
            {
                if (NativeFileNameToGetMpqFileMethod.TryGetValue((fileName ?? "").Trim(), out var action))
                {
                    return action(map);
                }
            }
            catch (Exception e)
            {
                DebugSettings.Warn(e.Message);
            }

            return null;
        }

        public static bool SetNativeFile(this Map map, string localDiskFileName, string mpqFileName, bool overwrite = false)
        {
            if (!overwrite)
            {
                using (var oldMapFile = map.GetNativeFile(mpqFileName))
                {
                    if (oldMapFile != null)
                    {
                        return false;
                    }
                }
            }

            var bytes = File.ReadAllBytes(localDiskFileName);
            if (bytes.Length == 0)
            {
                return false;
            }

            var encoding = GetCorrectFileEncoding(mpqFileName);

            try
            {
                using (var stream = new MemoryStream(bytes))
                {
                    return map.SetFile(mpqFileName, true, stream, encoding, true);
                }
            }
            catch (Exception e)
            {
                DebugSettings.Warn("Unable to set map file");
            }

            return false;
        }


        public static List<MpqKnownFile> GetAllNativeFiles(this Map map, bool ignoreExceptions = false)
        {
            //NOTE: w3i & war3mapunits.doo versions have to correlate or the editor crashes.
            //having mismatch can cause world editor to be very slow & take tons of memory, if it doesn't crash
            //not sure how to know compatability, but current guess is Units.UseNewFormat can't be used for MapInfoFormatVersion < v28

            if (map.Info == null)
            {
                map.Info = new MapInfo(default);
                map.Info.FormatVersion = Enum.GetValues(typeof(MapInfoFormatVersion)).Cast<MapInfoFormatVersion>().OrderByDescending(x => x).First();
                //map.Info.EditorVersion = Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>().OrderByDescending(x => x).First();
                map.Info.GameVersion = new Version(1, 36, 1, 20719);
            }

            if (map.Units != null && map.Info.FormatVersion >= MapInfoFormatVersion.v28)
            {
                map.Units.UseNewFormat = true;
            }

            if (map.Units != null && map.Units.UseNewFormat)
            {
                foreach (var unit in map.Units.Units)
                {
                    unit.SkinId = unit.TypeId;
                }
            }

            return AllNativeMapFileNames.Select(x => map.GetNativeFile(x)).OfType<MpqKnownFile>().Where(x => x != null).ToList();
        }
    }
}