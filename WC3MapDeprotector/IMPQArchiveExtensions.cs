using CSharpLua;
using NAudio.Wave;
using System.Text;
using System.Text.RegularExpressions;

namespace WC3MapDeprotector
{
    public static partial class StormMPQArchiveExtensions
    {
        private static Lazy<Dictionary<ulong, string>> _defaultListFileRainbowTable = new Lazy<Dictionary<ulong, string>>(() =>
        {
            var defaultListFile = new ConcurrentHashSet<string>() {
                "(attributes)",
                "(listfile)",
                "(signature)",
                "(user data)"
            };

            var extensions = new List<string>() { "blp", "doo", "imp", "j", "json", "lua", "mmp", "shd", "slk", "tga", "txt", "w3a", "w3b", "w3c", "w3d", "w3e", "w3f", "w3h", "w3i", "w3q", "w3r", "w3s", "w3t", "w3u", "wai", "wct", "wpm", "wtg", "wts" };
            var filePrefixes = new List<string>() { "AbilityBuffData", "AbilityBuffMetaData", "AbilityData", "AbilityMetaData", "conversation", "DestructableData", "DestructableMetaData", "DoodadMetaData", "Doodads", "ItemData", "ItemMetaData", "UnitAbilities", "UnitBalance", "UnitData", "UnitMetaData", "UnitUI", "UnitWeapons", "UpgradeData", "war3campaign", "war3campaignSkin", "war3map", "war3mapExtra", "war3mapMap", "war3mapMisc", "war3mapPath", "war3mapPreview", "war3mapSkin", "war3mapUnits" };
            var folders = new List<string>() { "", "scripts", "units", "doodads" };

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

            return ConvertListFileToRainbowTable(defaultListFile.ToList());
        });

        public static bool IsInDefaultListFile(string fileName)
        {
            return MPQFullHash.TryCalculate(fileName, out var hash) && _defaultListFileRainbowTable.Value.ContainsKey(hash);
        }

        public static Dictionary<ulong, string> ConvertListFileToRainbowTable(List<string> fileNames)
        {
            var result = new ConcurrentList<KeyValuePair<ulong, string>>();
            Parallel.ForEach(fileNames, fileName =>
            {
                if (MPQFullHash.TryCalculate(fileName, out var hash))
                {
                    result.Add(new KeyValuePair<ulong, string>(hash, fileName));
                }
            });

            return result.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.First().Value);
        }
        
        public static List<string> ProcessDefaultListFile(this StormMPQArchive archive)
        {
            return archive.ProcessListFile(_defaultListFileRainbowTable.Value);
        }

        public static List<string> ProcessListFile(this StormMPQArchive archive, Dictionary<ulong, string> rainbowTable)
        {
            var verifiedFileNames = archive.MPQFileNameFullHashes.Select(x => rainbowTable.TryGetValue(x, out var fileName) ? fileName : null).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            return ProcessListFile_Slow(archive, verifiedFileNames);
        }

        public static List<string> ProcessListFile_Slow(this StormMPQArchive archive, List<string> fileNames)
        {
            List<string> result = new List<string>();
            foreach (var file in fileNames)
            {
                if (archive.DiscoverFile(file, out var _))
                {
                    result.Add(file);
                }
            }

            return result;
        }

        private static string PredictFontFileExtension(Stream stream)
        {
            try
            {
                stream.Position = 0;
                var font = SixLabors.Fonts.FontDescription.LoadDescription(stream);
                return "ttf";
            }
            catch { }
            finally
            {
                stream.Position = 0;
            }

            return null;
        }

