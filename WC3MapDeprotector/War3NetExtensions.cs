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
using War3Net.Build.Object;

namespace WC3MapDeprotector
{
    public class War3NetObjectDataModificationWrapper
    {
        public object ObjectDataModification { get; protected set; }
        public War3NetObjectDataModificationWrapper(object objectDataModification)
        {
            ObjectDataModification = objectDataModification;
        }

        public int Level
        {
            get
            {
                switch (ObjectDataModification)
                {
                    case LevelObjectDataModification level:
                        return level.Level;
                }

                return default;
            }
            set
            {
                switch (ObjectDataModification)
                {
                    case LevelObjectDataModification level:
                        level.Level = value;
                        break;
                }
            }
        }

        public int Variation
        {
            get
            {
                switch (ObjectDataModification)
                {
                    case VariationObjectDataModification variation:
                        return variation.Variation;
                }

                return default;
            }
            set
            {
                switch (ObjectDataModification)
                {
                    case VariationObjectDataModification variation:
                        variation.Variation = value;
                        break;
                }
            }
        }

        public int Pointer
        {
            get
            {
                switch (ObjectDataModification)
                {
                    case LevelObjectDataModification level:
                        return level.Pointer;
                    case VariationObjectDataModification variation:
                        return variation.Pointer;
                }

                return default;
            }
            set
            {
                switch (ObjectDataModification)
                {
                    case VariationObjectDataModification variation:
                        variation.Pointer = value;
                        break;
                    case LevelObjectDataModification level:
                        level.Pointer = value;
                        break;
                }
            }
        }

        public int Id
        {
            get
            {
                return ((ObjectDataModification)ObjectDataModification).Id;
            }
            set
            {
                ((ObjectDataModification)ObjectDataModification).Id = value;
            }
        }
        public ObjectDataType Type
        {
            get
            {
                return ((ObjectDataModification)ObjectDataModification).Type;
            }
            set
            {
                ((ObjectDataModification)ObjectDataModification).Type = value;
            }
        }
        public object Value
        {
            get
            {
                return ((ObjectDataModification)ObjectDataModification).Value;
            }
            set
            {
                ((ObjectDataModification)ObjectDataModification).Value = value;
            }
        }

        public override string ToString()
        {
            return ObjectDataModification.ToString();
        }
    }

    public class War3NetObjectModificationWrapper
    {
        public object ObjectModification { get; protected set; }
        public War3NetObjectModificationWrapper(object objectModification)
        {
            ObjectModification = objectModification;
        }

        public override string ToString()
        {
            return ObjectModification.ToString();
        }

