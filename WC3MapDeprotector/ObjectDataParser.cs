using IniParser.Model.Configuration;
using IniParser;
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
        public ObjectDataModification ObjectDataModification { get; protected set; }
        public War3NetObjectDataModificationWrapper(ObjectDataModification objectDataModification)
        {
            ObjectDataModification = objectDataModification;
        }

        public override int GetHashCode()
        {
            return ObjectDataModification?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            return ObjectDataModification?.Equals((obj as War3NetObjectDataModificationWrapper)?.ObjectDataModification) ?? false;
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
                return ObjectDataModification.Id;
            }
            set
            {
                ObjectDataModification.Id = value;
            }
        }

        public War3Net.Build.Object.ObjectDataType Type
        {
            get
            {
                return ObjectDataModification.Type;
            }
            protected set
            {
                ObjectDataModification.Type = value;
            }
        }

        public object Value
        {
            get
            {
                return ObjectDataModification.Value;
            }
            set
            {
                SetValue(value);
            }
        }

        protected void SetValue(object value)
        {
            var valueAsString = value.ToString();
            Type = War3Net.Build.Object.ObjectDataType.String;
            ObjectDataModification.Value = valueAsString;
            if (int.TryParse(valueAsString, out var intValue))
            {
                Type = War3Net.Build.Object.ObjectDataType.Int;
                ObjectDataModification.Value = intValue;
            }
            else if (decimal.TryParse(valueAsString, out var decimalValue))
            {
                Type = War3Net.Build.Object.ObjectDataType.Real;
                ObjectDataModification.Value = (float)decimalValue;
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

        public War3NetObjectDataModificationWrapper GetEmptyObjectDataModificationWrapper()
        {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        return new War3NetObjectDataModificationWrapper(new LevelObjectDataModification());
                case SimpleObjectModification simple:
                    return new War3NetObjectDataModificationWrapper(new SimpleObjectDataModification());
                case VariationObjectModification variation:
                    return new War3NetObjectDataModificationWrapper(new VariationObjectDataModification());
            }

            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            return ObjectModification?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            return ObjectModification?.Equals((obj as War3NetObjectModificationWrapper)?.ObjectModification) ?? false;
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

        public override int GetHashCode()
        {
            return ObjectData?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            return ObjectData?.Equals((obj as War3NetObjectDataWrapper)?.ObjectData) ?? false;
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

        public ObjectDataType ObjectDataType
        {
            get
            {
                switch (ObjectData)
                {
                    case AbilityObjectData:
                        return ObjectDataType.Ability;
                    case BuffObjectData:
                        return ObjectDataType.Buff;
                    case DestructableObjectData:
                        return ObjectDataType.Destructable;
                    case DoodadObjectData:
                        return ObjectDataType.Doodad;
                    case ItemObjectData:
                        return ObjectDataType.Item;
                    case UnitObjectData:
                        return ObjectDataType.Unit;
                    case UpgradeObjectData:
                        return ObjectDataType.Upgrade;
                }

                throw new NotImplementedException();
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

    public enum ObjectDataType
    {
        Ability,
        Buff,
        Destructable,
        Doodad,
        Item,
        Unit,
        Upgrade
    }

    public class ObjectData
    {
        public ObjectDataType ObjectDataType { get; protected set; }
        public Dictionary<string, object> Data { get; protected set; } = new Dictionary<string, object>();

        public ObjectData(ObjectDataType objectDataType)
        {
            ObjectDataType = objectDataType;
        }
    }

    public static class War3NetObjectDataExtensions
    {
        public static string GetMPQFileExtension(this ObjectDataType objectDataType)
        {
            switch (objectDataType)
            {
                case ObjectDataType.Ability:
                    return AbilityObjectData.FileExtension;
                case ObjectDataType.Buff:
                    return BuffObjectData.FileExtension;
                case ObjectDataType.Destructable:
                    return DestructableObjectData.FileExtension;
                case ObjectDataType.Doodad:
                    return DoodadObjectData.FileExtension;
                case ObjectDataType.Item:
                    return ItemObjectData.FileExtension;
                case ObjectDataType.Unit:
                    return UnitObjectData.FileExtension;
                case ObjectDataType.Upgrade:
                    return UpgradeObjectData.FileExtension;
                default:
                    throw new NotImplementedException();
            }
        }

        public static ObjectDataType? GetObjectDataTypeForID(this Map map, string fourCCRawCode)
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
                foreach (var objectDataType in Enum.GetValues(typeof(ObjectDataType)).Cast<ObjectDataType>())
                {
                    var objectData = map.GetObjectDataByType(objectDataType);
                    var value = objectData.BaseValues.FirstOrDefault(x => x.ToString() == id) ?? objectData.NewValues.FirstOrDefault(x => x.OldId.ToRawcode() == id || x.NewId.ToRawcode() == id);
                    if (value != null)
                    {
                        return objectDataType;
                    }
                }
            }

            return null;
        }

        public static War3NetObjectDataWrapper GetObjectDataByType(this Map map, ObjectDataType objectDataType)
        {
            //NOTE: War3Net uses a separate class for each type of ObjectData even though they're very similar, so we need to return object
            switch (objectDataType)
                {
                    case ObjectDataType.Ability:
                    map.AbilityObjectData ??= new AbilityObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.AbilityObjectData);
                case ObjectDataType.Buff:
                    map.BuffObjectData ??= new BuffObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.BuffObjectData);
                case ObjectDataType.Destructable:
                    map.DestructableObjectData ??= new DestructableObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.DestructableObjectData);
                case ObjectDataType.Doodad:
                    map.DoodadObjectData ??= new DoodadObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.DoodadObjectData);
                case ObjectDataType.Item:
                    map.ItemObjectData ??= new ItemObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.ItemObjectData);
                case ObjectDataType.Unit:
                    map.UnitObjectData ??= new UnitObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.UnitObjectData);
                case ObjectDataType.Upgrade:
                    map.UpgradeObjectData ??= new UpgradeObjectData(ObjectDataFormatVersion.v3);
                    return new War3NetObjectDataWrapper(map.UpgradeObjectData);
                default:
                    throw new NotImplementedException();
            }
        }
    }

    public class ObjectDataParser
    {
        public static ObjectDataType GetTypeByWar3MapFileExtension(string war3mapFileExtension)
        {
            foreach (ObjectDataType type in Enum.GetValues(typeof(ObjectDataType)))
            {
                if (war3mapFileExtension.Equals(type.GetMPQFileExtension(), StringComparison.InvariantCultureIgnoreCase))
                {
                    return type;
                }
            }

            throw new NotImplementedException();
        }

        public static ObjectDataType GetObjectDataTypeByFileName(string fileName)
        {
            fileName = Path.GetFileName(fileName);
            if (fileName.Contains("ability", StringComparison.InvariantCultureIgnoreCase))
            {
                if (fileName.Contains("buff", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ObjectDataType.Buff;
                }

                return ObjectDataType.Ability;
            }
            if (fileName.Contains("doodad", StringComparison.InvariantCultureIgnoreCase))
            {
                return ObjectDataType.Doodad;
            }
            if (fileName.Contains("destructable", StringComparison.InvariantCultureIgnoreCase))
            {
                return ObjectDataType.Destructable;
            }
            if (fileName.Contains("item", StringComparison.InvariantCultureIgnoreCase))
            {
                return ObjectDataType.Item;
            }
            if (fileName.Contains("unit", StringComparison.InvariantCultureIgnoreCase))
            {
                return ObjectDataType.Unit;
            }
            if (fileName.Contains("upgrade", StringComparison.InvariantCultureIgnoreCase))
            {
                return ObjectDataType.Upgrade;
            }

            throw new NotImplementedException();
        }

        public Dictionary<string, ObjectData> ParseObjectDataFromTxtFiles(List<string> fileNames)
        {
            var result = new Dictionary<string, ObjectData>();
            foreach (var fileName in fileNames.Where(x => Path.GetExtension(x).Equals(".txt", StringComparison.InvariantCultureIgnoreCase)))
            {
                try
                {
                    var type = GetObjectDataTypeByFileName(fileName);

                    var parser = new FileIniDataParser(new IniParser.Parser.IniDataParser(new IniParserConfiguration() { SkipInvalidLines = true, AllowDuplicateKeys = true, AllowDuplicateSections = true, AllowKeysWithoutSection = true }));
                    var ini = parser.ReadFile(fileName, Encoding.UTF8);
                    ini.Configuration.AssigmentSpacer = "";
                    foreach (var objectEntry in ini.Sections)
                    {
                        var objectData = new ObjectData(type);
                        foreach (var propertyId in objectEntry.Keys)
                        {

                            objectData.Data[propertyId.KeyName] = propertyId.Value;
                        }
                        result[objectEntry.SectionName] = objectData;
                    }
                }
                catch { }
            }

            return result;
        }
        public Dictionary<string, ObjectData> ParseObjectDataFromSLKFiles(List<string> fileNames)
        {
            var result = new Dictionary<string, ObjectData>();
            foreach (var fileName in fileNames.Where(x => Path.GetExtension(x).Equals(".slk", StringComparison.InvariantCultureIgnoreCase)))
            {
                try
                {
                    var type = GetObjectDataTypeByFileName(fileName);
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
                            if (!result.TryGetValue(objectId, out var objectData))
                            {
                                objectData = new ObjectData(type);
                                result[objectId] = objectData;
                            }

                            for (var columnIdx = 0; columnIdx < slkTable.Width; columnIdx++)
                            {
                                try
                                {
                                    var value = slkTable[columnIdx, row];
                                    if (value != null)
                                    {
                                        objectData.Data[columnNames[columnIdx]] = value;
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

        protected Dictionary<ObjectDataType, Dictionary<string, List<string>>> PropertyIdRawCodePerObjectDataTypeColumn;

        public ObjectDataParser()
        {
            // parsed from https://github.com/fbicirno/Nirvana/blob/master/wurst/lua/metadata/meta.lua
            // todo, check if this URL has any additional fields: https://github.com/sumneko/YDWE/blob/master/Development/Component/plugin/w3x2lni/script/core/defined/metadata.ini

            var data = new Dictionary<ObjectDataType, string[]>()
            {
                {
                    ObjectDataType.Ability, new string[]
                    {
                        "Animnames,aani",
                        "Area,aare",
                        "Area1,aare",
                        "Areaeffectart,aaea",
                        "Art,aart",
                        "BuffID,abuf",
                        "BuffID1,abuf",
                        "Buttonpos_1,abpx",
                        "Buttonpos_2,abpy",
                        "Cast,Npa6",
                        "Cast,Rpb6",
                        "Cast,acas",
                        "Cast1,acas",
                        "CasterArt,acat",
                        "Casterattach,acap",
                        "Casterattach1,aca1",
                        "Casterattachcount,acac",
                        "Cool,acdn",
                        "Cool1,acdn",
                        "Cost,amcs",
                        "Cost1,amcs",
                        "DataA,Adm1",
                        "DataA,Akb1",
                        "DataA,Ams1",
                        "DataA,Apl1",
                        "DataA,Arm1",
                        "DataA,Bgm1",
                        "DataA,Bli1",
                        "DataA,Blo1",
                        "DataA,Cac1",
                        "DataA,Cad1",
                        "DataA,Can1",
                        "DataA,Car1",
                        "DataA,Chd1",
                        "DataA,Cor1",
                        "DataA,Cri1",
                        "DataA,Crs",
                        "DataA,Ctb1",
                        "DataA,Ctc1",
                        "DataA,Dda1",
                        "DataA,Def1",
                        "DataA,Det1",
                        "DataA,Dev1",
                        "DataA,Dtn1",
                        "DataA,Eah1",
                        "DataA,Ear1",
                        "DataA,Eat1",
                        "DataA,Ebl1",
                        "DataA,Eer1",
                        "DataA,Eev1",
                        "DataA,Efk1",
                        "DataA,Efn1",
                        "DataA,Egm1",
                        "DataA,Eim1",
                        "DataA,Emb1",
                        "DataA,Eme1",
                        "DataA,Ens1",
                        "DataA,Esf1",
                        "DataA,Esh1",
                        "DataA,Esn1",
                        "DataA,Esv1",
                        "DataA,Eth1",
                        "DataA,Etq1",
                        "DataA,Fae1",
                        "DataA,Fla1",
                        "DataA,Gho1",
                        "DataA,Gld1",
                        "DataA,Gyd1",
                        "DataA,Hab1",
                        "DataA,Had1",
                        "DataA,Har1",
                        "DataA,Hav1",
                        "DataA,Hbh1",
                        "DataA,Hbn1",
                        "DataA,Hbz1",
                        "DataA,Hca1",
                        "DataA,Hds1",
                        "DataA,Hea1",
                        "DataA,Hfa1",
                        "DataA,Hfs1",
                        "DataA,Hhb1",
                        "DataA,Hmt1",
                        "DataA,Hre1",
                        "DataA,Htb1",
                        "DataA,Htc1",
                        "DataA,Hwe2",
                        "DataA,Iaa1",
                        "DataA,Iagi",
                        "DataA,Iatt",
                        "DataA,Icfd",
                        "DataA,Icre",
                        "DataA,Idam",
                        "DataA,Idef",
                        "DataA,Idet",
                        "DataA,Idic",
                        "DataA,Idim",
                        "DataA,Idps",
                        "DataA,Ifa1",
                        "DataA,Igl1",
                        "DataA,Igol",
                        "DataA,Ihpg",
                        "DataA,Ihpr",
                        "DataA,Ihps",
                        "DataA,Iild",
                        "DataA,Ilev",
                        "DataA,Ilif",
                        "DataA,Ilum",
                        "DataA,Iman",
                        "DataA,Impg",
                        "DataA,Imrp",
                        "DataA,Imvb",
                        "DataA,Inf1",
                        "DataA,Ircd",
                        "DataA,Irec",
                        "DataA,Isib",
                        "DataA,Isn1",
                        "DataA,Ispi",
                        "DataA,Isx1",
                        "DataA,Itpm",
                        "DataA,Ivam",
                        "DataA,Ivs1",
                        "DataA,Ixpg",
                        "DataA,Ixs1",
                        "DataA,Lit1",
                        "DataA,Lsh1",
                        "DataA,Mbt1",
                        "DataA,Mil1",
                        "DataA,Min1",
                        "DataA,Nab1",
                        "DataA,Nba1",
                        "DataA,Nbr1",
                        "DataA,Nch1",
                        "DataA,Ncl1",
                        "DataA,Ncs1",
                        "DataA,Ndc1",
                        "DataA,Nde1",
                        "DataA,Ndo1",
                        "DataA,Ndp1",
                        "DataA,Ndr1",
                        "DataA,Ndt1",
                        "DataA,Nef1",
                        "DataA,Neg1",
                        "DataA,Neu1",
                        "DataA,Nfd1",
                        "DataA,Nfy1",
                        "DataA,Nic1",
                        "DataA,Nmr1",
                        "DataA,Nms1",
                        "DataA,Npr1",
                        "DataA,Nrc1",
                        "DataA,Nsa1",
                        "DataA,Nse1",
                        "DataA,Nsi1",
                        "DataA,Nso1",
                        "DataA,Nsp1",
                        "DataA,Nst1",
                        "DataA,Nsy1",
                        "DataA,Ntm1",
                        "DataA,Nvc1",
                        "DataA,Oae1",
                        "DataA,Oar1",
                        "DataA,Ocl1",
                        "DataA,Ocr1",
                        "DataA,Oeq1",
                        "DataA,Ofs1",
                        "DataA,Omi1",
                        "DataA,Ore1",
                        "DataA,Osh1",
                        "DataA,Owk1",
                        "DataA,Oww1",
                        "DataA,Ply1",
                        "DataA,Poa1",
                        "DataA,Poi1",
                        "DataA,Pos1",
                        "DataA,Prg1",
                        "DataA,Rai1",
                        "DataA,Rej1",
                        "DataA,Rep1",
                        "DataA,Roa1",
                        "DataA,Roo1",
                        "DataA,Rtn1",
                        "DataA,Sal1",
                        "DataA,Shm1",
                        "DataA,Slo1",
                        "DataA,Sod1",
                        "DataA,Spa1",
                        "DataA,Spo1",
                        "DataA,Ssk1",
                        "DataA,Sta1",
                        "DataA,Tau1",
                        "DataA,Tdg1",
                        "DataA,Tsp1",
                        "DataA,Uan1",
                        "DataA,Uau1",
                        "DataA,Uav1",
                        "DataA,Ucs1",
                        "DataA,Udc1",
                        "DataA,Udd1",
                        "DataA,Udp1",
                        "DataA,Uds1",
                        "DataA,Ufa1",
                        "DataA,Ufn1",
                        "DataA,Uhf1",
                        "DataA,Uim1",
                        "DataA,Uin1",
                        "DataA,Uls1",
                        "DataA,Usl1",
                        "DataA,Uts1",
                        "DataA,War1",
                        "DataA,Wha1",
                        "DataA,Wrp1",
                        "DataA,Wrs1",
                        "DataA,abs1",
                        "DataA,ast1",
                        "DataA,bsk1",
                        "DataA,coa1",
                        "DataA,cyc1",
                        "DataA,dcp1",
                        "DataA,dvm1",
                        "DataA,exh1",
                        "DataA,fak1",
                        "DataA,fbk1",
                        "DataA,flk1",
                        "DataA,gra1",
                        "DataA,ict1",
                        "DataA,idc1",
                        "DataA,imo1",
                        "DataA,inv1",
                        "DataA,ipv1",
                        "DataA,irl1",
                        "DataA,isr1",
                        "DataA,liq1",
                        "DataA,mec1",
                        "DataA,mfl1",
                        "DataA,mim1",
                        "DataA,mls1",
                        "DataA,nca1",
                        "DataA,pxf1",
                        "DataA,sla1",
                        "DataA,spb1",
                        "DataA,spl1",
                        "DataA,tpi1",
                        "DataA1,mls1",
                        "DataB,Adm2",
                        "DataB,Ams2",
                        "DataB,Apl2",
                        "DataB,Arm2",
                        "DataB,Bgm2",
                        "DataB,Bli2",
                        "DataB,Blo2",
                        "DataB,Btl2",
                        "DataB,Can2",
                        "DataB,Chd2",
                        "DataB,Cmg2",
                        "DataB,Cri2",
                        "DataB,Ctc2",
                        "DataB,Dda2",
                        "DataB,Def2",
                        "DataB,Dev2",
                        "DataB,Dtn2",
                        "DataB,Eah2",
                        "DataB,Ear2",
                        "DataB,Eat2",
                        "DataB,Ebl2",
                        "DataB,Efk2",
                        "DataB,Egm2",
                        "DataB,Eim2",
                        "DataB,Emb2",
                        "DataB,Eme2",
                        "DataB,Ens2",
                        "DataB,Esf2",
                        "DataB,Esh2",
                        "DataB,Esn2",
                        "DataB,Eth2",
                        "DataB,Etq2",
                        "DataB,Fae2",
                        "DataB,Fla2",
                        "DataB,Gho2",
                        "DataB,Gld2",
                        "DataB,Gyd2",
                        "DataB,Hab2",
                        "DataB,Had2",
                        "DataB,Har2",
                        "DataB,Hav2",
                        "DataB,Hbh2",
                        "DataB,Hbn2",
                        "DataB,Hbz2",
                        "DataB,Hca2",
                        "DataB,Hfs2",
                        "DataB,Hmt2",
                        "DataB,Hre2",
                        "DataB,Htc2",
                        "DataB,Iarp",
                        "DataB,Icfm",
                        "DataB,Idel",
                        "DataB,Idid",
                        "DataB,Ihp2",
                        "DataB,Iilw",
                        "DataB,Iint",
                        "DataB,Imps",
                        "DataB,Inf2",
                        "DataB,Iob2",
                        "DataB,Isn2",
                        "DataB,Itp2",
                        "DataB,Ixs2",
                        "DataB,Lit2",
                        "DataB,Mbt2",
                        "DataB,Mil2",
                        "DataB,Min2",
                        "DataB,Nab2",
                        "DataB,Nba2",
                        "DataB,Ncl2",
                        "DataB,Ncs2",
                        "DataB,Nde2",
                        "DataB,Ndo2",
                        "DataB,Ndp2",
                        "DataB,Ndr2",
                        "DataB,Ndt2",
                        "DataB,Neg2",
                        "DataB,Neu2",
                        "DataB,Nfd2",
                        "DataB,Nfy2",
                        "DataB,Nic2",
                        "DataB,Nlm2",
                        "DataB,Nms2",
                        "DataB,Nrc2",
                        "DataB,Nsa2",
                        "DataB,Nsi2",
                        "DataB,Nso2",
                        "DataB,Nsp2",
                        "DataB,Nst2",
                        "DataB,Nsy2",
                        "DataB,Ntm2",
                        "DataB,Nvc2",
                        "DataB,Oae2",
                        "DataB,Oar2",
                        "DataB,Ocl2",
                        "DataB,Ocr2",
                        "DataB,Oeq2",
                        "DataB,Omi2",
                        "DataB,Osf2",
                        "DataB,Osh2",
                        "DataB,Owk2",
                        "DataB,Oww2",
                        "DataB,Ply2",
                        "DataB,Poa2",
                        "DataB,Poi2",
                        "DataB,Pos2",
                        "DataB,Prg2",
                        "DataB,Rai2",
                        "DataB,Rej2",
                        "DataB,Rep2",
                        "DataB,Roa2",
                        "DataB,Roo2",
                        "DataB,Rtn2",
                        "DataB,Sal2",
                        "DataB,Shm2",
                        "DataB,Slo2",
                        "DataB,Sod2",
                        "DataB,Spo2",
                        "DataB,Ssk2",
                        "DataB,Sta2",
                        "DataB,Tau2",
                        "DataB,Tdg2",
                        "DataB,Tsp2",
                        "DataB,Uau2",
                        "DataB,Ucs2",
                        "DataB,Udd2",
                        "DataB,Udp2",
                        "DataB,Uds2",
                        "DataB,Ufa2",
                        "DataB,Ufn2",
                        "DataB,Uhf2",
                        "DataB,Uim2",
                        "DataB,Uin2",
                        "DataB,Uls2",
                        "DataB,Uts2",
                        "DataB,War2",
                        "DataB,Wha2",
                        "DataB,Wrp2",
                        "DataB,Wrs2",
                        "DataB,abs2",
                        "DataB,ast2",
                        "DataB,bsk2",
                        "DataB,coa2",
                        "DataB,dcp2",
                        "DataB,dvm2",
                        "DataB,fak2",
                        "DataB,fbk2",
                        "DataB,flk2",
                        "DataB,gra2",
                        "DataB,ict2",
                        "DataB,idc2",
                        "DataB,imo2",
                        "DataB,inv2",
                        "DataB,ipv2",
                        "DataB,irc2",
                        "DataB,irl2",
                        "DataB,isr2",
                        "DataB,liq2",
                        "DataB,mfl2",
                        "DataB,pxf2",
                        "DataB,sla2",
                        "DataB,spb2",
                        "DataB,spl2",
                        "DataB,tpi2",
                        "DataC,Ams3",
                        "DataC,Apl3",
                        "DataC,Bgm3",
                        "DataC,Blo3",
                        "DataC,Chd3",
                        "DataC,Cmg3",
                        "DataC,Cri3",
                        "DataC,Ctc3",
                        "DataC,Dda3",
                        "DataC,Def3",
                        "DataC,Dev3",
                        "DataC,Ear3",
                        "DataC,Eat3",
                        "DataC,Efk3",
                        "DataC,Eim3",
                        "DataC,Emb3",
                        "DataC,Eme3",
                        "DataC,Ens3",
                        "DataC,Esf3",
                        "DataC,Esh3",
                        "DataC,Esn3",
                        "DataC,Etq3",
                        "DataC,Fla3",
                        "DataC,Gho3",
                        "DataC,Gld3",
                        "DataC,Gyd3",
                        "DataC,Har3",
                        "DataC,Hav3",
                        "DataC,Hbh3",
                        "DataC,Hbz3",
                        "DataC,Hca3",
                        "DataC,Hfs3",
                        "DataC,Hmt3",
                        "DataC,Htc3",
                        "DataC,Icfx",
                        "DataC,Imp2",
                        "DataC,Inf3",
                        "DataC,Iob3",
                        "DataC,Ist1",
                        "DataC,Istr",
                        "DataC,Mbt3",
                        "DataC,Nab3",
                        "DataC,Nba3",
                        "DataC,Ncl3",
                        "DataC,Ncs3",
                        "DataC,Nde3",
                        "DataC,Ndo3",
                        "DataC,Ndp3",
                        "DataC,Ndr3",
                        "DataC,Ndt3",
                        "DataC,Neg3",
                        "DataC,Neu3",
                        "DataC,Nfd3",
                        "DataC,Nic3",
                        "DataC,Nlm3",
                        "DataC,Nsa3",
                        "DataC,Nsi3",
                        "DataC,Nso3",
                        "DataC,Nsp3",
                        "DataC,Nst3",
                        "DataC,Nsy3",
                        "DataC,Ntm3",
                        "DataC,Nvc3",
                        "DataC,Ocl3",
                        "DataC,Ocr3",
                        "DataC,Oeq3",
                        "DataC,Omi3",
                        "DataC,Osh3",
                        "DataC,Owk3",
                        "DataC,Ply3",
                        "DataC,Poa3",
                        "DataC,Poi3",
                        "DataC,Pos3",
                        "DataC,Prg3",
                        "DataC,Rai3",
                        "DataC,Rej3",
                        "DataC,Rep3",
                        "DataC,Roa3",
                        "DataC,Roo3",
                        "DataC,Rpb3",
                        "DataC,Shm3",
                        "DataC,Slo3",
                        "DataC,Spo3",
                        "DataC,Ssk3",
                        "DataC,Sta3",
                        "DataC,Tau3",
                        "DataC,Tdg3",
                        "DataC,Uan3",
                        "DataC,Uau3",
                        "DataC,Ucs3",
                        "DataC,Udp3",
                        "DataC,Uim3",
                        "DataC,Uin3",
                        "DataC,Uls3",
                        "DataC,Uts3",
                        "DataC,War3",
                        "DataC,Wha3",
                        "DataC,Wrs3",
                        "DataC,bsk3",
                        "DataC,dvm3",
                        "DataC,fak3",
                        "DataC,fbk3",
                        "DataC,flk3",
                        "DataC,gra3",
                        "DataC,idc3",
                        "DataC,imo3",
                        "DataC,inv3",
                        "DataC,ipv3",
                        "DataC,irc3",
                        "DataC,irl3",
                        "DataC,liq3",
                        "DataC,mfl3",
                        "DataC,spb3",
                        "DataD,Ams4",
                        "DataD,Bgm4",
                        "DataD,Ctc4",
                        "DataD,Dda4",
                        "DataD,Def4",
                        "DataD,Ear4",
                        "DataD,Efk4",
                        "DataD,Eme4",
                        "DataD,Esh4",
                        "DataD,Esn4",
                        "DataD,Hav4",
                        "DataD,Hbh4",
                        "DataD,Hbz4",
                        "DataD,Hca4",
                        "DataD,Hfs4",
                        "DataD,Htc4",
                        "DataD,Ihid",
                        "DataD,Inf4",
                        "DataD,Iob4",
                        "DataD,Ist2",
                        "DataD,Mbt4",
                        "DataD,Nab4",
                        "DataD,Ncl4",
                        "DataD,Ncs4",
                        "DataD,Nde4",
                        "DataD,Ndo4",
                        "DataD,Ndr4",
                        "DataD,Neg4",
                        "DataD,Neu4",
                        "DataD,Nic4",
                        "DataD,Nlm4",
                        "DataD,Nsa4",
                        "DataD,Nsi4",
                        "DataD,Nso4",
                        "DataD,Nst4",
                        "DataD,Nsy4",
                        "DataD,Ntm4",
                        "DataD,Nvc4",
                        "DataD,Ocr4",
                        "DataD,Oeq4",
                        "DataD,Omi4",
                        "DataD,Osh4",
                        "DataD,Owk4",
                        "DataD,Ply4",
                        "DataD,Poa4",
                        "DataD,Poi4",
                        "DataD,Pos4",
                        "DataD,Prg4",
                        "DataD,Rai4",
                        "DataD,Rej4",
                        "DataD,Rep4",
                        "DataD,Roa4",
                        "DataD,Roo4",
                        "DataD,Rpb4",
                        "DataD,Spo4",
                        "DataD,Ssk4",
                        "DataD,Sta4",
                        "DataD,Tdg4",
                        "DataD,Ucs4",
                        "DataD,Udp4",
                        "DataD,Uim4",
                        "DataD,Uls4",
                        "DataD,War4",
                        "DataD,dvm4",
                        "DataD,fak4",
                        "DataD,fbk4",
                        "DataD,flk4",
                        "DataD,gra4",
                        "DataD,inv4",
                        "DataD,irl4",
                        "DataD,liq4",
                        "DataD,mfl4",
                        "DataD,spb4",
                        "DataE,Ans5",
                        "DataE,Def5",
                        "DataE,Eme5",
                        "DataE,Esh5",
                        "DataE,Hbh5",
                        "DataE,Hbz5",
                        "DataE,Hfs5",
                        "DataE,Iob5",
                        "DataE,Mbt5",
                        "DataE,Nab5",
                        "DataE,Nbf5",
                        "DataE,Ncl5",
                        "DataE,Ncr5",
                        "DataE,Ncs5",
                        "DataE,Ndr5",
                        "DataE,Neg5",
                        "DataE,Nic5",
                        "DataE,Nlm5",
                        "DataE,Npa5",
                        "DataE,Nrg5",
                        "DataE,Nsa5",
                        "DataE,Nso5",
                        "DataE,Nst5",
                        "DataE,Nsy5",
                        "DataE,Nvc5",
                        "DataE,Ocr5",
                        "DataE,Ply5",
                        "DataE,Poa5",
                        "DataE,Prg5",
                        "DataE,Rep5",
                        "DataE,Roa5",
                        "DataE,Rpb5",
                        "DataE,Sds1",
                        "DataE,Ssk5",
                        "DataE,Tdg5",
                        "DataE,Ucb5",
                        "DataE,Uco5",
                        "DataE,Udp5",
                        "DataE,Uls5",
                        "DataE,ave5",
                        "DataE,dvm5",
                        "DataE,fak5",
                        "DataE,fbk5",
                        "DataE,flk5",
                        "DataE,gra5",
                        "DataE,inv5",
                        "DataE,irl5",
                        "DataE,mfl5",
                        "DataE,spb5",
                        "DataF,Ans6",
                        "DataF,Def6",
                        "DataF,Hbz6",
                        "DataF,Hfs6",
                        "DataF,Nab6",
                        "DataF,Ncl6",
                        "DataF,Ncr6",
                        "DataF,Ncs6",
                        "DataF,Ndr6",
                        "DataF,Neg6",
                        "DataF,Nhs6",
                        "DataF,Nic6",
                        "DataF,Nlm6",
                        "DataF,Nrg6",
                        "DataF,Nvc6",
                        "DataF,Prg6",
                        "DataF,Roa6",
                        "DataF,Sds6",
                        "DataF,Ucb6",
                        "DataF,Uco6",
                        "DataF,dvm6",
                        "DataF,mfl6",
                        "DataG,Def7",
                        "DataG,Ndr7",
                        "DataG,Roa7",
                        "DataH,Def8",
                        "DataH,Ndr8",
                        "DataI,Ndr9",
                        "Dur,adur",
                        "Dur1,adur",
                        "EditorSuffix,ansf",
                        "EfctID,aeff",
                        "EfctID1,aeff",
                        "EffectArt,aeat",
                        "Effectsound,aefs",
                        "Effectsoundlooped,aefl",
                        "HeroDur,ahdu",
                        "HeroDur1,ahdu",
                        "Hotkey,ahky",
                        "LightningEffect,alig",
                        "MissileHoming,amho",
                        "Missilearc,amac",
                        "Missileart,amat",
                        "Missilespeed,amsp",
                        "Name,anam",
                        "Order,aord",
                        "Orderoff,aorf",
                        "Orderon,aoro",
                        "Requires,areq",
                        "Requiresamount,arqa",
                        "ResearchArt,arar",
                        "Researchbuttonpos_1,arpx",
                        "Researchbuttonpos_2,arpy",
                        "Researchhotkey,arhk",
                        "Researchtip,aret",
                        "Researchubertip,arut",
                        "Rng,aran",
                        "Rng1,aran",
                        "SpecialArt,asat",
                        "Specialattach,aspt",
                        "TargetArt,atat",
                        "Targetattach,ata0",
                        "Targetattach1,ata1",
                        "Targetattach2,ata2",
                        "Targetattach3,ata3",
                        "Targetattach4,ata4",
                        "Targetattach5,ata5",
                        "Targetattachcount,atac",
                        "Tip,atp1",
                        "Ubertip,aub1",
                        "UnButtonpos_1,aubx",
                        "UnButtonpos_2,auby",
                        "Unart,auar",
                        "Unhotkey,auhk",
                        "UnitID,Aplu",
                        "UnitID,Btl1",
                        "UnitID,Cha1",
                        "UnitID,Chl1",
                        "UnitID,Efnu",
                        "UnitID,Emeu",
                        "UnitID,Esvu",
                        "UnitID,Gydu",
                        "UnitID,Hwe1",
                        "UnitID,Ibl1",
                        "UnitID,Iglu",
                        "UnitID,Iobu",
                        "UnitID,Loa1",
                        "UnitID,Nbau",
                        "UnitID,Ndc2",
                        "UnitID,Ndou",
                        "UnitID,Nfyu",
                        "UnitID,Nsl1",
                        "UnitID,Nsyu",
                        "UnitID,Ntou",
                        "UnitID,Nvcu",
                        "UnitID,Osf1",
                        "UnitID,Raiu",
                        "UnitID,Stau",
                        "UnitID,Uin4",
                        "UnitID,Ulsu",
                        "UnitID,coau",
                        "UnitID,ent1",
                        "UnitID,exhu",
                        "UnitID,hwdu",
                        "UnitID,imou",
                        "UnitID,ipmu",
                        "UnitSkinID,ausk",
                        "Unorder,aoru",
                        "Untip,aut1",
                        "Unubertip,auu1",
                        "YDWEtip,Ytip",
                        "checkDep,achd",
                        "hero,aher",
                        "item,aite",
                        "levelSkip,alsk",
                        "levels,alev",
                        "priority,apri",
                        "race,arac",
                        "reqLevel,arlv",
                        "targs,atar",
                        "targs1,atar"
                    }
                },
                {
                    ObjectDataType.Buff, new string[]
                    {
                        "Buffart,fart",
                        "Bufftip,ftip",
                        "Buffubertip,fube",
                        "EditorName,fnam",
                        "EditorSuffix,fnsf",
                        "EffectArt,feat",
                        "Effectattach,feft",
                        "Effectsound,fefs",
                        "Effectsoundlooped,fefl",
                        "LightningEffect,flig",
                        "MissileHoming,fmho",
                        "Missilearc,fmac",
                        "Missileart,fmat",
                        "Missilespeed,fmsp",
                        "SpecialArt,fsat",
                        "Specialattach,fspt",
                        "Spelldetail,fspd",
                        "TargetArt,ftat",
                        "Targetattach,fta0",
                        "Targetattach1,fta1",
                        "Targetattach2,fta2",
                        "Targetattach3,fta3",
                        "Targetattach4,fta4",
                        "Targetattach5,fta5",
                        "Targetattachcount,ftac",
                        "isEffect,feff",
                        "race,frac"

                    }
                },
                {
                    ObjectDataType.Destructable, new string[]
                    {
                        "EditorSuffix,bsuf",
                        "HP,bhps",
                        "MMBlue,bmmb",
                        "MMGreen,bmmg",
                        "MMRed,bmmr",
                        "Name,bnam",
                        "UserList,busr",
                        "armor,barm",
                        "buildTime,bbut",
                        "canPlaceDead,bcpd",
                        "canPlaceRandScale,bcpr",
                        "category,bcat",
                        "cliffHeight,bclh",
                        "colorB,bvcb",
                        "colorG,bvcg",
                        "colorR,bvcr",
                        "deathSnd,bdsn",
                        "fatLOS,bflo",
                        "file,bfil",
                        "fixedRot,bfxr",
                        "flyH,bflh",
                        "fogRadius,bfra",
                        "fogVis,bfvi",
                        "goldRep,breg",
                        "lightweight,blit",
                        "loopSound,bsnd",
                        "lumberRep,brel",
                        "maxPitch,bmap",
                        "maxRoll,bmar",
                        "maxScale,bmas",
                        "minScale,bmis",
                        "numVar,bvar",
                        "occH,boch",
                        "onCliffs,bonc",
                        "onWater,bonw",
                        "pathTex,bptx",
                        "pathTexDeath,bptd",
                        "portraitmodel,bgpm",
                        "radius,brad",
                        "repairTime,bret",
                        "selSize,bsel",
                        "selcircsize,bgsc",
                        "selectable,bgse",
                        "shadow,bshd",
                        "showInMM,bsmm",
                        "targType,btar",
                        "texFile,btxf",
                        "texID,btxi",
                        "tilesetSpecific,btsp",
                        "tilesets,btil",
                        "useClickHelper,buch",
                        "useMMColor,bumm",
                        "walkable,bwal"
                    }
                },
                {
        ObjectDataType.Doodad, new string[]
                    {
                        "MMBlue,dmmb",
                        "MMGreen,dmmg",
                        "MMRed,dmmr",
                        "Name,dnam",
                        "UserList,dusr",
                        "animInFog,danf",
                        "canPlaceRandScale,dcpr",
                        "category,dcat",
                        "defScale,ddes",
                        "file,dfil",
                        "fixedRot,dfxr",
                        "floats,dflt",
                        "ignoreModelClick,dimc",
                        "maxPitch,dmap",
                        "maxRoll,dmar",
                        "maxScale,dmas",
                        "minScale,dmis",
                        "numVar,dvar",
                        "onCliffs,donc",
                        "onWater,donw",
                        "pathTex,dptx",
                        "selSize,dsel",
                        "shadow,dshd",
                        "showInFog,dshf",
                        "showInMM,dsmm",
                        "soundLoop,dsnd",
                        "tilesetSpecific,dtsp",
                        "tilesets,dtil",
                        "useClickHelper,duch",
                        "useMMColor,dumc",
                        "vertB,dvb1",
                        "vertG,dvg1",
                        "vertR,dvr1",
                        "visRadius,dvis",
                        "walkable,dwlk"
                    }
                },
                {
                    ObjectDataType.Item, new string[]
                    {
                        "Art,iico",
                        "Buttonpos_1,ubpx",
                        "Buttonpos_2,ubpy",
                        "Description,ides",
                        "HP,ihtp",
                        "Hotkey,uhot",
                        "Level,ilev",
                        "Name,unam",
                        "Requires,ureq",
                        "Requiresamount,urqa",
                        "Tip,utip",
                        "Ubertip,utub",
                        "abilList,iabi",
                        "armor,iarm",
                        "class,icla",
                        "colorB,iclb",
                        "colorG,iclg",
                        "colorR,iclr",
                        "cooldownID,icid",
                        "drop,idrp",
                        "droppable,idro",
                        "file,ifil",
                        "goldcost,igol",
                        "ignoreCD,iicd",
                        "lumbercost,ilum",
                        "morph,imor",
                        "oldLevel,ilvo",
                        "pawnable,ipaw",
                        "perishable,iper",
                        "pickRandom,iprn",
                        "powerup,ipow",
                        "prio,ipri",
                        "scale,isca",
                        "selSize,issc",
                        "sellable,isel",
                        "stackMax,ista",
                        "stockInitial,isit",
                        "stockMax,isto",
                        "stockRegen,istr",
                        "stockStart,isst",
                        "usable,iusa",
                        "uses,iuse",

                    }
                },
                {
                    ObjectDataType.Unit, new string[]
                    {
                        "AGI,uagi",
                        "AGIplus,uagp",
                        "Art,uico",
                        "Attachmentanimprops,uaap",
                        "Attachmentlinkprops,ualp",
                        "Awakentip,uawt",
                        "Boneprops,ubpr",
                        "BuildingSoundLabel,ubsl",
                        "Builds,ubui",
                        "Buttonpos_1,ubpx",
                        "Buttonpos_2,ubpy",
                        "Casterupgradeart,ucua",
                        "Casterupgradename,ucun",
                        "Casterupgradetip,ucut",
                        "DependencyOr,udep",
                        "Description,ides",
                        "EditorSuffix,unsf",
                        "Farea1,ua1f",
                        "Farea2,ua2f",
                        "HP,uhpm",
                        "Harea1,ua1h",
                        "Harea2,ua2h",
                        "Hfact1,uhd1",
                        "Hfact2,uhd2",
                        "Hotkey,uhot",
                        "INT,uint",
                        "INTplus,uinp",
                        "LoopingSoundFadeIn,ulfi",
                        "LoopingSoundFadeOut,ulfo",
                        "Makeitems,umki",
                        "MissileHoming1,umh1",
                        "MissileHoming2,umh2",
                        "MissileHoming_1,umh1",
                        "MissileHoming_2,umh2",
                        "Missilearc1,uma1",
                        "Missilearc2,uma2",
                        "Missilearc_1,uma1",
                        "Missilearc_2,uma2",
                        "Missileart1,ua1m",
                        "Missileart2,ua2m",
                        "Missileart_1,ua1m",
                        "Missileart_2,ua2m",
                        "Missilespeed1,ua1z",
                        "Missilespeed2,ua2z",
                        "Missilespeed_1,ua1z",
                        "Missilespeed_2,ua2z",
                        "MovementSoundLabel,umsl",
                        "Name,unam",
                        "Primary,upra",
                        "Propernames,upro",
                        "Qarea1,ua1q",
                        "Qarea2,ua2q",
                        "Qfact1,uqd1",
                        "Qfact2,uqd2",
                        "RandomSoundLabel,ursl",
                        "Requires,ureq",
                        "Requires1,urq1",
                        "Requires2,urq2",
                        "Requires3,urq3",
                        "Requires4,urq4",
                        "Requires5,urq5",
                        "Requires6,urq6",
                        "Requires7,urq7",
                        "Requires8,urq8",
                        "Requiresamount,urqa",
                        "Requirescount,urqc",
                        "Researches,ures",
                        "Revive,urev",
                        "Reviveat,urva",
                        "Revivetip,utpr",
                        "RngBuff1,urb1",
                        "RngBuff2,urb2",
                        "STR,ustr",
                        "STRplus,ustp",
                        "ScoreScreenIcon,ussi",
                        "Sellitems,usei",
                        "Sellunits,useu",
                        "Specialart,uspa",
                        "Targetart,utaa",
                        "Tip,utip",
                        "Trains,utra",
                        "Ubertip,utub",
                        "Upgrade,uupt",
                        "abilList,uabi",
                        "abilSkinList,uabs",
                        "acquire,uacq",
                        "animProps,uani",
                        "armor,uarm",
                        "atkType1,ua1t",
                        "atkType2,ua2t",
                        "auto,udaa",
                        "backSw1,ubs1",
                        "backSw2,ubs2",
                        "bldtm,ubld",
                        "blend,uble",
                        "blue,uclb",
                        "bountydice,ubdi",
                        "bountyplus,ubba",
                        "bountysides,ubsi",
                        "buffRadius,uabr",
                        "buffType,uabt",
                        "buildingShadow,ushb",
                        "campaign,ucam",
                        "canBuildOn,ucbo",
                        "canFlee,ufle",
                        "canSleep,usle",
                        "cargoSize,ucar",
                        "castbsw,ucbs",
                        "castpt,ucpt",
                        "collision,ucol",
                        "cool1,ua1c",
                        "cool2,ua2c",
                        "customTeamColor,utcc",
                        "damageLoss1,udl1",
                        "damageLoss2,udl2",
                        "death,udtm",
                        "deathType,udea",
                        "def,udef",
                        "defType,udty",
                        "defUp,udup",
                        "dice1,ua1d",
                        "dice2,ua2d",
                        "dmgUp1,udu1",
                        "dmgUp2,udu2",
                        "dmgplus1,ua1b",
                        "dmgplus2,ua2b",
                        "dmgpt1,udp1",
                        "dmgpt2,udp2",
                        "dropItems,udro",
                        "elevPts,uept",
                        "elevRad,uerd",
                        "fatLOS,ulos",
                        "file,umdl",
                        "fileVerFlags,uver",
                        "fmade,ufma",
                        "fogRad,ufrd",
                        "formation,ufor",
                        "fused,ufoo",
                        "goldRep,ugor",
                        "goldcost,ugol",
                        "green,uclg",
                        "heroAbilList,uhab",
                        "heroAbilSkinList,uhas",
                        "hideHeroBar,uhhb",
                        "hideHeroDeathMsg,uhhd",
                        "hideHeroMinimap,uhhm",
                        "hideOnMinimap,uhom",
                        "hostilePal,uhos",
                        "impactSwimZ,uisz",
                        "impactZ,uimz",
                        "inEditor,uine",
                        "isBuildOn,uibo",
                        "isbldg,ubdg",
                        "launchSwimZ,ulsz",
                        "launchX,ulpx",
                        "launchY,ulpy",
                        "launchZ,ulpz",
                        "level,ulev",
                        "lumberRep,ulur",
                        "lumberbountydice,ulbd",
                        "lumberbountyplus,ulba",
                        "lumberbountysides,ulbs",
                        "lumbercost,ulum",
                        "mana0,umpi",
                        "manaN,umpm",
                        "maxPitch,umxp",
                        "maxRoll,umxr",
                        "maxSpd,umas",
                        "minRange,uamn",
                        "minSpd,umis",
                        "modelScale,usca",
                        "moveFloor,umvf",
                        "moveHeight,umvh",
                        "movetp,umvt",
                        "nameCount,upru",
                        "nbmmIcon,unbm",
                        "nbrandom,unbr",
                        "nsight,usin",
                        "occH,uocc",
                        "orientInterp,uori",
                        "pathTex,upat",
                        "points,upoi",
                        "preventPlace,upap",
                        "prio,upri",
                        "propWin,uprw",
                        "race,urac",
                        "rangeN1,ua1r",
                        "rangeN2,ua2r",
                        "red,uclr",
                        "regenHP,uhpr",
                        "regenMana,umpr",
                        "regenType,uhrt",
                        "reptm,urtm",
                        "repulse,urpo",
                        "repulseGroup,urpg",
                        "repulseParam,urpp",
                        "repulsePrio,urpr",
                        "requirePlace,upar",
                        "requireWaterRadius,upaw",
                        "run,urun",
                        "scale,ussc",
                        "scaleBull,uscb",
                        "selCircOnWater,usew",
                        "selZ,uslz",
                        "shadowH,ushh",
                        "shadowOnWater,ushr",
                        "shadowW,ushw",
                        "shadowX,ushx",
                        "shadowY,ushy",
                        "showUI1,uwu1",
                        "showUI2,uwu2",
                        "showUI4,uwu2",
                        "sides1,ua1s",
                        "sides2,ua2s",
                        "sight,usid",
                        "spd,umvs",
                        "special,uspe",
                        "spillDist1,usd1",
                        "spillDist2,usd2",
                        "spillRadius1,usr1",
                        "spillRadius2,usr2",
                        "splashTargs1,ua1p",
                        "splashTargs2,ua2p",
                        "stockInitial,usit",
                        "stockMax,usma",
                        "stockRegen,usrg",
                        "stockStart,usst",
                        "targCount1,utc1",
                        "targCount2,utc2",
                        "targType,utar",
                        "targs1,ua1g",
                        "targs2,ua2g",
                        "teamColor,utco",
                        "tilesetSpecific,utss",
                        "tilesets,util",
                        "turnRate,umvr",
                        "type,utyp",
                        "uberSplat,uubs",
                        "unitShadow,ushu",
                        "unitSound,usnd",
                        "upgrades,upgr",
                        "useClickHelper,uuch",
                        "walk,uwal",
                        "weapTp1,ua1w",
                        "weapTp2,ua2w",
                        "weapType1,ucs1",
                        "weapType2,ucs2",
                        "weapsOn,uaen"
                    }
                },
                {
                    ObjectDataType.Upgrade, new string[]
                    {
                        "Art,gar1",
                        "Buttonpos_1,gbpx",
                        "Buttonpos_2,gbpy",
                        "EditorSuffix,gnsf",
                        "Hotkey,ghk1",
                        "Name,gnam",
                        "Requires,greq",
                        "Requiresamount,grqc",
                        "Tip,gtp1",
                        "Ubertip,gub1",
                        "base1,gba1",
                        "base2,gba2",
                        "base3,gba3",
                        "base4,gba4",
                        "class,gcls",
                        "code1,gco1",
                        "code2,gco2",
                        "code3,gco3",
                        "code4,gco4",
                        "effect1,gef1",
                        "effect2,gef2",
                        "effect3,gef3",
                        "effect4,gef4",
                        "global,glob",
                        "goldbase,gglb",
                        "goldmod,gglm",
                        "inherit,ginh",
                        "lumberbase,glmb",
                        "lumbermod,glmm",
                        "maxlevel,glvl",
                        "mod1,gmo1",
                        "mod2,gmo2",
                        "mod3,gmo3",
                        "mod4,gmo4",
                        "race,grac",
                        "timebase,gtib",
                        "timemod,gtim"
                    }
                }
            };

            PropertyIdRawCodePerObjectDataTypeColumn = data.ToDictionary(x => x.Key, x => x.Value.Select(y => y.Split(',')).GroupBy(y => y[0]).ToDictionary(y => y.Key, y => y.Select(y => y[1]).ToList(), StringComparer.InvariantCultureIgnoreCase));
        }

        public List<string> ConvertToPropertyIdPossibleRawCodes(ObjectDataType type, string columnName)
        {
            if (PropertyIdRawCodePerObjectDataTypeColumn.TryGetValue(type, out var rawCodePerObjectDataColumn) && rawCodePerObjectDataColumn.TryGetValue((columnName ?? "").Trim(), out var result))
            {
                return result;
            }

            return new List<string>();
        }

        public List<War3NetObjectDataWrapper> ToWar3NetObjectData(Dictionary<string, ObjectData> objectData, ObjectDataFormatVersion objectDataFormatVersion = ObjectDataFormatVersion.v3)
        {
            var result = new List<War3NetObjectDataWrapper>();
            foreach (ObjectDataType type in Enum.GetValues(typeof(ObjectDataType)))
            {
                var war3NetObjectData = GetEmptyObjectDataWrapper(type, objectDataFormatVersion);
                var war3NetObjectDataModifications = new List<War3NetObjectModificationWrapper>();

                foreach (var objectDataRecord in objectData.Where(x => x.Value.ObjectDataType == type))
                {
                    var dataModifications = new List<War3NetObjectDataModificationWrapper>();
                    foreach (var data in objectDataRecord.Value.Data)
                    {
                        var fourCCList = ConvertToPropertyIdPossibleRawCodes(type, data.Key);
                        if (!fourCCList.Any())
                        {
                            DebugSettings.Warn("Unknown SLK Column");
                        }

                        foreach (var fourCC in fourCCList)
                        {
                            var dataModificationWrapper = GetEmptyObjectDataModificationWrapper(type);
                            dataModificationWrapper.Id = fourCC.FromRawcode();
                            dataModificationWrapper.Value = data.Value;
                            dataModifications.Add(dataModificationWrapper);
                        }
                    }

                    var objectEntry = GetEmptyObjectModificationWrapper(type);
                    objectEntry.OldId = objectDataRecord.Key.FromRawcode();
                    objectEntry.Modifications = dataModifications;
                    war3NetObjectDataModifications.Add(objectEntry);
                }

                war3NetObjectData.BaseValues = war3NetObjectDataModifications;
                result.Add(war3NetObjectData);
            }

            return result;
        }

        public static War3NetObjectDataModificationWrapper GetEmptyObjectDataModificationWrapper(ObjectDataType type)
        {
            switch (type)
            {
                case ObjectDataType.Ability:
                    return new War3NetObjectDataModificationWrapper(new LevelObjectDataModification());
                case ObjectDataType.Buff:
                    return new War3NetObjectDataModificationWrapper(new SimpleObjectDataModification());
                case ObjectDataType.Destructable:
                    return new War3NetObjectDataModificationWrapper(new SimpleObjectDataModification());
                case ObjectDataType.Doodad:
                    return new War3NetObjectDataModificationWrapper(new VariationObjectDataModification());
                case ObjectDataType.Item:
                    return new War3NetObjectDataModificationWrapper(new SimpleObjectDataModification());
                case ObjectDataType.Unit:
                    return new War3NetObjectDataModificationWrapper(new SimpleObjectDataModification());
                case ObjectDataType.Upgrade:
                    return new War3NetObjectDataModificationWrapper(new LevelObjectDataModification());
                default:
                    throw new NotImplementedException();
            }
        }

        public static War3NetObjectModificationWrapper GetEmptyObjectModificationWrapper(ObjectDataType type)
        {
            switch (type)
            {
                case ObjectDataType.Ability:
                    return new War3NetObjectModificationWrapper(new LevelObjectModification());
                case ObjectDataType.Buff:
                    return new War3NetObjectModificationWrapper(new SimpleObjectModification());
                case ObjectDataType.Destructable:
                    return new War3NetObjectModificationWrapper(new SimpleObjectModification());
                case ObjectDataType.Doodad:
                    return new War3NetObjectModificationWrapper(new VariationObjectModification());
                case ObjectDataType.Item:
                    return new War3NetObjectModificationWrapper(new SimpleObjectModification());
                case ObjectDataType.Unit:
                    return new War3NetObjectModificationWrapper(new SimpleObjectModification());
                case ObjectDataType.Upgrade:
                    return new War3NetObjectModificationWrapper(new LevelObjectModification());
                default:
                    throw new NotImplementedException();
            }
        }

        public static War3NetObjectDataWrapper GetEmptyObjectDataWrapper(ObjectDataType type, ObjectDataFormatVersion objectDataFormatVersion = ObjectDataFormatVersion.v3)
        {
            switch (type)
            {
                case ObjectDataType.Ability:
                    return new War3NetObjectDataWrapper(new AbilityObjectData(objectDataFormatVersion));
                case ObjectDataType.Buff:
                    return new War3NetObjectDataWrapper(new BuffObjectData(objectDataFormatVersion));
                case ObjectDataType.Destructable:
                    return new War3NetObjectDataWrapper(new DestructableObjectData(objectDataFormatVersion));
                case ObjectDataType.Doodad:
                    return new War3NetObjectDataWrapper(new DoodadObjectData(objectDataFormatVersion));
                case ObjectDataType.Item:
                    return new War3NetObjectDataWrapper(new ItemObjectData(objectDataFormatVersion));
                case ObjectDataType.Unit:
                    return new War3NetObjectDataWrapper(new UnitObjectData(objectDataFormatVersion));
                case ObjectDataType.Upgrade:
                    return new War3NetObjectDataWrapper(new UpgradeObjectData(objectDataFormatVersion));
                default:
                    throw new NotImplementedException();
            }
        }
    }
}