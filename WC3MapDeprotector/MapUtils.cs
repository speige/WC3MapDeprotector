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
                return StormLibrary.SFileExtractFile(archiveHandle, archivePath, localDiskFileName, 0);
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
                                        function.Parameters[0].Value = ConvertJassToLua(function.Parameters[0].Value, new Jass2LuaTranspiler.Options() { AddGithubAttributionLink = false, AddStringPlusOperatorOverload = false, PrependTranspilerWarnings = false });
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

            Directory.CreateDirectory(Path.GetDirectoryName(mapFileName_Lua));
            File.Copy(mapFileName_Jass, mapFileName_Lua);

            RemoveFile(mapFileName_Lua, MapInfo.FileName);
            using (var mpqStream = map.GetNativeFile(MapInfo.FileName))
            using (var fileStream = File.Create(tempInfoFileName))
            {
                mpqStream.MpqStream.CopyTo(fileStream);
            }
            if (!AddFile(mapFileName_Lua, tempInfoFileName, MapInfo.FileName))
            {
                throw new Exception("Unable to modify MPQ. It may be a protected map. Please de-protect first.");
            }

            RemoveFile(mapFileName_Lua, JassMapScript.FileName);
            RemoveFile(mapFileName_Lua, JassMapScript.FullName);
            using (var mpqStream = map.GetNativeFile(LuaMapScript.FileName))
            using (var fileStream = File.Create(tempScriptFileName))
            {
                mpqStream.MpqStream.CopyTo(fileStream);
            }
            if (!AddFile(mapFileName_Lua, tempScriptFileName, LuaMapScript.FileName))
            {
                throw new Exception("Unknown error adding lua script to MPQ");
            }

            RemoveFile(mapFileName_Lua, MapCustomTextTriggers.FileName);
            if (map.CustomTextTriggers != null)
            {
                using (var mpqStream = map.GetNativeFile(MapCustomTextTriggers.FileName))
                using (var fileStream = File.Create(tempCustomTextTriggersFileName))
                {
                    mpqStream.MpqStream.CopyTo(fileStream);
                }
                if (!AddFile(mapFileName_Lua, tempCustomTextTriggersFileName, MapCustomTextTriggers.FileName))
                {
                    throw new Exception("Unknown error updating custom text triggers file");
                }
            }

            RemoveFile(mapFileName_Lua, MapTriggers.FileName);
            if (map.Triggers != null)
            {
                using (var mpqStream = map.GetNativeFile(MapTriggers.FileName))
                using (var fileStream = File.Create(tempTriggersFileName))
                {
                    mpqStream.MpqStream.CopyTo(fileStream);
                }
                if (!AddFile(mapFileName_Lua, tempTriggersFileName, MapTriggers.FileName))
                {
                    throw new Exception("Unknown error updating GUI triggers file");
                }
            }

            Utils.SafeDeleteFile(tempInfoFileName);
            Utils.SafeDeleteFile(tempScriptFileName);
            Utils.SafeDeleteFile(tempCustomTextTriggersFileName);
            Utils.SafeDeleteFile(tempTriggersFileName);
        }

        public static string ConvertJassToLua(string jassScript, Jass2LuaTranspiler.Options options = null)
        {
            var transpiler = new Jass2LuaTranspiler(options);
            return transpiler.Transpile(jassScript, out var warnings);
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
            mpqFileName = Path.GetFileName((mpqFileName ?? "").Trim().ToLower());
            if (JassMapScript.FileName == mpqFileName || LuaMapScript.FileName == mpqFileName)
            {
                return Utils.NO_ENCODING;
            }

            return Encoding.UTF8;
        }

        private static string[] _allNativeMapFileNames = new[] { MapSounds.FileName, MapCameras.FileName, MapEnvironment.FileName, MapPathingMap.FileName, MapPreviewIcons.FileName, MapRegions.FileName, MapShadowMap.FileName, ImportedFiles.MapFileName, MapInfo.FileName, AbilityObjectData.MapFileName, BuffObjectData.MapFileName, DestructableObjectData.MapFileName, DoodadObjectData.MapFileName, ItemObjectData.MapFileName, UnitObjectData.MapFileName, UpgradeObjectData.MapFileName, AbilityObjectData.MapSkinFileName, BuffObjectData.MapSkinFileName, DestructableObjectData.MapSkinFileName, DoodadObjectData.MapSkinFileName, ItemObjectData.MapSkinFileName, UnitObjectData.MapSkinFileName, UpgradeObjectData.MapSkinFileName, MapCustomTextTriggers.FileName, MapTriggers.FileName, TriggerStrings.MapFileName, MapDoodads.FileName, MapUnits.FileName, JassMapScript.FileName, JassMapScript.FullName, LuaMapScript.FileName, LuaMapScript.FullName };
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

            return _allNativeMapFileNames.Select(x => map.GetNativeFile(x)).OfType<MpqKnownFile>().Where(x => x != null).ToList();
        }

        public static MpqFile GetNativeFile(this Map map, string fileName)
        {
            try
            {
                Encoding encoding = GetCorrectFileEncoding(fileName);

                switch ((fileName ?? "").Trim().ToLower())
                {
                    case MapSounds.FileName:
                        return map.GetSoundsFile(encoding);
                    case MapCameras.FileName:
                        return map.GetCamerasFile(encoding);
                    case MapEnvironment.FileName:
                        return map.GetEnvironmentFile(encoding);
                    case MapPathingMap.FileName:
                        return map.GetPathingMapFile(encoding);
                    case MapPreviewIcons.FileName:
                        return map.GetPreviewIconsFile(encoding);
                    case MapRegions.FileName:
                        return map.GetRegionsFile(encoding);
                    case MapShadowMap.FileName:
                        return map.GetShadowMapFile(encoding);
                    case ImportedFiles.MapFileName:
                        return map.GetImportedFilesFile(encoding);
                    case MapInfo.FileName:
                        return map.GetInfoFile(encoding);
                    case AbilityObjectData.MapFileName:
                        return map.GetAbilityObjectDataFile(encoding);
                    case BuffObjectData.MapFileName:
                        return map.GetBuffObjectDataFile(encoding);
                    case DestructableObjectData.MapFileName:
                        return map.GetDestructableObjectDataFile(encoding);
                    case DoodadObjectData.MapFileName:
                        return map.GetDoodadObjectDataFile(encoding);
                    case ItemObjectData.MapFileName:
                        return map.GetItemObjectDataFile(encoding);
                    case UnitObjectData.MapFileName:
                        return map.GetUnitObjectDataFile(encoding);
                    case UpgradeObjectData.MapFileName:
                        return map.GetUpgradeObjectDataFile(encoding);
                    case AbilityObjectData.MapSkinFileName:
                        return map.GetAbilitySkinObjectDataFile(encoding);
                    case BuffObjectData.MapSkinFileName:
                        return map.GetBuffSkinObjectDataFile(encoding);
                    case DestructableObjectData.MapSkinFileName:
                        return map.GetDestructableSkinObjectDataFile(encoding);
                    case DoodadObjectData.MapSkinFileName:
                        return map.GetDoodadSkinObjectDataFile(encoding);
                    case ItemObjectData.MapSkinFileName:
                        return map.GetItemSkinObjectDataFile(encoding);
                    case UnitObjectData.MapSkinFileName:
                        return map.GetUnitSkinObjectDataFile(encoding);
                    case UpgradeObjectData.MapSkinFileName:
                        return map.GetUpgradeSkinObjectDataFile(encoding);
                    case MapCustomTextTriggers.FileName:
                        return map.GetCustomTextTriggersFile(encoding);
                    case MapTriggers.FileName:
                        return map.GetTriggersFile(encoding);
                    case TriggerStrings.MapFileName:
                        return map.GetTriggerStringsFile(encoding);
                    case MapDoodads.FileName:
                        return map.GetDoodadsFile(encoding);
                    case MapUnits.FileName:
                        return map.GetUnitsFile(encoding);

                    case JassMapScript.FileName:
                    case JassMapScript.FullName:
                        if (map?.Info?.ScriptLanguage != ScriptLanguage.Jass)
                        {
                            return null;
                        }

                        return map.GetScriptFile(encoding);
                    case LuaMapScript.FileName:
                    case LuaMapScript.FullName:
                        if (map?.Info?.ScriptLanguage != ScriptLanguage.Lua)
                        {
                            return null;
                        }

                        return map.GetScriptFile(encoding);
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
    }
}