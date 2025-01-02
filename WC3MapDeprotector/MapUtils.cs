using CSharpLua;
using System.Text;
using War3Net.Build;
using War3Net.Build.Extensions;
using War3Net.Build.Info;
using War3Net.Build.Script;
using War3Net.CodeAnalysis.Jass;

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
                return StormLibrary.SFileAddFileEx(archiveHandle, localDiskFileName, archivePath, MPQ_FILE_COMPRESS, string.Equals(Path.GetExtension(archivePath), ".wav", StringComparison.InvariantCultureIgnoreCase) ? MPQ_COMPRESSION_HUFFMANN : MPQ_COMPRESSION_ZLIB, MPQ_COMPRESSION_NEXT_SAME);
            }
            finally
            {
                StormLibrary.SFileCloseArchive(archiveHandle);
            }
        }

        public static bool RemoveFile(string mpqFileName, string archivePath)
        {
            if (!StormLibrary.SFileOpenArchive(mpqFileName, 0, StormLibrary.SFileOpenArchiveFlags.AccessReadWriteShare, out var archiveHandle))
            {
                throw new Exception("Unable to open MPQ Archive. File may be corrupt or locked by another program.");
            }

            try
            {
                return StormLibrary.SFileRemoveFile(archiveHandle, archivePath, 0);
            }
            finally
            {
                StormLibrary.SFileCloseArchive(archiveHandle);
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
                map.SetInfoFile(File.OpenRead(tempInfoFileName));
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
                    map.SetCustomTextTriggersFile(File.OpenRead(tempCustomTextTriggersFileName));
                    map.CustomTextTriggers.GlobalCustomScriptCode.Code = ConvertJassToLua(map.CustomTextTriggers.GlobalCustomScriptCode.Code.Replace("%%", "%")).Replace("%", "%%");
                    foreach (var textTrigger in map.CustomTextTriggers.CustomTextTriggers)
                    {
                        textTrigger.Code = ConvertJassToLua(textTrigger.Code);
                    }
                }
                catch
                {
                    // swallow exceptions (probably a blank file)
                }
            }

            var tempTriggersFileName = Path.GetTempFileName();            
            if (ExtractFile(mapFileName_Jass, MapTriggers.FileName, tempTriggersFileName))
            {
                map.SetTriggersFile(File.OpenRead(tempTriggersFileName));
                foreach (var trigger in map.Triggers.TriggerItems.OfType<TriggerDefinition>())
                {
                    foreach (var function in trigger.Functions.Where(x => string.Equals(x.Name, "CustomScriptCode")))
                    {
                        function.Parameters[0].Value = ConvertJassToLua(function.Parameters[0].Value, new Jass2LuaTranspiler.Options() { AddGithubAttributionLink = false, AddStringPlusOperatorOverload = false, PrependTranspilerWarnings = false });
                    }
                }
            }

            map.Info.ScriptLanguage = ScriptLanguage.Lua;
            map.Script = ConvertJassToLua(File.ReadAllText(tempScriptFileName));

            File.Copy(mapFileName_Jass, mapFileName_Lua);

            RemoveFile(mapFileName_Lua, MapInfo.FileName);
            using (var mpqStream = map.GetInfoFile())
            using (var fileStream = File.Create(tempInfoFileName))
            {
                mpqStream.MpqStream.CopyTo(fileStream);
            }
            AddFile(mapFileName_Lua, tempInfoFileName, MapInfo.FileName);

            RemoveFile(mapFileName_Lua, JassMapScript.FileName);
            RemoveFile(mapFileName_Lua, JassMapScript.FullName);
            using (var mpqStream = map.GetScriptFile())
            using (var fileStream = File.Create(tempScriptFileName))
            {
                mpqStream.MpqStream.CopyTo(fileStream);
            }
            AddFile(mapFileName_Lua, tempScriptFileName, LuaMapScript.FileName);

            RemoveFile(mapFileName_Lua, MapCustomTextTriggers.FileName);
            if (map.CustomTextTriggers != null)
            {
                using (var mpqStream = map.GetCustomTextTriggersFile())
                using (var fileStream = File.Create(tempCustomTextTriggersFileName))
                {
                    mpqStream.MpqStream.CopyTo(fileStream);
                }
                AddFile(mapFileName_Lua, tempCustomTextTriggersFileName, MapCustomTextTriggers.FileName);
            }

            RemoveFile(mapFileName_Lua, MapTriggers.FileName);
            if (map.Triggers != null)
            {
                using (var mpqStream = map.GetTriggersFile())
                using (var fileStream = File.Create(tempTriggersFileName))
                {
                    mpqStream.MpqStream.CopyTo(fileStream);
                }
                AddFile(mapFileName_Lua, tempTriggersFileName, MapTriggers.FileName);
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
    }
}