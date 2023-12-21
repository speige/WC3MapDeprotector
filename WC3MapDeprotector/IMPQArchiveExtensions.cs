using CSharpLua;
using System.Text;
using War3Net.IO.Mpq;

namespace WC3MapDeprotector
{
    public static class IMPQArchiveExtensions
    {
        private static readonly List<string> _defaultListFile = new List<string>()
        {
                "(attributes)",
                "(listfile)",
                "(signature)",
                "(user data)",
                "war3map.w3s",
                "war3map.w3c",
                "war3map.w3e",
                "war3map.wpm",
                "war3map.mmp",
                "war3map.w3r",
                "war3map.shd",
                "war3campaign.w3f",
                "war3map.w3i",
                "war3campaign.w3a",
                "war3campaign.w3h",
                "war3campaign.w3b",
                "war3campaign.w3d",
                "war3campaign.w3t",
                "war3campaign.w3u",
                "war3campaign.w3q",
                "war3campaignSkin.w3a",
                "war3campaignSkin.w3h",
                "war3campaignSkin.w3b",
                "war3campaignSkin.w3d",
                "war3campaignSkin.w3t",
                "war3campaignSkin.w3u",
                "war3campaignSkin.w3q",
                "war3map.w3a",
                "war3map.w3h",
                "war3map.w3b",
                "war3map.w3d",
                "war3map.w3t",
                "war3map.w3u",
                "war3map.w3q",
                "war3mapSkin.w3a",
                "war3mapSkin.w3h",
                "war3mapSkin.w3b",
                "war3mapSkin.w3d",
                "war3mapSkin.w3t",
                "war3mapSkin.w3u",
                "war3mapSkin.w3q",
                "war3campaign.wts",
                "war3map.wct",
                "war3map.wtg",
                "war3map.wts",
                "war3map.j",
                @"scripts\war3map.j",
                "war3map.lua",
                @"scripts\war3map.lua",
                "war3map.doo",
                "war3mapUnits.doo",
                "war3campaign.imp",
                "war3map.imp",
                "war3mapPreview.tga",
                "war3mapPreview.blp",
                "war3mapPath.tga",
                "war3mapMap.tga",
                "war3mapMap.blp",
                "war3mapSkin.txt",
                "war3mapMisc.txt",
                "war3mapExtra.txt",
                "UpgradeData.slk",
                "UnitWeapons.slk",
                "UnitUI.slk",
                "UnitData.slk",
                "UnitBalance.slk",
                "UnitAbilities.slk",
                "ItemData.slk",
                "AbilityData.slk",
                "AbilityBuffData.slk",
                "conversation.json"
        };

        public static Dictionary<ulong, string> ConvertListFileToRainbowTable(List<string> fileNames)
        {
            var result = new ConcurrentList<KeyValuePair<ulong, string>>();
            Parallel.ForEach(fileNames, fileName =>
            {
                if (MPQHashing.TryHashFileName(fileName, out var hash))
                {
                    result.Add(new KeyValuePair<ulong, string>(hash, fileName));
                }
            });

            return result.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.First().Value);
        }
        
        public static void ProcessDefaultListFile(this IMPQArchive archive)
        {
            archive.ProcessListFile_Slow(_defaultListFile);
        }

        public static void ProcessListFile(this IMPQArchive archive, Dictionary<ulong, string> rainbowTable)
        {
            var verifiedFileNames = archive.UnknownFileNameHashes.Select(x => rainbowTable.TryGetValue(x, out var fileName) ? fileName : null).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            ProcessListFile_Slow(archive, verifiedFileNames);
        }

        public static void ProcessListFile_Slow(this IMPQArchive archive, List<string> fileNames)
        {
            foreach (var file in fileNames)
            {
                archive.DiscoverFile(file);
            }
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

                if (fileContents.StartsWith("blp", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".blp";
                }
                if (fileContents.StartsWith("MDLXVERS", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mdx";
                }
                if (fileContents.StartsWith("DDS", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".dds";
                }
                if (fileContents.Contains("TRUEVISION-XFILE", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".tga";
                }
                if (fileContents.Contains("magosx.com", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("FormatVersion", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mdl";
                }
                if (fileContents.Contains("ID3", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("Lavf", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("LAME", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mp3";
                }
                if (fileContents.StartsWith("RIFF", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("WAVEfmt", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".wav";
                }
                if (fileContents.StartsWith("Mz", StringComparison.InvariantCultureIgnoreCase) && fileContents.Contains("This program cannot be run in DOS mode", StringComparison.InvariantCultureIgnoreCase))
                {
                    //note: could technically also be exe, but unlikely
                    return ".dll";
                }

                var lines = fileContents.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (lines.Count(x => x.EndsWith(".fdf", StringComparison.InvariantCultureIgnoreCase)) >= lines.Count / 2)
                {
                    return ".toc";
                }
                if (lines.Count(x => x.Contains("frame", StringComparison.InvariantCultureIgnoreCase) || x.Contains("IncludeFile", StringComparison.InvariantCultureIgnoreCase) || x.Contains("Template", StringComparison.InvariantCultureIgnoreCase) || x.Contains("Control", StringComparison.InvariantCultureIgnoreCase) || x.Contains("INHERITS", StringComparison.InvariantCultureIgnoreCase)) >= lines.Count / 10)
                {
                    return ".fdf";
                }
                var avgSemicolonCount = lines.Average(x => x.Length - x.Replace(";", "").Length);
                if (avgSemicolonCount >= 1)
                {
                    return ".slk";
                }

                if (fileContents.Length >= 2 && fileContents[0] == '\xFF' && (fileContents[1] >= '\xE0' && fileContents[1] <= '\xFF'))
                {
                    return ".mp3";
                }

                if (fileContents.StartsWith("\xFF\xD8") || fileContents.Contains("JFIF", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains("Exif", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".jpg";
                }

                if (fileContents.StartsWith("\x42\x4D"))
                {
                    return ".bmp";
                }

                if (fileContents.Contains("Saved by D3DX", StringComparison.InvariantCultureIgnoreCase))
                {
                    return PredictImageFileExtension(stream) ?? ".tga";
                }

                /*
                if (fileContents.StartsWith("SFPK", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".sfk";
                }
                */

                var isTextFile = fileContents.Count(x => x >= ' ' && x <= '~') >= fileContents.Length / 2;
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

                if (fileContents.StartsWith("\x02\x00\0\0") && (fileContents.Contains(".w3m", StringComparison.InvariantCultureIgnoreCase) || fileContents.Contains(".w3x", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return ".wai";
                }

                var imageFormat = PredictImageFileExtension(stream);
                if (!string.IsNullOrWhiteSpace(imageFormat))
                {
                    return imageFormat;
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