using System.Text;
using War3Net.Build;
using War3Net.Build.Extensions;
using War3Net.Build.Object;
using War3Net.Common.Extensions;
using War3Net.Common.Providers;
using War3Net.IO.Slk;

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
            set
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        level.OldId = value;
                        break;
                    case SimpleObjectModification simple:
                        simple.OldId = value;
                        break;
                    case VariationObjectModification variation:
                        variation.OldId = value;
                        break;
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
            set
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        level.NewId = value;
                        break;
                    case SimpleObjectModification simple:
                        simple.NewId = value;
                        break;
                    case VariationObjectModification variation:
                        variation.NewId = value;
                        break;
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

        public IEnumerable<War3NetObjectDataModificationWrapper> Modifications
        {
            get
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        return level.Modifications.Select(x => new War3NetObjectDataModificationWrapper(x)).ToList().AsReadOnly();
                    case SimpleObjectModification simple:
                        return simple.Modifications.Select(x => new War3NetObjectDataModificationWrapper(x)).ToList().AsReadOnly();
                    case VariationObjectModification variation:
                        return variation.Modifications.Select(x => new War3NetObjectDataModificationWrapper(x)).ToList().AsReadOnly();
                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        level.Modifications.Clear();
                        level.Modifications.AddRange(value.Select(x => (LevelObjectDataModification)x.ObjectDataModification));
                        break;
                    case SimpleObjectModification simple:
                        simple.Modifications.Clear();
                        simple.Modifications.AddRange(value.Select(x => (SimpleObjectDataModification)x.ObjectDataModification));
                        break;
                    case VariationObjectModification variation:
                        variation.Modifications.Clear();
                        variation.Modifications.AddRange(value.Select(x => (VariationObjectDataModification)x.ObjectDataModification));
                        break;
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

        public byte[] Serialize(Encoding encoding = null)
        {
            if (ObjectData is null)
            {
                return new byte[0];
            }

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream, encoding ?? UTF8EncodingProvider.StrictUTF8, true))
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        writer.Write(ability);
                        break;
                    case BuffObjectData buff:
                        writer.Write(buff);
                        break;
                    case DestructableObjectData destructable:
                        writer.Write(destructable);
                        break;
                    case DoodadObjectData doodad:
                        writer.Write(doodad);
                        break;
                    case ItemObjectData item:
                        writer.Write(item);
                        break;
                    case UnitObjectData unit:
                        writer.Write(unit);
                        break;
                    case UpgradeObjectData upgrade:
                        writer.Write(upgrade);
                        break;
                }
                writer.Flush();
                return memoryStream.ToArray();
            }
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

        public IEnumerable<War3NetObjectModificationWrapper> BaseValues
        {
            get
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        return ability.BaseAbilities.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case BuffObjectData buff:
                        return buff.BaseBuffs.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case DestructableObjectData destructable:
                        return destructable.BaseDestructables.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case DoodadObjectData doodad:
                        return doodad.BaseDoodads.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case ItemObjectData item:
                        return item.BaseItems.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case UnitObjectData unit:
                        return unit.BaseUnits.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case UpgradeObjectData upgrade:
                        return upgrade.BaseUpgrades.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        ability.BaseAbilities.Clear();
                        ability.BaseAbilities.AddRange(value.Select(x => (LevelObjectModification)x.ObjectModification));
                        break;
                    case BuffObjectData buff:
                        buff.BaseBuffs.Clear();
                        buff.BaseBuffs.AddRange(value.Select(x => (SimpleObjectModification)x.ObjectModification));
                        break;
                    case DestructableObjectData destructable:
                        destructable.BaseDestructables.Clear();
                        destructable.BaseDestructables.AddRange(value.Select(x => (SimpleObjectModification)x.ObjectModification));
                        break;
                    case DoodadObjectData doodad:
                        doodad.BaseDoodads.Clear();
                        doodad.BaseDoodads.AddRange(value.Select(x => (VariationObjectModification)x.ObjectModification));
                        break;
                    case ItemObjectData item:
                        item.BaseItems.Clear();
                        item.BaseItems.AddRange(value.Select(x => (SimpleObjectModification)x.ObjectModification));
                        break;
                    case UnitObjectData unit:
                        unit.BaseUnits.Clear();
                        unit.BaseUnits.AddRange(value.Select(x => (SimpleObjectModification)x.ObjectModification));
                        break;
                    case UpgradeObjectData upgrade:
                        upgrade.BaseUpgrades.Clear();
                        upgrade.BaseUpgrades.AddRange(value.Select(x => (LevelObjectModification)x.ObjectModification));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public IEnumerable<War3NetObjectModificationWrapper> NewValues
        {
            get
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        return ability.NewAbilities.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case BuffObjectData buff:
                        return buff.NewBuffs.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case DestructableObjectData destructable:
                        return destructable.NewDestructables.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case DoodadObjectData doodad:
                        return doodad.NewDoodads.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case ItemObjectData item:
                        return item.NewItems.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case UnitObjectData unit:
                        return unit.NewUnits.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    case UpgradeObjectData upgrade:
                        return upgrade.NewUpgrades.Select(x => new War3NetObjectModificationWrapper(x)).ToList().AsReadOnly();
                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                switch (ObjectData)
                {
                    case AbilityObjectData ability:
                        ability.NewAbilities.Clear();
                        ability.NewAbilities.AddRange(value.Select(x => (LevelObjectModification)x.ObjectModification));
                        break;
                    case BuffObjectData buff:
                        buff.NewBuffs.Clear();
                        buff.NewBuffs.AddRange(value.Select(x => (SimpleObjectModification)x.ObjectModification));
                        break;
                    case DestructableObjectData destructable:
                        destructable.NewDestructables.Clear();
                        destructable.NewDestructables.AddRange(value.Select(x => (SimpleObjectModification)x.ObjectModification));
                        break;
                    case DoodadObjectData doodad:
                        doodad.NewDoodads.Clear();
                        doodad.NewDoodads.AddRange(value.Select(x => (VariationObjectModification)x.ObjectModification));
                        break;
                    case ItemObjectData item:
                        item.NewItems.Clear();
                        item.NewItems.AddRange(value.Select(x => (SimpleObjectModification)x.ObjectModification));
                        break;
                    case UnitObjectData unit:
                        unit.NewUnits.Clear();
                        unit.NewUnits.AddRange(value.Select(x => (SimpleObjectModification)x.ObjectModification));
                        break;
                    case UpgradeObjectData upgrade:
                        upgrade.NewUpgrades.Clear();
                        upgrade.NewUpgrades.AddRange(value.Select(x => (LevelObjectModification)x.ObjectModification));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

        }
    }

    public enum SLKType
    {
        Ability,
        Buff,
        Destructable,
        Doodad,
        Item,
        Unit,
        Upgrade
    }

    public class SLKObject
    {
        public SLKType SLKType { get; protected set; }
        public Dictionary<string, object> Data { get; protected set; } = new Dictionary<string, object>();

        public SLKObject(SLKType slkType)
        {
            SLKType = slkType;
        }
    }

    public static class War3Net_SLK_Extensions
    {
        public static string GetMPQFileExtension(this SLKType slkType)
        {
            switch (slkType)
            {
                case SLKType.Ability:
                    return AbilityObjectData.FileExtension;
                case SLKType.Buff:
                    return BuffObjectData.FileExtension;
                case SLKType.Destructable:
                    return DestructableObjectData.FileExtension;
                case SLKType.Doodad:
                    return DoodadObjectData.FileExtension;
                case SLKType.Item:
                    return ItemObjectData.FileExtension;
                case SLKType.Unit:
                    return UnitObjectData.FileExtension;
                case SLKType.Upgrade:
                    return UpgradeObjectData.FileExtension;
                default:
                    throw new NotImplementedException();
            }
        }

        public static SLKType? GetObjectDataTypeForID(this Map map, string fourCCRawCode)
        {
            var ids = new List<string>();
            if (fourCCRawCode.Length == 4)
            {
                ids.Add(fourCCRawCode);
            }
            if (fourCCRawCode.Length == 9 && fourCCRawCode[4] == ':')
            {
                ids.Add(fourCCRawCode.Substring(0, 4));
                ids.Add(fourCCRawCode.Substring(5, 4));
            }

            foreach (var id in ids)
            {
                foreach (var slkType in Enum.GetValues(typeof(SLKType)).Cast<SLKType>())
                {
                    var objectData = map.GetObjectDataBySLKType(slkType);
                    var value = objectData.BaseValues.FirstOrDefault(x => x.ToString() == id) ?? objectData.NewValues.FirstOrDefault(x => x.OldId.ToRawcode() == id || x.NewId.ToRawcode() == id);
                    if (value != null)
                    {
                        return slkType;
                    }
                }
            }

            return null;
        }

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
    }

    public class SLKParser
    {
        public static SLKType GetTypeByWar3MapFileExtension(string war3mapFileExtension)
        {
            foreach (SLKType slkType in Enum.GetValues(typeof(SLKType)))
            {
                if (war3mapFileExtension.Equals(slkType.GetMPQFileExtension(), StringComparison.InvariantCultureIgnoreCase))
                {
                    return slkType;
                }
            }

            throw new NotImplementedException();
        }

        public static SLKType GetTypeBySLKFileName(string slkFileName)
        {
            slkFileName = Path.GetFileName(slkFileName);
            if (slkFileName.Contains("ability", StringComparison.InvariantCultureIgnoreCase))
            {
                if (slkFileName.Contains("buff", StringComparison.InvariantCultureIgnoreCase))
                {
                    return SLKType.Buff;
                }

                return SLKType.Ability;
            }
            if (slkFileName.Contains("doodad", StringComparison.InvariantCultureIgnoreCase))
            {
                return SLKType.Doodad;
            }
            if (slkFileName.Contains("destructable", StringComparison.InvariantCultureIgnoreCase))
            {
                return SLKType.Destructable;
            }
            if (slkFileName.Contains("item", StringComparison.InvariantCultureIgnoreCase))
            {
                return SLKType.Item;
            }
            if (slkFileName.Contains("unit", StringComparison.InvariantCultureIgnoreCase))
            {
                return SLKType.Unit;
            }
            if (slkFileName.Contains("upgrade", StringComparison.InvariantCultureIgnoreCase))
            {
                return SLKType.Upgrade;
            }

            throw new NotImplementedException();
        }

        public Dictionary<string, SLKObject> ParseSLKObjectsFromFiles(List<string> fileNames)
        {
            var result = new Dictionary<string, SLKObject>();
            foreach (var fileName in fileNames)
            {
                try
                {
                    var slkTable = new SylkParser().Parse(File.OpenRead(fileName));
                    var columnNames = new string[slkTable.Width];
                    for (var columnIdx = 0; columnIdx < slkTable.Width; columnIdx++)
                    {
                        columnNames[columnIdx] = slkTable[columnIdx, 0]?.ToString() ?? $"{Path.GetFileName(fileName)}_{columnIdx}";
                    }

                    var objectIdColumnName = columnNames[0];
                    for (var row = 1; row < slkTable.Rows; row++)
                    {
                        try
                        {
                            var objectId = slkTable[0, row]?.ToString();
                            if (!result.TryGetValue(objectId, out var slkObject))
                            {
                                slkObject = new SLKObject(GetTypeBySLKFileName(fileName));
                                result[objectId] = slkObject;
                            }

                            for (var columnIdx = 0; columnIdx < slkTable.Width; columnIdx++)
                            {
                                try
                                {
                                    var value = slkTable[columnIdx, row];
                                    if (value != null)
                                    {
                                        slkObject.Data[columnNames[columnIdx]] = value;
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return result;
        }

        protected Dictionary<SLKType, Dictionary<string, string>> PropertyIdRawCodePerSLKColumn = new Dictionary<SLKType, Dictionary<string, string>>()
        {
            {
                SLKType.Ability, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Animnames", "aani" },
                    { "Area1", "aare" },
                    { "Areaeffectart", "aaea" },
                    { "Art", "aart" },
                    { "BuffID1", "abuf" },
                    { "ButtonposX", "abpx" },
                    { "ButtonposY", "abpy" },
                    { "Cast1", "acas" },
                    { "CasterArt", "acat" },
                    { "Casterattach", "acap" },
                    { "Casterattach1", "aca1" },
                    { "Casterattachcount", "acac" },
                    { "Cool1", "acdn" },
                    { "Cost1", "amcs" },
                    { "DataA1", "mls1" },
                    { "Dur1", "adur" },
                    { "EditorSuffix", "ansf" },
                    { "EfctID1", "aeff" },
                    { "EffectArt", "aeat" },
                    { "Effectsound", "aefs" },
                    { "Effectsoundlooped", "aefl" },
                    { "HeroDur1", "ahdu" },
                    { "Hotkey", "ahky" },
                    { "LightningEffect", "alig" },
                    { "MissileHoming", "amho" },
                    { "Missilearc", "amac" },
                    { "Missileart", "amat" },
                    { "Missilespeed", "amsp" },
                    { "Name", "anam" },
                    { "Order", "aord" },
                    { "Orderoff", "aorf" },
                    { "Orderon", "aoro" },
                    { "Requires", "areq" },
                    { "Requiresamount", "arqa" },
                    { "Rng1", "aran" },
                    { "SpecialArt", "asat" },
                    { "Specialattach", "aspt" },
                    { "TargetArt", "atat" },
                    { "Targetattach", "ata0" },
                    { "Targetattach1", "ata1" },
                    { "Targetattach2", "ata2" },
                    { "Targetattach3", "ata3" },
                    { "Targetattach4", "ata4" },
                    { "Targetattach5", "ata5" },
                    { "Targetattachcount", "atac" },
                    { "Tip", "atp1" },
                    { "Ubertip", "aub1" },
                    { "UnButtonposX", "aubx" },
                    { "UnButtonposY", "auby" },
                    { "Unart", "auar" },
                    { "Unhotkey", "auhk" },
                    { "UnitSkinID", "ausk" },
                    { "Unorder", "aoru" },
                    { "Untip", "aut1" },
                    { "Unubertip", "auu1" },
                    { "checkDep", "achd" },
                    { "hero", "aher" },
                    { "item", "aite" },
                    { "levels", "alev" },
                    { "priority", "apri" },
                    { "race", "arac" },
                    { "targs1", "atar" }
                }
            },
            {
                SLKType.Buff, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Buffart", "fart" },
                    { "Bufftip", "ftip" },
                    { "Buffubertip", "fube" },
                    { "EditorName", "fnam" },
                    { "EditorSuffix", "fnsf" },
                    { "EffectArt", "feat" },
                    { "Effectattach", "feft" },
                    { "Effectsound", "fefs" },
                    { "Effectsoundlooped", "fefl" },
                    { "LightningEffect", "flig" },
                    { "MissileHoming", "fmho" },
                    { "Missilearc", "fmac" },
                    { "Missileart", "fmat" },
                    { "Missilespeed", "fmsp" },
                    { "SpecialArt", "fsat" },
                    { "Specialattach", "fspt" },
                    { "Spelldetail", "fspd" },
                    { "TargetArt", "ftat" },
                    { "Targetattach", "fta0" },
                    { "Targetattach1", "fta1" },
                    { "Targetattach2", "fta2" },
                    { "Targetattach3", "fta3" },
                    { "Targetattach4", "fta4" },
                    { "Targetattach5", "fta5" },
                    { "Targetattachcount", "ftac" },
                    { "isEffect", "feff" },
                    { "race", "frac" }
                }
            },
            {
                SLKType.Destructable, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "EditorSuffix", "bsuf" },
                    { "HP", "bhps" },
                    { "MMBlue", "bmmb" },
                    { "MMGreen", "bmmg" },
                    { "MMRed", "bmmr" },
                    { "Name", "bnam" },
                    { "UserList", "busr" },
                    { "armor", "barm" },
                    { "buildTime", "bbut" },
                    { "canPlaceDead", "bcpd" },
                    { "canPlaceRandScale", "bcpr" },
                    { "category", "bcat" },
                    { "cliffHeight", "bclh" },
                    { "colorB", "bvcb" },
                    { "colorG", "bvcg" },
                    { "colorR", "bvcr" },
                    { "deathSnd", "bdsn" },
                    { "fatLOS", "bflo" },
                    { "file", "bfil" },
                    { "fixedRot", "bfxr" },
                    { "flyH", "bflh" },
                    { "fogRadius", "bfra" },
                    { "fogVis", "bfvi" },
                    { "goldRep", "breg" },
                    { "lightweight", "blit" },
                    { "loopSound", "bsnd" },
                    { "lumberRep", "brel" },
                    { "maxPitch", "bmap" },
                    { "maxRoll", "bmar" },
                    { "maxScale", "bmas" },
                    { "minScale", "bmis" },
                    { "numVar", "bvar" },
                    { "occH", "boch" },
                    { "onCliffs", "bonc" },
                    { "onWater", "bonw" },
                    { "pathTex", "bptx" },
                    { "pathTexDeath", "bptd" },
                    { "portraitmodel", "bgpm" },
                    { "radius", "brad" },
                    { "repairTime", "bret" },
                    { "selSize", "bsel" },
                    { "selcircsize", "bgsc" },
                    { "selectable", "bgse" },
                    { "shadow", "bshd" },
                    { "showInMM", "bsmm" },
                    { "targType", "btar" },
                    { "texFile", "btxf" },
                    { "texID", "btxi" },
                    { "tilesetSpecific", "btsp" },
                    { "tilesets", "btil" },
                    { "useClickHelper", "buch" },
                    { "useMMColor", "bumm" },
                    { "walkable", "bwal" }
                }
            },
            {
                SLKType.Doodad, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "MMBlue", "dmmb" },
                    { "MMGreen", "dmmg" },
                    { "MMRed", "dmmr" },
                    { "Name", "dnam" },
                    { "UserList", "dusr" },
                    { "animInFog", "danf" },
                    { "canPlaceRandScale", "dcpr" },
                    { "category", "dcat" },
                    { "defScale", "ddes" },
                    { "file", "dfil" },
                    { "fixedRot", "dfxr" },
                    { "floats", "dflt" },
                    { "ignoreModelClick", "dimc" },
                    { "maxPitch", "dmap" },
                    { "maxRoll", "dmar" },
                    { "maxScale", "dmas" },
                    { "minScale", "dmis" },
                    { "numVar", "dvar" },
                    { "onCliffs", "donc" },
                    { "onWater", "donw" },
                    { "pathTex", "dptx" },
                    { "selSize", "dsel" },
                    { "shadow", "dshd" },
                    { "showInFog", "dshf" },
                    { "showInMM", "dsmm" },
                    { "soundLoop", "dsnd" },
                    { "tilesetSpecific", "dtsp" },
                    { "tilesets", "dtil" },
                    { "useClickHelper", "duch" },
                    { "useMMColor", "dumc" },
                    { "vertB", "dvb1" },
                    { "vertG", "dvg1" },
                    { "vertR", "dvr1" },
                    { "visRadius", "dvis" },
                    { "walkable", "dwlk" }
                }
            },
            {
                SLKType.Item, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Art", "iico" },
                    { "ButtonposX", "ubpx" },
                    { "ButtonposY", "ubpy" },
                    { "Description", "ides" },
                    { "HP", "ihtp" },
                    { "Hotkey", "uhot" },
                    { "Level", "ilev" },
                    { "Name", "unam" },
                    { "Requires", "ureq" },
                    { "Requiresamount", "urqa" },
                    { "Tip", "utip" },
                    { "Ubertip", "utub" },
                    { "abilList", "iabi" },
                    { "armor", "iarm" },
                    { "class", "icla" },
                    { "colorB", "iclb" },
                    { "colorG", "iclg" },
                    { "colorR", "iclr" },
                    { "cooldownID", "icid" },
                    { "drop", "idrp" },
                    { "droppable", "idro" },
                    { "file", "ifil" },
                    { "goldcost", "igol" },
                    { "ignoreCD", "iicd" },
                    { "lumbercost", "ilum" },
                    { "morph", "imor" },
                    { "oldLevel", "ilvo" },
                    { "pawnable", "ipaw" },
                    { "perishable", "iper" },
                    { "pickRandom", "iprn" },
                    { "powerup", "ipow" },
                    { "prio", "ipri" },
                    { "scale", "isca" },
                    { "selSize", "issc" },
                    { "sellable", "isel" },
                    { "stackMax", "ista" },
                    { "stockInitial", "isit" },
                    { "stockMax", "isto" },
                    { "stockRegen", "istr" },
                    { "stockStart", "isst" },
                    { "usable", "iusa" },
                    { "uses", "iuse" }
                }
            },
            {
                SLKType.Unit, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Art", "uico" },
                    { "Attachmentanimprops", "uaap" },
                    { "Attachmentlinkprops", "ualp" },
                    { "Boneprops", "ubpr" },
                    { "Builds", "ubui" },
                    { "ButtonposX", "ubpx" },
                    { "ButtonposY", "ubpy" },
                    { "Casterupgradeart", "ucua" },
                    { "Casterupgradename", "ucun" },
                    { "Casterupgradetip", "ucut" },
                    { "DependencyOr", "udep" },
                    { "Description", "ides" },
                    { "EditorSuffix", "unsf" },
                    { "Farea1", "ua1f" },
                    { "Farea2", "ua2f" },
                    { "HP", "uhpm" },
                    { "Harea1", "ua1h" },
                    { "Harea2", "ua2h" },
                    { "Hfact1", "uhd1" },
                    { "Hfact2", "uhd2" },
                    { "Hotkey", "uhot" },
                    { "LoopingSoundFadeIn", "ulfi" },
                    { "LoopingSoundFadeOut", "ulfo" },
                    { "MissileHoming1", "umh1" },
                    { "MissileHoming2", "umh2" },
                    { "Missilearc1", "uma1" },
                    { "Missilearc2", "uma2" },
                    { "Missileart1", "ua1m" },
                    { "Missileart2", "ua2m" },
                    { "Missilespeed1", "ua1z" },
                    { "Missilespeed2", "ua2z" },
                    { "MovementSoundLabel", "umsl" },
                    { "Name", "unam" },
                    { "Qarea1", "ua1q" },
                    { "Qarea2", "ua2q" },
                    { "Qfact1", "uqd1" },
                    { "Qfact2", "uqd2" },
                    { "RandomSoundLabel", "ursl" },
                    { "Requires", "ureq" },
                    { "Requiresamount", "urqa" },
                    { "RngBuff1", "urb1" },
                    { "RngBuff2", "urb2" },
                    { "ScoreScreenIcon", "ussi" },
                    { "Sellitems", "usei" },
                    { "Sellunits", "useu" },
                    { "Specialart", "uspa" },
                    { "Targetart", "utaa" },
                    { "Tip", "utip" },
                    { "Ubertip", "utub" },
                    { "abilList", "uabi" },
                    { "abilSkinList", "uabs" },
                    { "acquire", "uacq" },
                    { "animProps", "uani" },
                    { "armor", "uarm" },
                    { "atkType1", "ua1t" },
                    { "atkType2", "ua2t" },
                    { "auto", "udaa" },
                    { "backSw1", "ubs1" },
                    { "backSw2", "ubs2" },
                    { "bldtm", "ubld" },
                    { "blend", "uble" },
                    { "blue", "uclb" },
                    { "bountydice", "ubdi" },
                    { "bountyplus", "ubba" },
                    { "bountysides", "ubsi" },
                    { "buffRadius", "uabr" },
                    { "buffType", "uabt" },
                    { "buildingShadow", "ushb" },
                    { "campaign", "ucam" },
                    { "canFlee", "ufle" },
                    { "canSleep", "usle" },
                    { "cargoSize", "ucar" },
                    { "castbsw", "ucbs" },
                    { "castpt", "ucpt" },
                    { "collision", "ucol" },
                    { "cool1", "ua1c" },
                    { "cool2", "ua2c" },
                    { "customTeamColor", "utcc" },
                    { "damageLoss1", "udl1" },
                    { "damageLoss2", "udl2" },
                    { "death", "udtm" },
                    { "deathType", "udea" },
                    { "def", "udef" },
                    { "defType", "udty" },
                    { "defUp", "udup" },
                    { "dice1", "ua1d" },
                    { "dice2", "ua2d" },
                    { "dmgUp1", "udu1" },
                    { "dmgUp2", "udu2" },
                    { "dmgplus1", "ua1b" },
                    { "dmgplus2", "ua2b" },
                    { "dmgpt1", "udp1" },
                    { "dmgpt2", "udp2" },
                    { "dropItems", "udro" },
                    { "elevPts", "uept" },
                    { "elevRad", "uerd" },
                    { "fatLOS", "ulos" },
                    { "file", "umdl" },
                    { "fileVerFlags", "uver" },
                    { "fmade", "ufma" },
                    { "fogRad", "ufrd" },
                    { "formation", "ufor" },
                    { "fused", "ufoo" },
                    { "goldRep", "ugor" },
                    { "goldcost", "ugol" },
                    { "green", "uclg" },
                    { "heroAbilSkinList", "uhas" },
                    { "hideOnMinimap", "uhom" },
                    { "hostilePal", "uhos" },
                    { "impactSwimZ", "uisz" },
                    { "impactZ", "uimz" },
                    { "inEditor", "uine" },
                    { "isbldg", "ubdg" },
                    { "launchSwimZ", "ulsz" },
                    { "launchX", "ulpx" },
                    { "launchY", "ulpy" },
                    { "launchZ", "ulpz" },
                    { "level", "ulev" },
                    { "lumberRep", "ulur" },
                    { "lumberbountydice", "ulbd" },
                    { "lumberbountyplus", "ulba" },
                    { "lumberbountysides", "ulbs" },
                    { "lumbercost", "ulum" },
                    { "mana0", "umpi" },
                    { "manaN", "umpm" },
                    { "maxPitch", "umxp" },
                    { "maxRoll", "umxr" },
                    { "maxSpd", "umas" },
                    { "minRange", "uamn" },
                    { "minSpd", "umis" },
                    { "modelScale", "usca" },
                    { "moveFloor", "umvf" },
                    { "moveHeight", "umvh" },
                    { "movetp", "umvt" },
                    { "nsight", "usin" },
                    { "occH", "uocc" },
                    { "orientInterp", "uori" },
                    { "points", "upoi" },
                    { "prio", "upri" },
                    { "propWin", "uprw" },
                    { "race", "urac" },
                    { "rangeN1", "ua1r" },
                    { "rangeN2", "ua2r" },
                    { "red", "uclr" },
                    { "regenHP", "uhpr" },
                    { "regenMana", "umpr" },
                    { "regenType", "uhrt" },
                    { "reptm", "urtm" },
                    { "repulse", "urpo" },
                    { "repulseGroup", "urpg" },
                    { "repulseParam", "urpp" },
                    { "repulsePrio", "urpr" },
                    { "run", "urun" },
                    { "scale", "ussc" },
                    { "scaleBull", "uscb" },
                    { "selCircOnWater", "usew" },
                    { "selZ", "uslz" },
                    { "shadowH", "ushh" },
                    { "shadowOnWater", "ushr" },
                    { "shadowW", "ushw" },
                    { "shadowX", "ushx" },
                    { "shadowY", "ushy" },
                    { "showUI1", "uwu1" },
                    { "showUI4", "uwu2" },
                    { "sides1", "ua1s" },
                    { "sides2", "ua2s" },
                    { "sight", "usid" },
                    { "spd", "umvs" },
                    { "special", "uspe" },
                    { "spillDist1", "usd1" },
                    { "spillDist2", "usd2" },
                    { "spillRadius1", "usr1" },
                    { "spillRadius2", "usr2" },
                    { "splashTargs1", "ua1p" },
                    { "splashTargs2", "ua2p" },
                    { "stockInitial", "usit" },
                    { "stockMax", "usma" },
                    { "stockRegen", "usrg" },
                    { "stockStart", "usst" },
                    { "targCount1", "utc1" },
                    { "targCount2", "utc2" },
                    { "targType", "utar" },
                    { "targs1", "ua1g" },
                    { "targs2", "ua2g" },
                    { "teamColor", "utco" },
                    { "tilesetSpecific", "utss" },
                    { "tilesets", "util" },
                    { "turnRate", "umvr" },
                    { "type", "utyp" },
                    { "unitShadow", "ushu" },
                    { "unitSound", "usnd" },
                    { "upgrades", "upgr" },
                    { "useClickHelper", "uuch" },
                    { "walk", "uwal" },
                    { "weapTp1", "ua1w" },
                    { "weapTp2", "ua2w" },
                    { "weapType1", "ucs1" },
                    { "weapType2", "ucs2" },
                    { "weapsOn", "uaen" }
                }
            },
            {
                SLKType.Upgrade, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Art", "gar1" },
                    { "ButtonposX", "gbpx" },
                    { "ButtonposY", "gbpy" },
                    { "EditorSuffix", "gnsf" },
                    { "Hotkey", "ghk1" },
                    { "Name", "gnam" },
                    { "Requires", "greq" },
                    { "Requiresamount", "grqc" },
                    { "Tip", "gtp1" },
                    { "Ubertip", "gub1" },
                    { "base1", "gba1" },
                    { "class", "gcls" },
                    { "effect1", "gef1" },
                    { "effect2", "gef2" },
                    { "effect3", "gef3" },
                    { "effect4", "gef4" },
                    { "global", "glob" },
                    { "goldbase", "gglb" },
                    { "goldmod", "gglm" },
                    { "inherit", "ginh" },
                    { "lumberbase", "glmb" },
                    { "lumbermod", "glmm" },
                    { "maxlevel", "glvl" },
                    { "mod1", "gmo1" },
                    { "race", "grac" },
                    { "timebase", "gtib" },
                    { "timemod", "gtim" }
                }
            }
        };

        public string ConvertToPropertyIdRawCode(SLKType slkType, string columnName)
        {
            if (PropertyIdRawCodePerSLKColumn.TryGetValue(slkType, out var rawCodePerSLKColumn) && rawCodePerSLKColumn.TryGetValue((columnName ?? "").Trim(), out var result))
            {
                return result;
            }

            return null;
        }
    }
}