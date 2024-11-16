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
using War3Net.Build.Audio;
using War3Net.Build.Widget;
using War3Net.Build.Extensions;
using War3Net.Build.Script;
using War3Net.CodeAnalysis.Decompilers;
using War3Net.Build.Object;
using War3Net.Build.Environment;

namespace WC3MapDeprotector
{
    public static class War3NetExtensions
    {
        public static Map Clone_Shallow(this Map map)
        {
            return new Map()
            {
                Sounds = map.Sounds,
                Cameras = map.Cameras,
                Environment = map.Environment,
                PathingMap = map.PathingMap,
                PreviewIcons = map.PreviewIcons,
                Regions = map.Regions,
                ShadowMap = map.ShadowMap,
                ImportedFiles = map.ImportedFiles,
                Info = map.Info,
                AbilityObjectData = map.AbilityObjectData,
                BuffObjectData = map.BuffObjectData,
                DestructableObjectData = map.DestructableObjectData,
                DoodadObjectData = map.DoodadObjectData,
                ItemObjectData = map.ItemObjectData,
                UnitObjectData = map.UnitObjectData,
                UpgradeObjectData = map.UpgradeObjectData,
                AbilitySkinObjectData = map.AbilitySkinObjectData,
                BuffSkinObjectData = map.BuffSkinObjectData,
                DestructableSkinObjectData = map.DestructableSkinObjectData,
                DoodadSkinObjectData = map.DoodadSkinObjectData,
                ItemSkinObjectData = map.ItemSkinObjectData,
                UnitSkinObjectData = map.UnitSkinObjectData,
                UpgradeSkinObjectData = map.UpgradeSkinObjectData,
                CustomTextTriggers = map.CustomTextTriggers,
                Script = map.Script,
                Triggers = map.Triggers,
                TriggerStrings = map.TriggerStrings,
                Doodads = map.Doodads,
                Units = map.Units
            };
        }

        public static void ConcatObjectData(this Map map, Map otherMap)
        {
            map.AbilityObjectData ??= new AbilityObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.AbilityObjectData != null)
            {
                map.AbilityObjectData.BaseAbilities.AddRange(otherMap.AbilityObjectData.BaseAbilities);
                map.AbilityObjectData.NewAbilities.AddRange(otherMap.AbilityObjectData.NewAbilities);
            }

            map.BuffObjectData ??= new BuffObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.BuffObjectData != null)
            {
                map.BuffObjectData.BaseBuffs.AddRange(otherMap.BuffObjectData.BaseBuffs);
                map.BuffObjectData.NewBuffs.AddRange(otherMap.BuffObjectData.NewBuffs);
            }