        public int OldId
        {
            get
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        return level.OldId;
                    case SimpleObjectModification simple:
                        return simple.OldId;
                    case VariationObjectModification variation:
                        return variation.OldId;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public int NewId
        {
            get
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        return level.NewId;
                    case SimpleObjectModification simple:
                        return simple.NewId;
                    case VariationObjectModification variation:
                        return variation.NewId;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public List<int> Unk
        {
            get
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        return level.Unk;
                    case SimpleObjectModification simple:
                        return simple.Unk;
                    case VariationObjectModification variation:
                        return variation.Unk;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public List<War3NetObjectDataModificationWrapper> Modifications
        {
            get
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        return level.Modifications.Select(x => new War3NetObjectDataModificationWrapper(x)).ToList();
                    case SimpleObjectModification simple:
                        return simple.Modifications.Select(x => new War3NetObjectDataModificationWrapper(x)).ToList();
                    case VariationObjectModification variation:
                        return variation.Modifications.Select(x => new War3NetObjectDataModificationWrapper(x)).ToList();
                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }

    public class War3NetObjectDataWrapper
    {
        public object ObjectData { get; protected set; }
        public War3NetObjectDataWrapper(object objectData)
        {
            ObjectData = objectData;
        }

        public ObjectDataFormatVersion FormatVersion
        {
            get
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        return ability.FormatVersion;
                    case BuffObjectData buff:
                        return buff.FormatVersion;
                    case DestructableObjectData destructable:
                        return destructable.FormatVersion;
                    case DoodadObjectData doodad:
                        return doodad.FormatVersion;
                    case ItemObjectData item:
                        return item.FormatVersion;
                    case UnitObjectData unit:
                        return unit.FormatVersion;
                    case UpgradeObjectData upgrade:
                        return upgrade.FormatVersion;
                }

                throw new NotImplementedException();
            }
            set
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        ability.FormatVersion = value;
                        break;
                    case BuffObjectData buff:
                        buff.FormatVersion = value;
                        break;
                    case DestructableObjectData destructable:
                        destructable.FormatVersion = value;
                        break;
                    case DoodadObjectData doodad:
                        doodad.FormatVersion = value;
                        break;
                    case ItemObjectData item:
                        item.FormatVersion = value;
                        break;
                    case UnitObjectData unit:
                        unit.FormatVersion = value;
                        break;
                    case UpgradeObjectData upgrade:
                        upgrade.FormatVersion = value;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public override string ToString()
        {
            return ObjectData.ToString();
        }

        public string FileExtension()
        {
                switch (ObjectData)
                {
                    case AbilityObjectData:
                        return AbilityObjectData.FileExtension;
                    case BuffObjectData:
                        return BuffObjectData.FileExtension;
                    case DestructableObjectData:
                        return DestructableObjectData.FileExtension;
                    case DoodadObjectData:
                        return DoodadObjectData.FileExtension;
                    case ItemObjectData:
                        return ItemObjectData.FileExtension;
                    case UnitObjectData:
                        return UnitObjectData.FileExtension;
                    case UpgradeObjectData:
                        return UpgradeObjectData.FileExtension;
                    default:
                        throw new NotImplementedException();
                }
        }

        public string CampaignFileName()
        {
            switch (ObjectData)
            {
                case AbilityObjectData:
                    return AbilityObjectData.CampaignFileName;
                case BuffObjectData:
                    return BuffObjectData.CampaignFileName;
                case DestructableObjectData:
                    return DestructableObjectData.CampaignFileName;
                case DoodadObjectData:
                    return DoodadObjectData.CampaignFileName;
                case ItemObjectData:
                    return ItemObjectData.CampaignFileName;
                case UnitObjectData:
                    return UnitObjectData.CampaignFileName;
                case UpgradeObjectData:
                    return UpgradeObjectData.CampaignFileName;
                default:
                    throw new NotImplementedException();
            }
        }

        public string CampaignSkinFileName()
        {
            switch (ObjectData)
            {
                case AbilityObjectData:
                    return AbilityObjectData.CampaignSkinFileName;
                case BuffObjectData:
                    return BuffObjectData.CampaignSkinFileName;
                case DestructableObjectData:
                    return DestructableObjectData.CampaignSkinFileName;
                case DoodadObjectData:
                    return DoodadObjectData.CampaignSkinFileName;
                case ItemObjectData:
                    return ItemObjectData.CampaignSkinFileName;
                case UnitObjectData:
                    return UnitObjectData.CampaignSkinFileName;
                case UpgradeObjectData:
                    return UpgradeObjectData.CampaignSkinFileName;
                default:
                    throw new NotImplementedException();
            }
        }

        public string MapFileName()
        {
            switch (ObjectData)
            {
                case AbilityObjectData:
                    return AbilityObjectData.MapFileName;
                case BuffObjectData:
                    return BuffObjectData.MapFileName;
                case DestructableObjectData:
                    return DestructableObjectData.MapFileName;
                case DoodadObjectData:
                    return DoodadObjectData.MapFileName;
                case ItemObjectData:
                    return ItemObjectData.MapFileName;
                case UnitObjectData:
                    return UnitObjectData.MapFileName;
                case UpgradeObjectData:
                    return UpgradeObjectData.MapFileName;
                default:
                    throw new NotImplementedException();
            }
        }

        public string MapSkinFileName()
        {
            switch (ObjectData)
            {
                case AbilityObjectData:
                    return AbilityObjectData.MapSkinFileName;
                case BuffObjectData:
                    return BuffObjectData.MapSkinFileName;
                case DestructableObjectData:
                    return DestructableObjectData.MapSkinFileName;
                case DoodadObjectData:
                    return DoodadObjectData.MapSkinFileName;
                case ItemObjectData:
                    return ItemObjectData.MapSkinFileName;
                case UnitObjectData:
                    return UnitObjectData.MapSkinFileName;
                case UpgradeObjectData:
                    return UpgradeObjectData.MapSkinFileName;
                default:
                    throw new NotImplementedException();
            }
        }

        public List<War3NetObjectModificationWrapper> BaseValues
        {
            get
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        return ability.BaseAbilities.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case BuffObjectData buff:
                        return buff.BaseBuffs.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case DestructableObjectData destructable:
                        return destructable.BaseDestructables.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case DoodadObjectData doodad:
                        return doodad.BaseDoodads.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case ItemObjectData item:
                        return item.BaseItems.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case UnitObjectData unit:
                        return unit.BaseUnits.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case UpgradeObjectData upgrade:
                        return upgrade.BaseUpgrades.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public List<War3NetObjectModificationWrapper> NewValues
        {
            get
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        return ability.NewAbilities.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case BuffObjectData buff:
                        return buff.NewBuffs.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case DestructableObjectData destructable:
                        return destructable.NewDestructables.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case DoodadObjectData doodad:
                        return doodad.NewDoodads.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case ItemObjectData item:
                        return item.NewItems.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case UnitObjectData unit:
                        return unit.NewUnits.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    case UpgradeObjectData upgrade:
                        return upgrade.NewUpgrades.Select(x => new War3NetObjectModificationWrapper(x)).ToList();
                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }

    public static class War3NetExtensions
    {
        public static War3NetObjectDataWrapper GetObjectDataBySLKType(this Map map, SLKType slkType)
        {
            //NOTE: War3Net uses a separate class for each type of ObjectData even though they're very similar, so we need to return object
            switch (slkType)
            {
                case SLKType.Ability:
                    map.AbilityObjectData ??= new AbilityObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.AbilityObjectData);
                case SLKType.Buff:
                    map.BuffObjectData ??= new BuffObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.BuffObjectData);
                case SLKType.Destructable:
                    map.DestructableObjectData ??= new DestructableObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.DestructableObjectData);
                case SLKType.Doodad:
                    map.DoodadObjectData ??= new DoodadObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.DoodadObjectData);
                case SLKType.Item:
                    map.ItemObjectData ??= new ItemObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.ItemObjectData);
                case SLKType.Unit:
                    map.UnitObjectData ??= new UnitObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.UnitObjectData);
                case SLKType.Upgrade:
                    map.UpgradeObjectData ??= new UpgradeObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.UpgradeObjectData);
                default:
                    throw new NotImplementedException();
            }
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