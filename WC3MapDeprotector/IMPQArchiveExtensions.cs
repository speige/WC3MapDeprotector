using CSharpLua;
using NAudio.Wave;
using NuGet.Packaging;
using System.Text;
using System.Text.RegularExpressions;

namespace WC3MapDeprotector
{
    public static class IMPQArchiveExtensions
    {
        private static readonly Dictionary<ulong, string> _defaultListFileRainbowTable;
        static IMPQArchiveExtensions()
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

            _defaultListFileRainbowTable = ConvertListFileToRainbowTable(defaultListFile.ToList());
        }

        public static bool IsInDefaultListFile(string fileName)
        {
            return MPQFileNameHash.TryCalculate(fileName, out var hash) && _defaultListFileRainbowTable.ContainsKey(hash);
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
            return archive.ProcessListFile(_defaultListFileRainbowTable);
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

        private static string PredictImageFileExtension(Stream stream)
        {
            try
            {
                return SixLabors.ImageSharp.Image.DetectFormat(stream)?.FileExtensions?.FirstOrDefault();
            }
            catch { }

            return null;
        }

        private static string PredictAudioFileExtension(Stream stream)
        {
            try
            {
                /*
                //todo: test if WC3 supports this file format, if not can delete code
                try
                {
                    stream.Position = 0;
                    new AiffFileReader(stream);
                    return ".aiff";
                }
                catch { }
                */

                try
                {
                    stream.Position = 0;
                    new Mp3FileReader(stream);
                    return ".mp3";
                }
                catch { }

                try
                {
                    stream.Position = 0;
                    new WaveFileReader(stream);
                    return ".wav";
                }
                catch { }
            }
            finally
            {
                stream.Position = 0;
            }

            stream.Position = 0;
            return null;
        }

        public static string PredictUnknownFileExtension(Stream stream)
        {
            try
            {
                string fileContents;
                stream.Position = 0;

                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    var bytes = reader.ReadBytes((int)stream.Length);
                    stream.Position = 0;

                    var stringBuilder = new StringBuilder();
                    for (var i = 0; i < bytes.Length; i++)
                    {
                        stringBuilder.Append((char)bytes[i]);
                    }
                    fileContents = stringBuilder.ToString();
                }

                //todo: detect w3m & w3x for nested campaign maps (test if StormLib can parse)

                //todo: [LowPriority] add real FDF parser? (none online, would need to build my own based on included fdf.g grammar file)

                var isTextFile = fileContents.Count(x => x >= ' ' && x <= '~') >= fileContents.Length * .75;
                var isBinaryFile = !isTextFile;

                if (isBinaryFile && fileContents.StartsWith("blp", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".blp";
                }
                if (isBinaryFile && fileContents.StartsWith("DDS", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".dds";
                }
                if (isBinaryFile && fileContents.StartsWith("MDLXVERS", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mdx";
                }
                if (isTextFile && fileContents.Contains("magosx.com", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("FormatVersion", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mdl";
                }
                if (isBinaryFile && fileContents.StartsWith("Mz", StringComparison.InvariantCultureIgnoreCase) && fileContents.Contains("This program cannot be run in DOS mode", StringComparison.InvariantCultureIgnoreCase))
                {
                    //note: could technically also be exe, but unlikely
                    return ".dll";
                }

                if (isTextFile && Regex.IsMatch(fileContents, "function\\s+config\\s+takes\\s+nothing\\s+returns\\s+nothing", RegexOptions.IgnoreCase))
                {
                    return ".j";
                }

                if (isTextFile && Regex.IsMatch(fileContents, "function\\s+config\\s*\\(\\)", RegexOptions.IgnoreCase))
                {
                    return ".lua";
                }

                var lines = fileContents.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (isTextFile && lines.Count(x => x.EndsWith(".fdf", StringComparison.InvariantCultureIgnoreCase)) >= lines.Count / 2)
                {
                    return ".toc";
                }
                if (isTextFile && lines.Count(x => x.Contains("frame", StringComparison.InvariantCultureIgnoreCase) || x.Contains("IncludeFile", StringComparison.InvariantCultureIgnoreCase) || x.Contains("Template", StringComparison.InvariantCultureIgnoreCase) || x.Contains("Control", StringComparison.InvariantCultureIgnoreCase) || x.Contains("INHERITS", StringComparison.InvariantCultureIgnoreCase)) >= lines.Count / 10)
                {
                    return ".fdf";
                }
                var avgSemicolonCount = lines.Average(x => x.Length - x.Replace(";", "").Length);
                if (isTextFile && avgSemicolonCount >= 1)
                {
                    return ".slk";
                }

                /*
                if (fileContents.StartsWith("SFPK", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".sfk";
                }
                */

                if (isTextFile && lines.Average(x => x.Length - x.Replace("<", "").Replace(">", "").Length) >= 1)
                {
                    return ".html";
                }

                var iniLines = lines.Count(x => (x.Length - x.Replace("=", "").Length) == 1 || (!x.Contains("=") && (x.Length - x.Replace("[", "").Replace("]", "").Length) == 2));
                if (isTextFile && iniLines >= lines.Count / 2)
                {
                    //note: could technically also be ini, but unlikely
                    return ".txt";
                }

                if (isBinaryFile && fileContents.StartsWith("\x02\x00\0\0") && (fileContents.Contains(".w3m", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains(".w3x", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return ".wai";
                }

                if (isBinaryFile)
                {
                    //todo: [LowPriority since regex already covers it] test against mdx parser
                }

                if (isTextFile)
                {
                    //todo: [LowPriority since regex already covers it] test against mdl parser
                }

                if (isBinaryFile)
                {
                    var imageFormat = PredictImageFileExtension(stream);
                    if (!string.IsNullOrWhiteSpace(imageFormat))
                    {
                        return $".{imageFormat}";
                    }
                }

                if (isBinaryFile)
                {
                    var audioFormat = PredictAudioFileExtension(stream);
                    if (!string.IsNullOrWhiteSpace(audioFormat))
                    {
                        return audioFormat;
                    }
                }

                if (isBinaryFile && fileContents.Contains("ID3", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("Lavf", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("LAME", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".mp3";
                }
                if (isBinaryFile && fileContents.StartsWith("RIFF", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("WAVEfmt", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".wav";
                }
                if (isBinaryFile && fileContents.Length >= 2 && fileContents[0] == '\xFF' && (fileContents[1] >= '\xE0' && fileContents[1] <= '\xFF'))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".mp3";
                }
                if (isBinaryFile && fileContents.Contains("TRUEVISION-XFILE", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".tga";
                }
                if (isBinaryFile && fileContents.StartsWith("\xFF\xD8") || fileContents.Contains("JFIF", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("Exif", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".jpg";
                }
                if (isBinaryFile && fileContents.StartsWith("\x42\x4D"))
                {
                    Console.WriteLine("Don't DELETE!");
                    return ".bmp";
                }
                if (isBinaryFile && fileContents.Contains("Saved by D3DX", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Don't DELETE!");
                    return PredictImageFileExtension(stream) ?? ".tga";
                }

                //todo: [LowPriority since DefaultListFile will handle it usually] detect common wc3 formats (test against War3Net SetFile command)

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