        private static string PredictImageFileExtension(Stream stream)
        {
            try
            {
                stream.Position = 0;
                var imageIdentity = SixLabors.ImageSharp.Image.Identify(stream);
                stream.Position = 0;
                if (imageIdentity != null && imageIdentity.Width > 0 && imageIdentity.Height > 0)
                {
                    var format = SixLabors.ImageSharp.Image.DetectFormat(stream);
                    if ("BMP".Equals(format?.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //NOTE: ImageSharp returns .bm instead of .bmp
                        return ".bmp";
                    }

                    return format?.FileExtensions.FirstOrDefault() ?? ".tga";
                }
            }
            catch { }
            finally
            {
                stream.Position = 0;
            }

            return null;
        }

        private static bool IsWavAudioFile(Stream stream)
        {
            try
            {
                stream.Position = 0;
                var wav = new WaveFileReader(stream);
                return wav.TotalTime.TotalSeconds >= .25;
            }
            catch { }
            finally
            {
                stream.Position = 0;
            }

            return false;
        }

        private static bool IsMp3AudioFile(Stream stream)
        {
            try
            {
                stream.Position = 0;
                var mp3 = new Mp3FileReader(stream);
                return mp3.TotalTime.TotalSeconds >= .25;
            }
            catch { }
            finally
            {
                stream.Position = 0;
            }

            return false;
        }

        private static bool IsAiffAudioFile(Stream stream)
        {
            //todo: test if WC3 supports this file format, if not can delete code
            try
            {
                stream.Position = 0;
                var aiff = new AiffFileReader(stream);
                return aiff.TotalTime.TotalSeconds >= .25;
            }
            catch { }
            finally
            {
                stream.Position = 0;
            }

            return false;
        }

        [GeneratedRegex(@"<[^>]+>")]
        private static partial Regex Regex_HtmlTags();

        [GeneratedRegex(@"\s*function\s+config\s+takes\s+nothing\s+returns\s+nothing", RegexOptions.IgnoreCase)]
        private static partial Regex Regex_JassScript();

        [GeneratedRegex(@"\s*function\s+config\s*\(\)", RegexOptions.IgnoreCase)]
        private static partial Regex Regex_LuaScript();
        
        [GeneratedRegex(@"\s*function\s+preloadfiles\s+takes\s+nothing\s+returns\s+nothing", RegexOptions.IgnoreCase)]
        private static partial Regex Regex_PreloadFile_Jass();

        public static string PredictUnknownFileExtension(Stream stream)
        {
            try
            {
                if (stream.Length == 0)
                {
                    return null;
                }

                //NOTE: When comparing binary files with String.StartsWith/etc, you need to use Ordinal if you don't want extra non-visible characters like \0 to skew the results
                stream.Position = 0;
                byte[] bytes;
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    bytes = memoryStream.ToArray();
                    stream.Position = 0;
                }

                var nonReadableAsciiCount = 0;
                var stringBuilder = new StringBuilder();
                for (var i = 0; i < bytes.Length; i++)
                {
                    var character = (char)bytes[i];
                    if (character >= 127 || (character >= '\0' && character < (byte)' '))
                    {
                        nonReadableAsciiCount++;
                    }
                    stringBuilder.Append(character);
                }
                var fileContents = stringBuilder.ToString();
                var isProbablyBinaryOrUnicode = nonReadableAsciiCount >= Math.Max(bytes.Length / 2, 1);

                //todo: detect w3m & w3x for nested campaign maps (test if StormLib can parse)

                //todo: [LowPriority since regex already covers it] test against mdx parser
                if (fileContents.StartsWith("MDLXVERS", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mdx";
                }

                if (fileContents.StartsWith("blp", StringComparison.OrdinalIgnoreCase))
                {
                    return ".blp";
                }

                var fontFormat = PredictFontFileExtension(stream);
                if (!string.IsNullOrWhiteSpace(fontFormat))
                {
                    return $".{fontFormat}";
                }

                var imageFormat = PredictImageFileExtension(stream);
                if (!string.IsNullOrWhiteSpace(imageFormat))
                {
                    return $".{imageFormat}";
                }

                if (IsWavAudioFile(stream))
                {
                    return ".wav";
                }

                if (fileContents.StartsWith("DDS\x20", StringComparison.OrdinalIgnoreCase))
                {
                    return ".dds";
                }
                if (fileContents.StartsWith("Mz", StringComparison.InvariantCultureIgnoreCase) && fileContents.Contains("This program cannot be run in DOS mode", StringComparison.InvariantCultureIgnoreCase))
                {
                    //note: could technically also be exe, but unlikely
                    return ".dll";
                }
                if (fileContents.StartsWith("\x02\0\0\0", StringComparison.Ordinal) && (fileContents.Contains(".w3m", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains(".w3x", StringComparison.InvariantCultureIgnoreCase)))
                {
                    //todo: does War3Net have a parser to test this?
                    return ".wai";
                }

                if (fileContents.StartsWith("BM", StringComparison.Ordinal))
                {
                    DebugSettings.Warn("Delete this file detection if breakpoint is never hit");
                    return ".bmp";
                }

                //todo: [LowPriority since DefaultListFile will handle it usually] detect common wc3 formats (test against War3Net SetFile command)

                if (Regex_JassScript().IsMatch(fileContents))
                {
                    return ".j";
                }

                if (Regex_LuaScript().IsMatch(fileContents))
                {
                    return ".lua";
                }

                var lines = fileContents.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (lines.Count(x => x.EndsWith(".fdf", StringComparison.InvariantCultureIgnoreCase)) >= Math.Max(lines.Count / 2, 1))
                {
                    return ".toc";
                }

                if (Regex_PreloadFile_Jass().IsMatch(lines.FirstOrDefault() ?? ""))
                {
                    return ".pld";
                }

                //todo: [LowPriority since regex already covers it] test against mdl parser
                if (fileContents.Contains("magosx.com", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("FormatVersion", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mdl";
                }

                //todo: [LowPriority] add real FDF parser? (none online, would need to build my own based on included fdf.g grammar file)
                if (lines.Count(x => x.Contains("frame", StringComparison.InvariantCultureIgnoreCase) || x.Contains("WithChildren", StringComparison.InvariantCultureIgnoreCase) || x.Contains("IncludeFile", StringComparison.InvariantCultureIgnoreCase) || x.Contains("Template", StringComparison.InvariantCultureIgnoreCase) || x.Contains("Control", StringComparison.InvariantCultureIgnoreCase) || x.Contains("INHERITS", StringComparison.InvariantCultureIgnoreCase)) >= Math.Max(lines.Count / 10, 1))
                {
                    return ".fdf";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("ID3", StringComparison.Ordinal) && (fileContents.Contains("Lavf", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("LAME", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return ".mp3";
                }

                var iniLines = lines.Count(x => (x.Length - x.Replace("=", "").Length) == 1 || (!x.Contains("=") && x.Trim().StartsWith("[") && x.Trim().EndsWith("]")));
                if (iniLines >= Math.Max(lines.Count / 2, 1))
                {
                    //note: could technically also be ini, but unlikely
                    return ".txt";
                }

                var slkLines = lines.Count(x => x.Length >= 2 && ((x[0] >= 'a' && x[0] <= 'z') || (x[0] >= 'A' && x[0] <= 'Z')) && x[1] == ';');
                if (lines.Count > 1 && lines[0].StartsWith("ID;", StringComparison.OrdinalIgnoreCase) && slkLines >= Math.Max(lines.Count / 2, 2))
                {
                    return ".slk";
                }

                /*
                if (fileContents.StartsWith("SFPK", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".sfk";
                }
                */

                if (Regex_HtmlTags().Matches(fileContents).Count >= lines.Count)
                {
                    return ".html";
                }

                if (fileContents.Contains("TRUEVISION-XFILE", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".tga";
                }

                if (IsMp3AudioFile(stream))
                {
                    //NOTE: mp3 gets false positives sometimes, so this should be below any higher-priority file extensions. Replace with different library instead of NAudio?
                    //todo: move up higher to see if I fixed it by resetting stream position to 0 before reading
                    return ".mp3";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("MPQ\x1A", StringComparison.Ordinal))
                {
                    return ".mpq";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("SMK2", StringComparison.Ordinal))
                {
                    return ".smk";
                }

                if (fileContents.StartsWith("TYPE", StringComparison.Ordinal))
                {
                    return ".pud";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("GIF8", StringComparison.Ordinal))
                {
                    return ".gif";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("\x1Blua", StringComparison.Ordinal))
                {
                    return ".lua";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("ÿØÿà", StringComparison.Ordinal))
                {
                    return ".jpg";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("fLaC", StringComparison.Ordinal))
                {
                    return ".flac";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("HM3W", StringComparison.Ordinal))
                {
                    //could also be w3m
                    return ".w3x";
                }

                if (fileContents.StartsWith("W3do", StringComparison.Ordinal))
                {
                    return ".doo";
                }

                if (fileContents.StartsWith("W3E!", StringComparison.Ordinal))
                {
                    return ".w3e";
                }

                if (fileContents.StartsWith("MP3W", StringComparison.Ordinal))
                {
                    return ".wpm";
                }

                if (fileContents.StartsWith("WTG!", StringComparison.Ordinal))
                {
                    return ".wtg";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("MM", StringComparison.Ordinal))
                {
                    //todo: add parser to look for textures?
                    return ".3ds";
                }

                if (fileContents.Length >= 2 && fileContents[0] == '\xFF' && (fileContents[1] >= '\xE0' && fileContents[1] <= '\xFF'))
                {
                    DebugSettings.Warn("Delete this file detection if breakpoint is never hit");
                    return ".mp3";
                }

                if (fileContents.Contains("Saved by D3DX", StringComparison.InvariantCultureIgnoreCase))
                {
                    DebugSettings.Warn("Delete this file detection if breakpoint is never hit");
                    return ".tga";
                }
            }
            catch { }
            finally
            {
                try
                {
                    stream.Position = 0;
                }
                catch { }
            }

            return null;
        }
    }
}