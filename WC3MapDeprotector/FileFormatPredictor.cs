using NAudio.Wave;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WC3MapDeprotector
{
    public static partial class FileFormatPredictor
    {
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
                        return "bmp";
                    }

                    return format?.FileExtensions.FirstOrDefault() ?? "tga";
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
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    stream.Position = 0;

                    // NAudio sometimes crashes during garbage collection due to stream already being disposed if it uses the shared stream that the other methods are using
                    using (var mp3 = new Mp3FileReader(memoryStream))
                    {
                        return mp3.TotalTime.TotalSeconds >= .25;
                    }
                }
            }
            catch { }

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

        [GeneratedRegex(@"\s*function\s+\S+\s+takes\s+nothing\s+returns\s+nothing", RegexOptions.IgnoreCase)]
        private static partial Regex Regex_JassScript();

        [GeneratedRegex(@"\s*function\s+\S+\s*\([a-z, \t]*\)", RegexOptions.IgnoreCase)]
        private static partial Regex Regex_LuaScript();
        [GeneratedRegex(@"\s*require\s+'[^']+'", RegexOptions.IgnoreCase)]
        private static partial Regex Regex_LuaScript2();

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
                var isProbablyAscii = !isProbablyBinaryOrUnicode;

                var utf8String = Encoding.UTF8.GetString(bytes);
                var nonReadableUtf8Count = 0;
                foreach (var character in utf8String)
                {
                    var charBytes = Encoding.UTF8.GetBytes(character.ToString());
                    if (charBytes.Length == 1)
                    {
                        if (character >= 127 || (character >= '\0' && character < (byte)' '))
                        {
                            nonReadableUtf8Count++;
                        }
                    }
                    else
                    {
                        var category = char.GetUnicodeCategory(character);
                        if (category == UnicodeCategory.OtherNotAssigned || category == UnicodeCategory.PrivateUse || category == UnicodeCategory.Control)
                        {
                            nonReadableUtf8Count++;
                        }
                    }
                }
                var isProbablyUTF8 = isProbablyBinaryOrUnicode && (nonReadableUtf8Count <= Math.Max(utf8String.Length * .1, 1));

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
                if (lines.Any(x => Regex_LuaScript2().IsMatch(x)))
                {
                    return ".lua";
                }

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

                if (IsMp3AudioFile(stream))
                {
                    //NOTE: mp3 gets false positives sometimes, so this should be below any higher-priority file extensions. Replace with different library instead of NAudio?
                    //todo: move up higher to see if I fixed it by resetting stream position to 0 before reading
                    return ".mp3";
                }

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("ID3", StringComparison.Ordinal))
                {
                    return ".mp3";
                }

                if (isProbablyBinaryOrUnicode && fileContents.Contains("Lavf", StringComparison.InvariantCultureIgnoreCase) && fileContents.Contains("LAME", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".mp3";
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

                if (fileContents.Contains("TRUEVISION-XFILE", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ".tga";
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

                if (isProbablyBinaryOrUnicode && fileContents.StartsWith("\x1BLua", StringComparison.Ordinal))
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

                if (fileContents.StartsWith("RIFF", StringComparison.Ordinal) && fileContents.Contains("WAVEfmt", StringComparison.Ordinal))
                {
                    return ".wav";
                }

                if (Regex_HtmlTags().Matches(fileContents).Count >= lines.Count)
                {
                    return ".html";
                }

                var iniLines = lines.Count(x => (x.Length - x.Replace("=", "").Length) == 1 || (!x.Contains("=") && x.Trim().StartsWith("[") && x.Trim().EndsWith("]")));
                if (iniLines >= Math.Max(lines.Count / 2, 1))
                {
                    //note: could technically also be ini, but unlikely
                    return ".txt";
                }

                if (fileContents.Length >= 2 && fileContents[0] == '\xFF' && (fileContents[1] >= '\xE0' && fileContents[1] <= '\xFF'))
                {
                    return ".mp3";
                }

                if (fileContents.Contains("Saved by D3DX", StringComparison.InvariantCultureIgnoreCase))
                {
                    DebugSettings.Warn("Delete this file detection if breakpoint is never hit");
                    return ".tga";
                }

                if (isProbablyAscii || isProbablyUTF8)
                {
                    return ".txt";
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