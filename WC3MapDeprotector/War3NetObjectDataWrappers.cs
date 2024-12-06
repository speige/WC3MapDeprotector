using System.Text;
using War3Net.Build.Object;
using System.Collections.ObjectModel;
using War3Net.Common.Providers;
using War3Net.Build.Extensions;
using NuGet.Packaging;

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
            set
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
            protected set
            {
                ObjectDataModification.Value = value;
            }
        }

        public void SetValue(object value, War3Net.Build.Object.ObjectDataType objectDataType)
        {
            Type = objectDataType;

            if (objectDataType == War3Net.Build.Object.ObjectDataType.Int && int.TryParse(value?.ToString(), out var valueAsInt))
            {
                Value = valueAsInt;
                return;
            }
            else if ((objectDataType == War3Net.Build.Object.ObjectDataType.Real || objectDataType == War3Net.Build.Object.ObjectDataType.Unreal) && float.TryParse(value?.ToString(), out var valueAsFloat))
            {
                Value = valueAsFloat;
                return;
            }
            else if (objectDataType == War3Net.Build.Object.ObjectDataType.Bool)
            {
                Type = War3Net.Build.Object.ObjectDataType.Int;
                if (bool.TryParse(value?.ToString(), out var valueAsBool))
                {
                    Value = valueAsBool ? 1 : 0;
                    return;
                }
                else if (int.TryParse(value?.ToString(), out var valueAsInt2))
                {
                    Value = valueAsInt2 > 0 ? 1 : 0;
                    return;
                }
            }
            else if (objectDataType == War3Net.Build.Object.ObjectDataType.Char)
            {
                Type = War3Net.Build.Object.ObjectDataType.String;
                Value = value?.ToString() ?? ""; //  (value?.ToString() ?? "").FirstOrDefault();
                return;
            }

            Type = War3Net.Build.Object.ObjectDataType.String;
            Value = value?.ToString() ?? "";
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
            set
            {
                switch (ObjectModification)
                {
                    case LevelObjectModification level:
                        level.Unk.Clear();
                        level.Unk.AddRange(value);
                        return;
                    case SimpleObjectModification simple:
                        simple.Unk.Clear();
                        simple.Unk.AddRange(value);
                        return;
                    case VariationObjectModification variation:
                        variation.Unk.Clear();
                        variation.Unk.AddRange(value);
                        return;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public ReadOnlyCollection<War3NetObjectDataModificationWrapper> Modifications
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

    public class War3NetObjectDataCollectionWrapper
    {
        public object ObjectData { get; protected set; }
        public War3NetObjectDataCollectionWrapper(object objectData)
        {
            ObjectData = objectData;
        }

        public override int GetHashCode()
        {
            return ObjectData?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            return ObjectData?.Equals((obj as War3NetObjectDataCollectionWrapper)?.ObjectData) ?? false;
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

        public override string ToString()
        {
            return ObjectData.ToString();
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

        public ReadOnlyCollection<War3NetObjectModificationWrapper> OriginalOverrides
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

        public ReadOnlyCollection<War3NetObjectModificationWrapper> CustomOverrides
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

    public class War3NetSkinnableObjectDataWrapper
    {
        public War3NetObjectDataCollectionWrapper CoreData { get; }
        public War3NetObjectDataCollectionWrapper SkinData { get; }

        public War3NetSkinnableObjectDataWrapper(War3NetObjectDataCollectionWrapper baseData, War3NetObjectDataCollectionWrapper skinData)
        {
            CoreData = baseData;
            SkinData = skinData;
        }

        public override int GetHashCode()
        {
            return (CoreData?.GetHashCode() ?? 0) ^ (SkinData?.GetHashCode() ?? 0);
        }

        public override bool Equals(object obj)
        {
            return (CoreData?.Equals((obj as War3NetSkinnableObjectDataWrapper)?.CoreData) ?? false) && (SkinData?.Equals((obj as War3NetSkinnableObjectDataWrapper)?.SkinData) ?? false);
        }

        public override string ToString()
        {
            return CoreData.ToString() + "_" + SkinData.ToString();
        }

        public ObjectDataType ObjectDataType
        {
            get
            {
                var result = CoreData.ObjectDataType;
                if (result != SkinData.ObjectDataType)
                {
                    throw new Exception("War3NetSkinnableObjectDataWrapper ObjectDataType mismatch");
                }

                return result;
            }
        }

        public ObjectDataFormatVersion FormatVersion
        {
            get
            {
                var result = CoreData.FormatVersion;
                SkinData.FormatVersion = result;
                return result;
            }
            set
            {
                CoreData.FormatVersion = value;
                SkinData.FormatVersion = value;
            }
        }

        public ReadOnlyCollection<War3NetObjectModificationWrapper> OriginalOverrides
        {
            get
            {
                return CoreData.OriginalOverrides.Concat(SkinData.OriginalOverrides).ToList().AsReadOnly();
            }
        }

        public ReadOnlyCollection<War3NetObjectModificationWrapper> CustomOverrides
        {
            get
            {
                return CoreData.CustomOverrides.Concat(SkinData.CustomOverrides).ToList().AsReadOnly();
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
        Upgrade,
        GameplayConstants,
        Mix_Of_Multiple,
        Unknown
    }

    public class GenericObjectData<Type_PropertyName>
    {
        static GenericObjectData()
        {
            if (typeof(Type_PropertyName) != typeof(string) && typeof(Type_PropertyName) != typeof(FourCC))
            {
                throw new InvalidOperationException($"Type {typeof(Type_PropertyName).Name} is not supported.");
            }
        }

        public ObjectDataType ObjectDataType { get; set; }
        public Type_PropertyName Parent { get; set; }
        protected Dictionary<Type_PropertyName, List<object>> Data { get; init; }

        public GenericObjectData(ObjectDataType objectDataType, IEqualityComparer<Type_PropertyName> comparer = default)
        {
            ObjectDataType = objectDataType;
            Data = new Dictionary<Type_PropertyName, List<object>>(comparer);
        }

        protected List<object> GetPerLevelOverrides(Type_PropertyName property)
        {
            return Data.GetValueOrDefault(property);
        }

        public void ClearValues(Type_PropertyName property)
        {
            Data.Remove(property);
        }

        public List<Type_PropertyName> GetPropertyNames()
        {
            return Data.Keys.ToList();
        }

        public int GetLevelCount(Type_PropertyName property)
        {
            return GetPerLevelOverrides(property)?.Count ?? 0;
        }

        public void AddNextLevelValue(Type_PropertyName property, object value)
        {
            int currentLevel = GetLevelCount(property);
            SetValue(property, value, currentLevel + 1);
        }

        public object GetValue(Type_PropertyName property, int level = 1)
        {
            level = Math.Min(level, GetLevelCount(property));
            if (level == 0)
            {
                return null;
            }

            return GetPerLevelOverrides(property)?[level - 1];
        }

        public void SetValue(Type_PropertyName property, object value, int level = 1)
        {
            level = Math.Max(level, 1);
            var perLevelOverrides = GetPerLevelOverrides(property);
            if (perLevelOverrides == null)
            {
                perLevelOverrides = new List<object>();
                Data[property] = perLevelOverrides;
            }

            while (perLevelOverrides.Count < level)
            {
                perLevelOverrides.Add(value);
            }

            perLevelOverrides[level - 1] = value;
        }

        public HashSet<string> ExportAllStringValues(HashSet<Type_PropertyName> propertyFilter = null)
        {
            var objects = Data;
            if (propertyFilter != null)
            {
                objects = objects.Where(x => propertyFilter.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            }

            return new HashSet<string>(objects.SelectMany(y => y.Value).OfType<string>());
        }

        public GenericObjectData<Type_PropertyName> Clone()
        {
            var result = new GenericObjectData<Type_PropertyName>(ObjectDataType, Data.Comparer);
            result.Parent = Parent;
            result.Data.AddRange(Data.Select(x => new KeyValuePair<Type_PropertyName, List<object>>(x.Key, new List<object>(x.Value))));
            return result;
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
    }
}