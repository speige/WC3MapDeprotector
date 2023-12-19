using CSharpLua;
using ICSharpCode.Decompiler.Util;
using System.Reflection;
using System.Text;
using War3Net.Build;
using War3Net.Build.Info;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.IO.Mpq;
using SixLabors.ImageSharp;

namespace WC3MapDeprotector
{
    public static class War3NetExtensions
    {
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

                using (var reader = new BinaryReader(stream))
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

            return null;
        }

        private static Dictionary<ulong, string> GetUnknownFileName_Memoized = new Dictionary<ulong, string>();
        public static string GetUnknownFileName(this MpqFile mpqFile)
        {
            var hash = mpqFile.Name;
            if (!GetUnknownFileName_Memoized.TryGetValue(hash, out var result))
            {
                result = mpqFile.Name + PredictUnknownFileExtension(mpqFile.MpqStream);
                GetUnknownFileName_Memoized[hash] = result;
            }

            return result;
        }

        public static string RenderScriptAsString(this JassCompilationUnitSyntax compilationUnit)
        {
            using (var writer = new StringWriter())
            {
                var renderer = new JassRenderer(writer);
                renderer.Render(compilationUnit);
                return writer.GetStringBuilder().ToString();
            }
        }

        public static List<object> GetAllChildSyntaxNodes(object syntaxNode)
        {
            var properties = syntaxNode.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
            return properties.Select(p => p.GetValue(syntaxNode)).Where(y => y != null && y.GetType().Namespace.StartsWith("War3Net.CodeAnalysis.Jass.Syntax")).ToList();
        }

        public static List<object> GetAllChildSyntaxNodes_Recursive(object syntaxNode)
        {
            return syntaxNode.DFS_Flatten(GetAllChildSyntaxNodes).ToList();
        }

        public static List<MpqKnownFile> GetAllFiles(this Map map)
        {
            //NOTE: w3i & war3mapunits.doo versions have to correlate or the editor crashes.
            //having mismatch can cause world editor to be very slow & take tons of memory, if it doesn't crash
            //not sure how to know compatability, but current guess is Units.UseNewFormat can't be used for MapInfoFormatVersion < v28

            if (map.Info == null)
            {
                map.Info = new MapInfo(default);
                var mapInfoVersion = Enum.GetValues(typeof(MapInfoFormatVersion)).Cast<MapInfoFormatVersion>().OrderByDescending(x => x).First();
                var editorVersion = Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>().OrderByDescending(x => x).First();
                map.Info.FormatVersion = mapInfoVersion;
                map.Info.GameVersion = new Version(1, 36, 1, 20719);
            }


            if (int.TryParse((map.Info.MapName ?? "").Replace("TRIGSTR_", ""), out var trgStr1))
            {
                var str = map.TriggerStrings.Strings.FirstOrDefault(x => x.Key == trgStr1);
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

            map.Info.CampaignBackgroundNumber = -1;
            map.Info.LoadingScreenBackgroundNumber = -1;
            map.Info.LoadingScreenPath = "\u004C\u006F\u0061\u0064\u0069\u006E\u0067\u0053\u0063\u0072\u0065\u0065\u006E\u002E\u006D\u0064\u0078";

            return AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName?.Contains("War3Net") ?? false).SelectMany(x => x.GetTypes()).Where(x => x.Name == "MapExtensions").SelectMany(x =>
            {
                var methods = x.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name.StartsWith("get", StringComparison.InvariantCultureIgnoreCase) && x.Name.EndsWith("file", StringComparison.InvariantCultureIgnoreCase));
                return methods.Select(x =>
                {
                    return x.Invoke(null, new object[] { map, null }) as MpqFile;
                }).Where(x => x is MpqKnownFile).Cast<MpqKnownFile>().ToList();
            }).ToList();
        }
    }
}