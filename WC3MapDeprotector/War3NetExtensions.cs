using CSharpLua;
using ICSharpCode.Decompiler.Util;
using System.Reflection;
using War3Net.Build;
using War3Net.Build.Info;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.IO.Mpq;
using SixLabors.ImageSharp;
using War3Net.Common.Extensions;
using NuGet.Packaging;

namespace WC3MapDeprotector
{
    public static class War3NetExtensions
    {
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

        public static List<string> GetObjectDataStringValues(this Map map, List<string> rawCodes = null)
        {
            var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var identifierToRawCode = new HashSet<int>();
            if (rawCodes != null)
            {
                identifierToRawCode.AddRange(rawCodes.Select(x => x.FromRawcode()));
            }

            var objectDataProperties = typeof(Map).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.Name.EndsWith("ObjectData", StringComparison.InvariantCultureIgnoreCase)).ToList();
            foreach (var objectDataProperty in objectDataProperties)
            {
                var objectData = objectDataProperty.GetValue(map);
                if (objectData == null)
                {
                    continue;
                }

                var childProperties = objectData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.Name.StartsWith("Base", StringComparison.InvariantCultureIgnoreCase) || x.Name.StartsWith("New", StringComparison.InvariantCultureIgnoreCase)).ToList();
                foreach (var childProperty in childProperties)
                {
                    var childData = (System.Collections.IList)childProperty.GetValue(objectData);
                    if (childData == null || childData.Count == 0)
                    {
                        continue;
                    }
                    
                    var modificationProperty = childData[0].GetType().GetProperty("Modifications", BindingFlags.Public | BindingFlags.Instance);
                    foreach (var child in childData)
                    {
                        var modifications = (System.Collections.IList)modificationProperty.GetValue(child);
                        if (modifications == null || modifications.Count == 0)
                        {
                            continue;
                        }

                        var modificationType = modifications[0].GetType();
                        var idProperty = modificationType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                        var valueProperty = modificationType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                        foreach (var modification in modifications)
                        {
                            var id = (int)idProperty.GetValue(modification);
                            if (identifierToRawCode.Any() && !identifierToRawCode.Contains(id))
                            {
                                continue;
                            }

                            var value = valueProperty.GetValue(modification);
                            if (value is string)
                            {
                                result.Add((string)value);
                            }
                        }
                    }
                }
            }

            return result.ToList();
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