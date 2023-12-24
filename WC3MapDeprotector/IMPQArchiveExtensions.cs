using CSharpLua;
using NAudio.Wave;
using System.Text;
using System.Text.RegularExpressions;

namespace WC3MapDeprotector
{
    public static partial class IMPQArchiveExtensions
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
            return MPQFileNameHash.TryCalculate(fileName, out var hash) && _defaultListFileRainbowTable.Value.ContainsKey(hash);
        }

        public static Dictionary<ulong, string> ConvertListFileToRainbowTable(List<string> fileNames)
        {
            var result = new ConcurrentList<KeyValuePair<ulong, string>>();
            Parallel.ForEach(fileNames, fileName =>
            {
                if (MPQFileNameHash.TryCalculate(fileName, out var hash))
                {
                    result.Add(new KeyValuePair<ulong, string>(hash, fileName));
                }
            });

            return result.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.First().Value);
        }
        
        public static List<string> ProcessDefaultListFile(this IMPQArchive archive)
        {
            return archive.ProcessListFile(_defaultListFileRainbowTable.Value);
        }

        public static List<string> ProcessListFile(this IMPQArchive archive, Dictionary<ulong, string> rainbowTable)
        {
            var verifiedFileNames = archive.UnknownFileNameHashes.Select(x => rainbowTable.TryGetValue(x, out var fileName) ? fileName : null).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            return ProcessListFile_Slow(archive, verifiedFileNames);
        }

        public static List<string> ProcessListFile_Slow(this IMPQArchive archive, List<string> fileNames)
        {
            List<string> result = new List<string>();
            foreach (var file in fileNames)
            {
                if (archive.DiscoverFile(file))
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
                var font = SixLabors.Fonts.FontDescription.LoadDescription(stream);
                return "ttf";
            }
            catch { }

            return null;
        }

        private static string PredictImageFileExtension(Stream stream)
        {
            try
            {
                return SixLabors.ImageSharp.Image.DetectFormat(stream)?.FileExtensions?.FirstOrDefault();
            }
            catch { }

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
        private static partial Regex HtmlTags();

        public static string PredictUnknownFileExtension(Stream stream)
        {
            try
            {
                if (stream.Length == 0)
                {
                    return null;
                }

                byte[] bytes;
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    bytes = memoryStream.ToArray();
                    stream.Position = 0;
                }

                var stringBuilder = new StringBuilder();
                for (var i = 0; i < bytes.Length; i++)
                {
                    var character = (char)bytes[i];
                    stringBuilder.Append(character);
                }
                var fileContents = stringBuilder.ToString();

                //todo: detect w3m & w3x for nested campaign maps (test if StormLib can parse)
                if (fileContents.StartsWith("MDLXVERS", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mdx";
                }

                if (fileContents.StartsWith("blp", StringComparison.InvariantCultureIgnoreCase))
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

                if (fileContents.StartsWith("DDS", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".dds";
                }
                if (fileContents.StartsWith("Mz", StringComparison.InvariantCultureIgnoreCase) && fileContents.Contains("This program cannot be run in DOS mode", StringComparison.InvariantCultureIgnoreCase))
                {
                    //note: could technically also be exe, but unlikely
                    return ".dll";
                }
                if (fileContents.StartsWith("\x02\x00\0\0") && (fileContents.Contains(".w3m", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains(".w3x", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return ".wai";
                }

                //todo: [LowPriority since regex already covers it] test against mdx parser

                if (fileContents.Contains("TRUEVISION-XFILE", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".tga";
                }
                if (fileContents.StartsWith("\xFF\xD8") || fileContents.Contains("JFIF", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("Exif", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".jpg";
                }
                if (fileContents.StartsWith("\x42\x4D"))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".bmp";
                }
                if (fileContents.Contains("Saved by D3DX", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Don't DELETE!");
                    return PredictImageFileExtension(stream) ?? ".tga";
                }

                //todo: [LowPriority since DefaultListFile will handle it usually] detect common wc3 formats (test against War3Net SetFile command)

                if (Regex.IsMatch(fileContents, "function\\s+config\\s+takes\\s+nothing\\s+returns\\s+nothing", RegexOptions.IgnoreCase))
                {
                    return ".j";
                }

                if (Regex.IsMatch(fileContents, "function\\s+config\\s*\\(\\)", RegexOptions.IgnoreCase))
                {
                    return ".lua";
                }

                var lines = fileContents.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (lines.Count(x => x.EndsWith(".fdf", StringComparison.InvariantCultureIgnoreCase)) >= lines.Count / 2)
                {
                    return ".toc";
                }

                //todo: [LowPriority since regex already covers it] test against mdl parser
                if (fileContents.Contains("magosx.com", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("FormatVersion", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mdl";
                }

                //todo: [LowPriority] add real FDF parser? (none online, would need to build my own based on included fdf.g grammar file)
                if (lines.Count(x => x.Contains("frame", StringComparison.InvariantCultureIgnoreCase) || x.Contains("IncludeFile", StringComparison.InvariantCultureIgnoreCase) || x.Contains("Template", StringComparison.InvariantCultureIgnoreCase) || x.Contains("Control", StringComparison.InvariantCultureIgnoreCase) || x.Contains("INHERITS", StringComparison.InvariantCultureIgnoreCase)) >= lines.Count / 10)
                {
                    return ".fdf";
                }

                var iniLines = lines.Count(x => (x.Length - x.Replace("=", "").Length) == 1 || (!x.Contains("=") && (x.Length - x.Replace("[", "").Replace("]", "").Length) == 2));
                if (iniLines >= lines.Count / 2)
                {
                    //note: could technically also be ini, but unlikely
                    return ".txt";
                }

                var avgSemicolonCount = lines.Average(x => x.Length - x.Replace(";", "").Length);
                if (avgSemicolonCount >= 1)
                {
                    return ".slk";
                }

                /*
                if (fileContents.StartsWith("SFPK", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".sfk";
                }
                */

                if (HtmlTags().Matches(fileContents).Count >= lines.Count)
                {
                    return ".html";
                }

                if (IsMp3AudioFile(stream))
                {
                    //NOTE: mp3 gets false positives sometimes & also misses valid ones, so this should be last. Replace with different library instead of NAudio?
                    return ".mp3";
                }

                if (fileContents.Contains("ID3", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("Lavf", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("LAME", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mp3";
                }

                if (fileContents.Length >= 2 && fileContents[0] == '\xFF' && (fileContents[1] >= '\xE0' && fileContents[1] <= '\xFF'))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".mp3";
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