using CSharpLua;
using System.Reflection;
using War3Net.Build;
using War3Net.Build.Info;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Syntax;
using War3Net.IO.Mpq;
using SixLabors.ImageSharp;
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
        public static Dictionary<ObjectDataType, War3NetSkinnableObjectDataWrapper> GetObjectDataCollection_War3Net(this Map map)
        {
            return Enum.GetValues(typeof(ObjectDataType)).Cast<ObjectDataType>().Select(x => new KeyValuePair<ObjectDataType, War3NetSkinnableObjectDataWrapper>(x, GetObjectDataCollectionByType(map, x))).Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value);
        }

        private static War3NetSkinnableObjectDataWrapper GetObjectDataCollectionByType(Map map, ObjectDataType objectDataType)
        {
            //NOTE: War3Net uses a separate class for each type of ObjectData even though they're very similar, so we need to return object
            switch (objectDataType)
            {
                case ObjectDataType.Ability:
                    map.AbilityObjectData ??= new AbilityObjectData(ObjectDataFormatVersion.v3);
                    map.AbilitySkinObjectData ??= new AbilityObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetSkinnableObjectDataWrapper(new War3NetObjectDataCollectionWrapper(map.AbilityObjectData), new War3NetObjectDataCollectionWrapper(map.AbilitySkinObjectData));
                case ObjectDataType.Buff:
                    map.BuffObjectData ??= new BuffObjectData(ObjectDataFormatVersion.v3);
                    map.BuffSkinObjectData ??= new BuffObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetSkinnableObjectDataWrapper(new War3NetObjectDataCollectionWrapper(map.BuffObjectData), new War3NetObjectDataCollectionWrapper(map.BuffSkinObjectData));
                case ObjectDataType.Destructable:
                    map.DestructableObjectData ??= new DestructableObjectData(ObjectDataFormatVersion.v3);
                    map.DestructableSkinObjectData ??= new DestructableObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetSkinnableObjectDataWrapper(new War3NetObjectDataCollectionWrapper(map.DestructableObjectData), new War3NetObjectDataCollectionWrapper(map.DestructableSkinObjectData));
                case ObjectDataType.Doodad:
                    map.DoodadObjectData ??= new DoodadObjectData(ObjectDataFormatVersion.v3);
                    map.DoodadSkinObjectData ??= new DoodadObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetSkinnableObjectDataWrapper(new War3NetObjectDataCollectionWrapper(map.DoodadObjectData), new War3NetObjectDataCollectionWrapper(map.DoodadSkinObjectData));
                case ObjectDataType.Item:
                    map.ItemObjectData ??= new ItemObjectData(ObjectDataFormatVersion.v3);
                    map.ItemSkinObjectData ??= new ItemObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetSkinnableObjectDataWrapper(new War3NetObjectDataCollectionWrapper(map.ItemObjectData), new War3NetObjectDataCollectionWrapper(map.ItemSkinObjectData));
                case ObjectDataType.Unit:
                    map.UnitObjectData ??= new UnitObjectData(ObjectDataFormatVersion.v3);
                    map.UnitSkinObjectData ??= new UnitObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetSkinnableObjectDataWrapper(new War3NetObjectDataCollectionWrapper(map.UnitObjectData), new War3NetObjectDataCollectionWrapper(map.UnitSkinObjectData));
                case ObjectDataType.Upgrade:
                    map.UpgradeObjectData ??= new UpgradeObjectData(ObjectDataFormatVersion.v3);
                    map.UpgradeSkinObjectData ??= new UpgradeObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetSkinnableObjectDataWrapper(new War3NetObjectDataCollectionWrapper(map.UpgradeObjectData), new War3NetObjectDataCollectionWrapper(map.UpgradeSkinObjectData));
                case ObjectDataType.GameplayConstants:
                    return null; // todo: not supported by War3Net yet
                case ObjectDataType.Unknown:
                    DebugSettings.Warn("Trying to convert unknown ObjectData format to War3Net");
                    return null;
                case ObjectDataType.Mix_Of_Multiple:
                    DebugSettings.Warn("Need to split ObjectData of mixed types before converting to War3Net");
                    return null;
                default:
                    throw new NotImplementedException();
            }
        }

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
            return syntaxNode.DFS_Flatten_Lazy(GetAllChildSyntaxNodes).ToList();
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