            map.DestructableObjectData ??= new DestructableObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.DestructableObjectData != null)
            {
                map.DestructableObjectData.BaseDestructables.AddRange(otherMap.DestructableObjectData.BaseDestructables);
                map.DestructableObjectData.NewDestructables.AddRange(otherMap.DestructableObjectData.NewDestructables);
            }

            map.DoodadObjectData ??= new DoodadObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.DoodadObjectData != null)
            {
                map.DoodadObjectData.BaseDoodads.AddRange(otherMap.DoodadObjectData.BaseDoodads);
                map.DoodadObjectData.NewDoodads.AddRange(otherMap.DoodadObjectData.NewDoodads);
            }

            map.ItemObjectData ??= new ItemObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.ItemObjectData != null)
            {
                map.ItemObjectData.BaseItems.AddRange(otherMap.ItemObjectData.BaseItems);
                map.ItemObjectData.NewItems.AddRange(otherMap.ItemObjectData.NewItems);
            }

            map.UnitObjectData ??= new UnitObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.UnitObjectData != null)
            {
                map.UnitObjectData.BaseUnits.AddRange(otherMap.UnitObjectData.BaseUnits);
                map.UnitObjectData.NewUnits.AddRange(otherMap.UnitObjectData.NewUnits);
            }

            map.UpgradeObjectData ??= new UpgradeObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.UpgradeObjectData != null)
            {
                map.UpgradeObjectData.BaseUpgrades.AddRange(otherMap.UpgradeObjectData.BaseUpgrades);
                map.UpgradeObjectData.NewUpgrades.AddRange(otherMap.UpgradeObjectData.NewUpgrades);
            }

            map.AbilitySkinObjectData ??= new AbilityObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.AbilitySkinObjectData != null)
            {
                map.AbilitySkinObjectData.BaseAbilities.AddRange(otherMap.AbilitySkinObjectData.BaseAbilities);
                map.AbilitySkinObjectData.NewAbilities.AddRange(otherMap.AbilitySkinObjectData.NewAbilities);
            }

            map.BuffSkinObjectData ??= new BuffObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.BuffSkinObjectData != null)
            {
                map.BuffSkinObjectData.BaseBuffs.AddRange(otherMap.BuffSkinObjectData.BaseBuffs);
                map.BuffSkinObjectData.NewBuffs.AddRange(otherMap.BuffSkinObjectData.NewBuffs);
            }

            map.DestructableSkinObjectData ??= new DestructableObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.DestructableSkinObjectData != null)
            {
                map.DestructableSkinObjectData.BaseDestructables.AddRange(otherMap.DestructableSkinObjectData.BaseDestructables);
                map.DestructableSkinObjectData.NewDestructables.AddRange(otherMap.DestructableSkinObjectData.NewDestructables);
            }

            map.DoodadSkinObjectData ??= new DoodadObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.DoodadSkinObjectData != null)
            {
                map.DoodadSkinObjectData.BaseDoodads.AddRange(otherMap.DoodadSkinObjectData.BaseDoodads);
                map.DoodadSkinObjectData.NewDoodads.AddRange(otherMap.DoodadSkinObjectData.NewDoodads);
            }

            map.ItemSkinObjectData ??= new ItemObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.ItemSkinObjectData != null)
            {
                map.ItemSkinObjectData.BaseItems.AddRange(otherMap.ItemSkinObjectData.BaseItems);
                map.ItemSkinObjectData.NewItems.AddRange(otherMap.ItemSkinObjectData.NewItems);
            }

            map.UnitSkinObjectData ??= new UnitObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.UnitSkinObjectData != null)
            {
                map.UnitSkinObjectData.BaseUnits.AddRange(otherMap.UnitSkinObjectData.BaseUnits);
                map.UnitSkinObjectData.NewUnits.AddRange(otherMap.UnitSkinObjectData.NewUnits);
            }

            map.UpgradeSkinObjectData ??= new UpgradeObjectData(ObjectDataFormatVersion.v3);
            if (otherMap?.UpgradeSkinObjectData != null)
            {
                map.UpgradeSkinObjectData.BaseUpgrades.AddRange(otherMap.UpgradeSkinObjectData.BaseUpgrades);
                map.UpgradeSkinObjectData.NewUpgrades.AddRange(otherMap.UpgradeSkinObjectData.NewUpgrades);
            }
        }

        public static string GetVariableName_BugFixPendingPR(this UnitData unitData)
        {
            var result = unitData.GetVariableName();
            if (unitData.IsItem())
            {
                return result.Replace("gg_unit_", "gg_item_");
            }

            return result;
        }

        public static string GetVariableName(this War3Net.Build.Environment.Region region)
        {
            return $"gg_rct_{region.Name.Replace(' ', '_')}";
        }

        public static string GetVariableName(this Sound sound)
        {
            return $"gg_snd_{sound.Name.Replace(' ', '_')}";
        }

        public static string GetVariableName(this Camera camera)
        {
            return $"gg_cam_{camera.Name.Replace(' ', '_')}";
        }

        public static string GetVariableName(this TriggerDefinition trigger)
        {
            return $"gg_trg_{trigger.Name.Replace(' ', '_')}";
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

        public static Dictionary<string, Dictionary<string, List<object>>> GetObjectData(this Map map, bool replaceTriggerStrings = true)
        {
            var result = new Dictionary<string, Dictionary<string, List<object>>>();

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
                        var objectId = child.ToString();
                        if (!result.TryGetValue(objectId, out var objectHashTable))
                        {
                            objectHashTable = new Dictionary<string, List<object>>();
                            result[objectId] = objectHashTable;
                        }

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
                            var value = valueProperty.GetValue(modification);

                            if (value is string valueString)
                            {
                                const string TRIGSTR_ = "TRIGSTR_";
                                if (replaceTriggerStrings && valueString.StartsWith(TRIGSTR_, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    if (int.TryParse(valueString.Substring(TRIGSTR_.Length), out var key))
                                    {
                                        value = map.TriggerStrings.Strings.FirstOrDefault(x => x.Key == key)?.Value ?? value;
                                    }
                                }
                            }

                            var rawCode = id.ToRawcode();
                            if (!objectHashTable.TryGetValue(rawCode, out var list))
                            {
                                list = new List<object>();
                                objectHashTable[rawCode] = list;
                            }

                            if (!list.Contains(value))
                            {
                                list.Add(value);
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static List<string> GetObjectDataStringValues(this Map map, List<string> rawCodes = null)
        {
            var result = GetObjectData(map);
            var identifierToRawCode = new HashSet<int>();
            if (rawCodes != null)
            {
                identifierToRawCode.AddRange(rawCodes.Select(x => x.FromRawcode()));
                result = result.Where(x => rawCodes.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            }

            return result.Values.SelectMany(x => x.SelectMany(y => y.Value)).OfType<string>().Distinct().ToList();
        }

        public static List<MpqKnownFile> GetObjectDataFiles(this Map map, bool ignoreExceptions = false)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName?.Contains("War3Net") ?? false).SelectMany(x => x.GetTypes()).Where(x => x.Name == "MapExtensions").SelectMany(x =>
            {
                var methods = x.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name.StartsWith("get", StringComparison.InvariantCultureIgnoreCase) && x.Name.Contains("ObjectData", StringComparison.InvariantCultureIgnoreCase) && x.Name.EndsWith("file", StringComparison.InvariantCultureIgnoreCase));
                return methods.Select(x =>
                {
                    try
                    {
                        return x.Invoke(null, new object[] { map, null }) as MpqFile;
                    }
                    catch
                    {
                        if (ignoreExceptions)
                        {
                            return null;
                        }

                        throw;
                    }
                }).OfType<MpqKnownFile>().ToList();
            }).Where(x => x != null).ToList();
        }

        public static List<MpqKnownFile> GetAllFiles(this Map map, bool ignoreExceptions = false)
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

            return AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName?.Contains("War3Net") ?? false).SelectMany(x => x.GetTypes()).Where(x => x.Name == "MapExtensions").SelectMany(x =>
            {
                var methods = x.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name.StartsWith("get", StringComparison.InvariantCultureIgnoreCase) && x.Name.EndsWith("file", StringComparison.InvariantCultureIgnoreCase));
                return methods.Select(x =>
                {
                    try
                    {
                        return x.Invoke(null, new object[] { map, null }) as MpqFile;
                    }
                    catch
                    {
                        if (ignoreExceptions)
                        {
                            return null;
                        }

                        throw;
                    }
                }).OfType<MpqKnownFile>().ToList();
            }).Where(x => x != null).ToList();
        }

        public static List<FunctionDeclarationContext> ParseScriptForNestedFunctionCalls(this IDictionary<string, FunctionDeclarationContext> contextFunctionDeclarations, string customText)
        {
            var keywords = customText.Split(new char[] { ',', '(', ')', ' ', '\t' }).Select(x => x.Trim()).Distinct().ToList();
            return keywords.Select(x => contextFunctionDeclarations.TryGetValue(x, out var function) ? function : null).Where(x => x != null).Distinct().ToList();
        }

        public static string RenderFunctionAsString(this FunctionDeclarationContext function)
        {
            return function.FunctionDeclaration.RenderFunctionAsString();
        }

        public static string RenderFunctionAsString(this JassFunctionDeclarationSyntax function)
        {
            using (var scriptWriter = new StringWriter())
            {
                var renderer = new JassRenderer(scriptWriter);
                renderer.Render(function);
                renderer.RenderNewLine();
                return scriptWriter.GetStringBuilder().ToString();
            }
        }

        public static IEnumerable<TriggerFunction> RecurseNestedTriggerFunctions(this TriggerFunction triggerFunction)
        {
            if (triggerFunction == null)
            {
                yield break;
            }

            yield return triggerFunction;
            if (triggerFunction.ChildFunctions != null)
            {
                foreach (var childFunction in triggerFunction.ChildFunctions)
                {
                    foreach (var nestedChildFunction in RecurseNestedTriggerFunctions(childFunction))
                    {
                        yield return nestedChildFunction;
                    }
                }
            }

            if (triggerFunction.Parameters != null)
            {
                foreach (var parameter in triggerFunction.Parameters)
                {
                    foreach (var nestedChildFunction in RecurseNestedTriggerFunctions(parameter.Function))
                    {
                        yield return nestedChildFunction;
                    }
                }
            }
        }
    }
}