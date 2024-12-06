using IniParser.Model.Configuration;
using IniParser;
using System.Text;
using War3Net.Build;
using War3Net.Build.Object;
using War3Net.CodeAnalysis.Decompilers;
using War3Net.CodeAnalysis.Jass.Extensions;
using System.Text.RegularExpressions;
using NuGet.Packaging;
using ObjectData = WC3MapDeprotector.GenericObjectData<WC3MapDeprotector.FourCC>;

namespace WC3MapDeprotector
{
    public enum ObjectEditorDataType { abilCode, abilityList, abilitySkinList, aiBuffer, armorType, attackBits, attackTable, attackType, attributeType, @bool, buffList, channelFlags, channelType, @char, combatSound, deathType, defenseTable, defenseType, defenseTypeInt, destructableCategory, detectionType, doodadCategory, effectList, fullFlags, heroAbilityList, icon, @int, intList, interactionFlags, itemClass, itemList, lightningEffect, lightningList, model, modelList, morphFlags, moveType, orderString, pathingListPrevent, pathingListRequire, pathingTexture, pickFlags, real, regenType, shadowImage, shadowTexture, silenceFlags, soundLabel, spellDetail, stackFlags, @string, stringList, targetList, teamColor, techAvail, techList, texture, tilesetList, uberSplat, unitClass, unitCode, unitList, unitRace, unitSkinList, unitSound, unreal, unrealList, upgradeClass, upgradeCode, upgradeEffect, upgradeList, versionFlags, weaponType }
    public static class ObjectEditorDataTypeExtensions
    {
        public static War3Net.Build.Object.ObjectDataType ConvertToObjectDataType(this ObjectEditorDataType objectEditorDataType)
        {
            switch (objectEditorDataType)
            {
                case ObjectEditorDataType.@bool:
                case ObjectEditorDataType.@int:
                case ObjectEditorDataType.attackBits:
                case ObjectEditorDataType.channelFlags:
                case ObjectEditorDataType.channelType:
                case ObjectEditorDataType.deathType:
                case ObjectEditorDataType.defenseTypeInt:
                case ObjectEditorDataType.detectionType:
                case ObjectEditorDataType.fullFlags:
                case ObjectEditorDataType.interactionFlags:
                case ObjectEditorDataType.morphFlags:
                case ObjectEditorDataType.pickFlags:
                case ObjectEditorDataType.silenceFlags:
                case ObjectEditorDataType.spellDetail:
                case ObjectEditorDataType.stackFlags:
                case ObjectEditorDataType.teamColor:
                case ObjectEditorDataType.versionFlags:
                case ObjectEditorDataType.techAvail:
                    return War3Net.Build.Object.ObjectDataType.Int;
                case ObjectEditorDataType.real:
                    return War3Net.Build.Object.ObjectDataType.Real;
                case ObjectEditorDataType.unreal:
                    return War3Net.Build.Object.ObjectDataType.Unreal;
            }

            return War3Net.Build.Object.ObjectDataType.String;
        }
    }

    public partial class ObjectEditor
    {
        protected HashSet<FourCC> _perLevelAttributeMaxLevelsPropertyIDs = new HashSet<FourCC>() { "alev", "glvl" };
        //todo: add cached Dictionary<ObjectDataType, FourCC> for faster filtering of 3 objectDataCollections. How to keep in-sync? custom class?
        protected Dictionary<FourCC, GenericObjectData<string>> _objectMetaDataCollection_baseGame;
        protected Dictionary<ObjectDataType, HashSet<FourCC>> _perLevelPropertyIDs;
        protected Dictionary<ObjectDataType, Dictionary<string, List<(FourCC propertyId, int level)>>> _propertyNameToIDs;
        protected HashSet<FourCC> _validParents;
        protected Dictionary<FourCC, ObjectData> _objectDataCollection_baseGame;
        protected Dictionary<FourCC, ObjectData> _objectDataCollection_overrides;

        public ObjectEditor(string baseGameFileDirectory)
        {
            var baseGameFiles = Directory.GetFiles(baseGameFileDirectory, "*.*", SearchOption.AllDirectories).ToList();
            var metadataFileNames = baseGameFiles.Where(x => x.Contains("metadata", StringComparison.InvariantCultureIgnoreCase)).ToList();

            _objectMetaDataCollection_baseGame = metadataFileNames.SelectMany(x => ParseObjectDataCollectionFromFile_Internal_NoReformatting(x).Select(x => new KeyValuePair<FourCC, GenericObjectData<string>>(x.Value.GetValue("ID")?.ToString(), x.Value))).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => MergeObjectData(x.Select(y => y.Value).ToArray()));

            _perLevelPropertyIDs = new Dictionary<ObjectDataType, HashSet<FourCC>>();
            _propertyNameToIDs = new Dictionary<ObjectDataType, Dictionary<string, List<(FourCC propertyId, int level)>>>();
            foreach (var objectDataType in Enum.GetValues(typeof(ObjectDataType)).Cast<ObjectDataType>())
            {
                var objectDataCollection = _objectMetaDataCollection_baseGame.Where(x => x.Value.ObjectDataType == objectDataType).ToList();
                _perLevelPropertyIDs[objectDataType] = new HashSet<FourCC>(_objectMetaDataCollection_baseGame.Where(x => x.Value.ObjectDataType == objectDataType && (x.Value.GetValue("repeat")?.ToString() ?? "0") != "0").Select(x => (FourCC)x.Key));
                _propertyNameToIDs[objectDataType] = objectDataCollection.SelectMany(x =>
                {
                    var result = new List<KeyValuePair<string, (FourCC propertyId, int level)>>();
                    var field = x.Value.GetValue("field")?.ToString() ?? "";
                    if (int.TryParse(x.Value.GetValue("data")?.ToString() ?? "", out var data) && data > 0)
                    {
                        field += (char)(((byte)'A') + (data - 1));
                    }

                    result.Add(new KeyValuePair<string, (FourCC propertyId, int level)>(field, (x.Key, 0)));
                    if (int.TryParse(x.Value.GetValue("repeat")?.ToString() ?? "", out var repeat))
                    {
                        if (int.TryParse(x.Value.GetValue("appendIndex")?.ToString() ?? "", out var appendIndex))
                        {
                            const int MAX_EDITOR_LEVELS = 100;
                            repeat = Math.Max(repeat, appendIndex > 0 ? MAX_EDITOR_LEVELS : 0);
                        }

                        for (var level = 1; level <= repeat; level++)
                        {
                            result.Add(new KeyValuePair<string, (FourCC propertyId, int level)>($"{field}{level}", (x.Key, level)));
                        }
                    }
                    return result;
                }).GroupBy(x => x.Key.ToString().ToLower()).ToDictionary(x => x.Key, x => x.Select(y => y.Value).Distinct().ToList(), StringComparer.InvariantCultureIgnoreCase);
            }

            // Items are a subclass of Units, they have the same property fields
            _perLevelPropertyIDs[ObjectDataType.Item] = _perLevelPropertyIDs[ObjectDataType.Unit];
            _propertyNameToIDs[ObjectDataType.Item] = _propertyNameToIDs[ObjectDataType.Unit];

            var parsedDataFiles = baseGameFiles.Except(metadataFileNames).GroupBy(x => (Path.GetExtension(x) ?? "").ToLower().Trim()).ToDictionary(x => x.Key, x => x.Select(y => ParseObjectDataCollectionFromFile(y)).ToList());
            _validParents = new HashSet<FourCC>(parsedDataFiles[".slk"].SelectMany(x => x.Keys).Distinct());
            _objectDataCollection_baseGame = MergeObjectDataCollections(parsedDataFiles.OrderBy(x => x.Key == ".slk" ? 0 : 1).Select(x => x.Value).SelectMany(x => x).ToArray());

            var allBaseGameValues = _objectDataCollection_baseGame.SelectMany(objectData => objectData.Value.GetPropertyNames().SelectMany(propertyId => Enumerable.Range(1, objectData.Value.GetLevelCount(propertyId)).Select(level => new KeyValuePair<FourCC, string>(propertyId, objectData.Value.GetValue(propertyId, level)?.ToString() ?? "")))).ToList();
            _objectDataCollection_overrides = new Dictionary<FourCC, ObjectData>();
        }
        public bool IsBaseGameObject(FourCC objectID)
        {
            return _objectMetaDataCollection_baseGame.ContainsKey(objectID) || _objectDataCollection_baseGame.ContainsKey(objectID);
        }

        public bool IsCustomObject(FourCC objectID)
        {
            return !IsBaseGameObject(objectID);
        }

        public bool ObjectIDExists(FourCC objectId)
        {
            return _objectDataCollection_overrides.ContainsKey(objectId) || _objectDataCollection_baseGame.ContainsKey(objectId);
        }

        public ObjectDataType? GetObjectDataTypeForID(FourCC objectId, bool includeInheritedValues = true)
        {
            if (_objectDataCollection_overrides.TryGetValue(objectId, out var objectData) || _objectDataCollection_baseGame.TryGetValue(objectId, out objectData))
            {
                return objectData.ObjectDataType;
            }

            return null;
        }

        public ObjectData GetObjectDataForID_ReadOnly(FourCC objectId, bool includeInheritedValues = true)
        {
            _objectDataCollection_overrides.TryGetValue(objectId, out var overrideObjectData);
            _objectDataCollection_overrides.TryGetValue(overrideObjectData?.Parent ?? "", out var parentOverrideObjectData);
            _objectDataCollection_baseGame.TryGetValue(objectId, out var baseObjectData);
            _objectDataCollection_baseGame.TryGetValue(overrideObjectData?.Parent ?? baseObjectData?.Parent ?? "", out var parentBaseObjectData);
            //todo: add recursive parent merging because baseGame objects like Aro2 can inherit from fake baseGame objects like Aroo

            if (!includeInheritedValues)
            {
                return (overrideObjectData ?? baseObjectData)?.Clone();
            }

            return MergeObjectData(parentBaseObjectData, baseObjectData, parentOverrideObjectData, overrideObjectData);
        }

        public List<(FourCC propertyId, int level)> TranslatePropertyNameToIDs(ObjectDataType objectDataType, string propertyName)
        {
            return _propertyNameToIDs.GetValueOrDefault(objectDataType)?.GetValueOrDefault(propertyName) ?? new List<(FourCC propertyId, int level)>();
        }

        protected Dictionary<string, GenericObjectData<string>> RemoveBlankValues(Dictionary<string, GenericObjectData<string>> objectDataCollection)
        {
            var result = objectDataCollection.ToDictionary(x => x.Key, x => x.Value.Clone());
            foreach (var record in result)
            {
                var objectData = record.Value;

                foreach (var property in objectData.GetPropertyNames())
                {
                    var remove = true;
                    var levelCount = objectData.GetLevelCount(property);
                    for (var level = 1; level <= levelCount; level++)
                    {
                        var value = (objectData.GetValue(property, level)?.ToString() ?? "").Trim();
                        if (value != "")
                        {
                            remove = false;
                        }
                    }

                    if (remove)
                    {
                        objectData.ClearValues(property);
                    }
                }
            }

            return result;
        }

        protected string unquoteString(string value)
        {
            value = value?.Trim() ?? "";
            if (value.StartsWith('"') && value.EndsWith('"') && value.Count(x => x == '"') == 2)
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        [GeneratedRegex(@"(<[^>]*>)|(.)", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_ObjectDataPropertyReferenceCSV();

        protected List<string> SplitQuotedCSV(string quotedCSV)
        {
            const string TEMP_CSV_REPLACEMENT = "###TEMP_CSV_REPLACEMENT###";
            var matches = Regex_ObjectDataPropertyReferenceCSV().Matches(quotedCSV);
            if (matches.Any())
            {
                quotedCSV = matches.Select(x => x.Value.Length == 1 ? x.Value : x.Value.Replace(",", TEMP_CSV_REPLACEMENT)).Aggregate((x, y) => x + y);
            }
            var isQuoted = quotedCSV.StartsWith('"') && quotedCSV.EndsWith('"');
            var split = isQuoted ? quotedCSV.Substring(1, quotedCSV.Length - 2).Split("\",\"") : quotedCSV.Split(',');
            if (split.Length > 1)
            {
                return split.Select(x => x.Replace(TEMP_CSV_REPLACEMENT, ",")).ToList();
            }

            return new List<string>() { unquoteString(quotedCSV.Replace(TEMP_CSV_REPLACEMENT, ",")) };
        }

        protected Dictionary<FourCC, ObjectData> ConvertSLKAndTXTPropertyNamesToFourCC(Dictionary<string, GenericObjectData<string>> objectDataCollection)
        {
            var result = new Dictionary<FourCC, ObjectData>();
            foreach (var objectData in objectDataCollection)
            {
                var objectId = (FourCC)objectData.Key;
                if (string.IsNullOrWhiteSpace(objectId))
                {
                    continue; // invalid Object
                }

                var convertedObjectData = new ObjectData(objectData.Value.ObjectDataType);
                result[objectId] = convertedObjectData;
                foreach (var propertyName in objectData.Value.GetPropertyNames())
                {
                    if (propertyName.Equals("code", StringComparison.InvariantCultureIgnoreCase))
                    {
                        convertedObjectData.Parent = objectData.Value.GetValue(propertyName).ToString();
                    }

                    var existingValue = objectData.Value.GetValue(propertyName)?.ToString() ?? "";
                    var propertyIDs = TranslatePropertyNameToIDs(objectData.Value.ObjectDataType, propertyName);
                    if (objectData.Value.ObjectDataType == ObjectDataType.Unknown || objectData.Value.ObjectDataType == ObjectDataType.Mix_Of_Multiple)
                    {
                        propertyIDs = Enum.GetValues(typeof(ObjectDataType)).Cast<ObjectDataType>().SelectMany(x => TranslatePropertyNameToIDs(x, propertyName)).Distinct().ToList();
                    }
                    var metaDataPerProperty = propertyIDs.ToDictionary(x => x, x => _objectMetaDataCollection_baseGame.TryGetValue(x.propertyId, out var metaData) ? metaData : null);
                    var splitCSV = metaDataPerProperty.DistinctBy(x => x.Value.GetValue("index")?.ToString() ?? "").Count() > 1 || metaDataPerProperty.Any(x => (x.Value.GetValue("repeat")?.ToString() ?? "0").Trim() != "0" && (x.Value.GetValue("appendIndex")?.ToString() ?? "1").Trim() != "1");
                    var splitIndexes = splitCSV ? SplitQuotedCSV(existingValue) : null;
                    foreach (var fourCC in propertyIDs)
                    {
                        if (splitCSV)
                        {
                            var metaData = metaDataPerProperty[fourCC];
                            if ((metaData.GetValue("repeat")?.ToString() ?? "0").Trim() != "0" && (metaData.GetValue("appendIndex")?.ToString() ?? "1").Trim() != "1")
                            {
                                for (var index = 0; index < splitIndexes.Count; index++)
                                {
                                    convertedObjectData.SetValue(fourCC.propertyId, unquoteString(splitIndexes[index]), index + 1);
                                }
                            }
                            else if (int.TryParse(metaData.GetValue("index")?.ToString() ?? "", out var index) && index != -1)
                            {
                                if (objectData.Value.GetLevelCount(propertyName) != 1)
                                {
                                    DebugSettings.Warn("Indexed property also has levels");
                                    continue;
                                }

                                if (splitIndexes.Count > index)
                                {
                                    convertedObjectData.SetValue(fourCC.propertyId, unquoteString(splitIndexes[index]), fourCC.level);
                                }
                            }
                        }
                        else
                        {
                            convertedObjectData.SetValue(fourCC.propertyId, unquoteString(existingValue), fourCC.level);
                        }
                    }
                }
            }

            return result;
        }

        public static ObjectDataType GetObjectDataTypeByFileName(string fileName)
        {
            if (fileName.Contains("CommonAbilityStrings", StringComparison.InvariantCultureIgnoreCase) || fileName.Contains("ItemAbilityStrings", StringComparison.InvariantCultureIgnoreCase))
            {
                DebugSettings.Warn("These files have a mix of abilities and buffs, need to determine from matching FourCC while merging other data files");
                return ObjectDataType.Mix_Of_Multiple;
            }

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
            if (fileName.Contains("misc", StringComparison.InvariantCultureIgnoreCase))
            {
                return ObjectDataType.GameplayConstants;
            }

            DebugSettings.Warn("Unrecognized data file name " + fileName);
            return ObjectDataType.Unknown;
        }

        public static GenericObjectData<T> MergeObjectData<T>(params GenericObjectData<T>[] objectDataArray)
        {
            objectDataArray = objectDataArray?.Where(x => x != null).ToArray();
            if (objectDataArray?.Any() == false)
            {
                return null;
            }

            var result = objectDataArray.First().Clone();
            foreach (var objectData in objectDataArray.Skip(1))
            {
                if (result.ObjectDataType == ObjectDataType.Mix_Of_Multiple || result.ObjectDataType == ObjectDataType.Unknown)
                {
                    if (objectData.ObjectDataType != ObjectDataType.Mix_Of_Multiple && objectData.ObjectDataType != ObjectDataType.Unknown)
                    {
                        result.ObjectDataType = objectData.ObjectDataType;
                    }
                }

                result.Parent ??= objectData.Parent;
                foreach (var propertyName in objectData.GetPropertyNames())
                {
                    var levelCount = objectData.GetLevelCount(propertyName);
                    for (var level = 1; level <= levelCount; level++)
                    {
                        result.SetValue(propertyName, objectData.GetValue(propertyName, level), level);
                    }
                }

            }

            return result;
        }

        public static Dictionary<T, GenericObjectData<T>> MergeObjectDataCollections<T>(params Dictionary<T, GenericObjectData<T>>[] objectDataCollectionArray)
        {
            var result = new Dictionary<T, GenericObjectData<T>>();
            foreach (var objectEditorData in objectDataCollectionArray)
            {
                foreach (var objectData in objectEditorData)
                {
                    var fourCC = objectData.Key;
                    if (string.IsNullOrWhiteSpace(fourCC?.ToString()))
                    {
                        DebugSettings.Warn("Empty FourCC");
                        continue;
                    }

                    if (!result.ContainsKey(fourCC))
                    {
                        result[fourCC] = objectData.Value.Clone();
                    }
                    else
                    {
                        result[fourCC] = MergeObjectData(result[fourCC], objectData.Value);
                    }
                }
            }

            return result;
        }

        public void RepairInvalidData()
        {
            AddDefaultValuesToCustomObjects();
            FillGapsInPerLevelValuesForCustomObjects();
            RemoveInvalidParents();
            FixMissingParents();
            RemoveSpecificPropertiesWithWrongParent();
        }

        protected void RemoveSpecificPropertiesWithWrongParent()
        {
            var metadataPerObjectDataType = _objectMetaDataCollection_baseGame.GroupBy(x => x.Value.ObjectDataType).ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Key, y => y.Value));
            foreach (var objectData in _objectDataCollection_overrides)
            {
                if (!metadataPerObjectDataType.TryGetValue(objectData.Value.ObjectDataType, out var metaDataAllProperties))
                {
                    continue;
                }

                foreach (var propertyName in objectData.Value.GetPropertyNames())
                {
                    if (!metaDataAllProperties.TryGetValue(propertyName, out var propertyMetaData))
                    {
                        continue;
                    }

                    var specificFields = propertyMetaData.GetValue("useSpecific")?.ToString();
                    if (string.IsNullOrWhiteSpace(specificFields))
                    {
                        continue;
                    }

                    specificFields = specificFields.Replace(".", ","); // bug in baseGame slk files
                    var validParents = specificFields.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => (FourCC)x).ToHashSet();
                    if (!validParents.Contains(objectData.Value.Parent))
                    {
                        objectData.Value.ClearValues(propertyName);
                    }
                }
            }
        }

        protected void FillGapsInPerLevelValuesForCustomObjects()
        {
            var customObjects = _objectDataCollection_overrides.Where(x => IsCustomObject(x.Key)).ToList();
            foreach (var objectData in customObjects)
            {
                if (!_perLevelPropertyIDs.TryGetValue(objectData.Value.ObjectDataType, out var perLevelCSVPropertyNames))
                {
                    continue;
                }

                var levels = 0;
                foreach (var levelPropertyName in _perLevelAttributeMaxLevelsPropertyIDs)
                {
                    if (int.TryParse(objectData.Value.GetValue(levelPropertyName)?.ToString(), out levels) && levels > 0)
                    {
                        break;
                    }
                }
                if (levels < 1)
                {
                    continue;
                }

                foreach (var propertyName in perLevelCSVPropertyNames)
                {
                    object defaultValue = null;
                    for (var level = 1; level <= levels; level++)
                    {
                        if (defaultValue != null)
                        {
                            break;
                        }

                        defaultValue = objectData.Value.GetValue(propertyName, level);
                    }

                    if (defaultValue == null)
                    {
                        continue;
                    }

                    for (var level = 1; level <= levels; level++)
                    {
                        var value = objectData.Value.GetValue(propertyName, level);
                        if (value == null || objectData.Value.GetLevelCount(propertyName) < level)
                        {
                            objectData.Value.SetValue(propertyName, defaultValue, level);
                        }
                        else
                        {
                            defaultValue = value;
                        }
                    }
                }
            }
        }

        protected void AddDefaultValuesToCustomObjects()
        {
            var metadataPerObjectDataType = _objectMetaDataCollection_baseGame.GroupBy(x => x.Value.ObjectDataType).ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Key, y => y.Value));
            var customObjects = _objectDataCollection_overrides.Where(x => IsCustomObject(x.Key)).ToList();
            foreach (var objectData in customObjects)
            {
                if (!metadataPerObjectDataType.TryGetValue(objectData.Value.ObjectDataType, out var metaDataAllProperties))
                {
                    continue;
                }

                var missingProperties = metaDataAllProperties.Where(x => objectData.Value.GetValue(x.Key) == null).ToList();
                foreach (var propertyMetaData in missingProperties)
                {
                    var defaultValue = GetDefaultPropertyValue(propertyMetaData.Key);
                    if (defaultValue != null)
                    {
                        objectData.Value.SetValue(propertyMetaData.Key, defaultValue);
                    }
                }
            }
        }

        public object GetDefaultPropertyValue(FourCC propertyId)
        {
            var metadata = _objectMetaDataCollection_baseGame.GetValueOrDefault(propertyId);
            if (metadata == null)
            {
                return null;
            }

            var dataTypeString = (metadata.GetValue("type")?.ToString() ?? "").Trim().ToLower();
            if (!Enum.TryParse<ObjectEditorDataType>(dataTypeString, true, out var dataType))
            {
                DebugSettings.Warn("Unrecognized metadata type");
                return null;
            }

            switch (dataType.ConvertToObjectDataType())
            {
                case War3Net.Build.Object.ObjectDataType.Bool:
                case War3Net.Build.Object.ObjectDataType.Int:
                    return 0;
                case War3Net.Build.Object.ObjectDataType.Real:
                case War3Net.Build.Object.ObjectDataType.Unreal:
                    return 0f;
                case War3Net.Build.Object.ObjectDataType.Char:
                case War3Net.Build.Object.ObjectDataType.String:
                    return "";
            }

            return null;

            /*
            var minValue = metadata.GetValue("minVal")?.ToString() ?? "";

            if (minValue == null)
            {
                DebugSettings.Warn("Unable to determine default value for metadata type");
            }

            return minValue ?? "";
            */
        }

        protected void RemoveInvalidParents()
        {
            var defaultReplacementParents = _objectDataCollection_baseGame.Where(x => x.Value.Parent != null && !_validParents.Contains(x.Value.Parent)).GroupBy(x => x.Value.Parent).ToDictionary(x => x.Key, x => x.Select(y => y.Key).FirstOrDefault());

            foreach (var objectData in _objectDataCollection_baseGame.Where(x => x.Value.Parent != null).ToList())
            {
                var oldParent = GetObjectDataForID_ReadOnly(objectData.Value.Parent);
                objectData.Value.Parent = null;
                if (oldParent != null)
                {
                    oldParent.Parent = null;
                    _objectDataCollection_baseGame[objectData.Key] = MergeObjectData(oldParent, objectData.Value);
                }
            }

            foreach (var objectData in _objectDataCollection_overrides.Where(x => x.Value.Parent != null && !_validParents.Contains(x.Value.Parent)).ToList())
            {
                var oldParentId = objectData.Value.Parent;
                var oldParent = GetObjectDataForID_ReadOnly(oldParentId);
                objectData.Value.Parent = null;
                if (oldParent != null)
                {
                    oldParent.Parent = null;
                    _objectDataCollection_overrides[objectData.Key] = MergeObjectData(oldParent, objectData.Value);
                }

                if (defaultReplacementParents.TryGetValue(oldParentId, out var newParent))
                {
                    _objectDataCollection_overrides[objectData.Key].Parent = newParent;
                }
            }
        }

        protected void FixMissingParents()
        {
            var missingParentObjects = _objectDataCollection_overrides.Where(x => x.Value.Parent == null && IsCustomObject(x.Key)).ToList();
            var validParentObjects = _validParents.ToDictionary(x => x, x => GetObjectDataForID_ReadOnly(x));
            foreach (var objectData in missingParentObjects)
            {
                var checkAllObjectDataTypes = objectData.Value.ObjectDataType == ObjectDataType.Unknown || objectData.Value.ObjectDataType == ObjectDataType.Mix_Of_Multiple;
                var newParent = validParentObjects.Where(x => checkAllObjectDataTypes || x.Value.ObjectDataType == objectData.Value.ObjectDataType).OrderByDescending(x => objectData.Value.GetPropertiesWithMatchingValues(x.Value).Count).FirstOrDefault();
                if (newParent.Key == null)
                {
                    continue;
                }

                objectData.Value.Parent = newParent.Key;
                var matchingProperties = objectData.Value.GetPropertiesWithMatchingValues(newParent.Value);
                foreach (var property in matchingProperties)
                {
                    objectData.Value.ClearValues(property);
                }
            }
        }

        public void ImportObjectDataCollectionFromFile(string fileName)
        {
            var parsedObjectDataCollection = ParseObjectDataCollectionFromFile(fileName);
            _objectDataCollection_overrides = MergeObjectDataCollections(_objectDataCollection_overrides, parsedObjectDataCollection);
        }

        protected Dictionary<FourCC, ObjectData> ParseObjectDataCollectionFromFile(string fileName)
        {
            var notFormatted = ParseObjectDataCollectionFromFile_Internal_NoReformatting(fileName);
            notFormatted = RemoveBlankValues(notFormatted);
            var result = ConvertSLKAndTXTPropertyNamesToFourCC(notFormatted);
            //result = UnquoteValuesAndSeparateCSVs(result);
            return result;
        }

        protected Dictionary<string, GenericObjectData<string>> ParseObjectDataCollectionFromFile_Internal_NoReformatting(string fileName)
        {
            if (Path.GetExtension(fileName).Equals(".txt", StringComparison.InvariantCultureIgnoreCase))
            {
                return ParseObjectDataCollectionFromTxtFile_Internal_NoReformatting(fileName);
            }
            else if (Path.GetExtension(fileName).Equals(".slk", StringComparison.InvariantCultureIgnoreCase))
            {
                return ParseObjectDataCollectionFromSLKFile_Internal_NoReformatting(fileName);
            }

            DebugSettings.Warn("Invalid file format");
            return new Dictionary<string, GenericObjectData<string>>();
        }

        protected Dictionary<string, GenericObjectData<string>> ParseObjectDataCollectionFromTxtFile_Internal_NoReformatting(string fileName)
        {
            var result = new Dictionary<string, GenericObjectData<string>>();
            try
            {
                var objectDataType = GetObjectDataTypeByFileName(fileName);
                if (objectDataType == ObjectDataType.Unknown)
                {
                    return result;
                }

                var parser = new FileIniDataParser(new IniParser.Parser.IniDataParser(new IniParserConfiguration() { SkipInvalidLines = true, AllowDuplicateKeys = true, AllowDuplicateSections = true, AllowKeysWithoutSection = true }));
                var ini = parser.ReadFile(fileName, Encoding.UTF8);
                ini.Configuration.AssigmentSpacer = "";
                foreach (var objectEntry in ini.Sections)
                {
                    var objectData = new GenericObjectData<string>(objectDataType, StringComparer.InvariantCultureIgnoreCase);
                    foreach (var propertyId in objectEntry.Keys)
                    {
                        objectData.SetValue(propertyId.KeyName, propertyId.Value);
                    }
                    result[objectEntry.SectionName] = objectData;
                }
            }
            catch (Exception e)
            {
                DebugSettings.Warn(e.Message);
            }

            return result;
        }

        protected Dictionary<string, GenericObjectData<string>> ParseObjectDataCollectionFromSLKFile_Internal_NoReformatting(string fileName)
        {
            var result = new Dictionary<string, GenericObjectData<string>>();
            try
            {
                var objectDataType = GetObjectDataTypeByFileName(fileName);
                if (objectDataType == ObjectDataType.Unknown)
                {
                    return result;
                }

                var slkData = SLKParser.Parse(fileName);
                var maxX = slkData.Select(x => x.Key.x).Max();
                var maxY = slkData.Select(x => x.Key.y).Max();

                var columnNames = new string[maxX + 1];
                for (var x = 0; x <= maxX; x++)
                {
                    columnNames[x] = slkData.GetValueOrDefault((x, 0))?.ToString() ?? $"{Path.GetFileName(fileName)}_{x}";
                }

                var objectIdColumnName = columnNames[0];
                for (var y = 1; y <= maxY; y++)
                {
                    try
                    {
                        var objectId = slkData.GetValueOrDefault((0, y))?.ToString();
                        if (string.IsNullOrWhiteSpace(objectId))
                        {
                            continue; // invalid row
                        }
                        if (!result.TryGetValue(objectId, out var objectData))
                        {
                            objectData = new GenericObjectData<string>(objectDataType, StringComparer.InvariantCultureIgnoreCase);
                            result[objectId] = objectData;
                        }

                        for (var x = 0; x <= maxX; x++)
                        {
                            try
                            {
                                var value = slkData.GetValueOrDefault((x, y));
                                if (value != null)
                                {
                                    objectData.SetValue(columnNames[x], value);
                                }
                            }
                            catch (Exception e)
                            {
                                DebugSettings.Warn(e.Message);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        DebugSettings.Warn(e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                DebugSettings.Warn(e.Message);
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

        public static War3NetObjectDataCollectionWrapper GetEmptyObjectDataWrapper(ObjectDataType type, ObjectDataFormatVersion objectDataFormatVersion = ObjectDataFormatVersion.v3)
        {
            switch (type)
            {
                case ObjectDataType.Ability:
                    return new War3NetObjectDataCollectionWrapper(new AbilityObjectData(objectDataFormatVersion));
                case ObjectDataType.Buff:
                    return new War3NetObjectDataCollectionWrapper(new BuffObjectData(objectDataFormatVersion));
                case ObjectDataType.Destructable:
                    return new War3NetObjectDataCollectionWrapper(new DestructableObjectData(objectDataFormatVersion));
                case ObjectDataType.Doodad:
                    return new War3NetObjectDataCollectionWrapper(new DoodadObjectData(objectDataFormatVersion));
                case ObjectDataType.Item:
                    return new War3NetObjectDataCollectionWrapper(new ItemObjectData(objectDataFormatVersion));
                case ObjectDataType.Unit:
                    return new War3NetObjectDataCollectionWrapper(new UnitObjectData(objectDataFormatVersion));
                case ObjectDataType.Upgrade:
                    return new War3NetObjectDataCollectionWrapper(new UpgradeObjectData(objectDataFormatVersion));
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

        public void SetWar3NetObjectFiles(Map map, bool skipOverridesMatchingParentValue = true, ObjectDataFormatVersion objectDataFormatVersion = ObjectDataFormatVersion.v3)
        {
            var allObjectDataCollections = ToWar3NetObjectDataCollectionFormat(skipOverridesMatchingParentValue, objectDataFormatVersion);
            foreach (var objectDataCollection in allObjectDataCollections)
            {
                SetObjectDataCollection(map, objectDataCollection);
            }
        }

        protected List<War3NetSkinnableObjectDataWrapper> ToWar3NetObjectDataCollectionFormat(bool skipOverridesMatchingParentValue = true, ObjectDataFormatVersion objectDataFormatVersion = ObjectDataFormatVersion.v3)
        {
            var metadataPerObjectDataType = _objectMetaDataCollection_baseGame.GroupBy(x => x.Value.ObjectDataType).ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Key, y => y.Value));

            var result = new List<War3NetSkinnableObjectDataWrapper>();
            foreach (ObjectDataType type in Enum.GetValues(typeof(ObjectDataType)))
            {
                var metaDataAllProperties = metadataPerObjectDataType.GetValueOrDefault(type);
                if (type == ObjectDataType.GameplayConstants)
                {
                    continue; // todo: not supported by War3Net yet
                }
                if (type == ObjectDataType.Unknown || type == ObjectDataType.Mix_Of_Multiple)
                {
                    DebugSettings.Warn("Trying to convert invalid ObjectData format to War3Net");
                    continue;
                }

                var convertedRecords = new List<War3NetObjectModificationWrapper>();

                foreach (var objectFourCC in _objectDataCollection_overrides.Where(x => x.Value.ObjectDataType == type).Select(x => x.Key))
                {
                    var objectData = GetObjectDataForID_ReadOnly(objectFourCC); // NOTE: can't read from _objectDataCollection_overrides because we also need to set the inherited properties
                    var parentBaseGameObjectData = _objectDataCollection_baseGame.GetValueOrDefault(string.IsNullOrWhiteSpace(objectData.Parent) ? objectFourCC : objectData.Parent);

                    var dataModifications = new List<War3NetObjectDataModificationWrapper>();
                    foreach (var propertyName in objectData.GetPropertyNames().OrderBy(x => _perLevelAttributeMaxLevelsPropertyIDs.Contains(x) ? 0 : 1))
                    {
                        if (skipOverridesMatchingParentValue && parentBaseGameObjectData != null)
                        {
                            var skip = true;
                            var maxLevels = Math.Max(objectData.GetLevelCount(propertyName), parentBaseGameObjectData.GetLevelCount(propertyName));
                            for (var level = 1; level <= maxLevels; level++)
                            {
                                var baseGameValue = parentBaseGameObjectData.GetValue(propertyName, level);
                                if (baseGameValue == null)
                                {
                                    if (level == 1)
                                    {
                                        skip = false;
                                    }

                                    break;
                                }

                                if (!baseGameValue.Equals(objectData.GetValue(propertyName, level)))
                                {
                                    skip = false;
                                    break;
                                }
                            }

                            if (skip)
                            {
                                continue;
                            }
                        }

                        int levelCount = objectData.GetLevelCount(propertyName);
                        for (var level = 1; level <= levelCount; level++)
                        {
                            var value = objectData.GetValue(propertyName, level);
                            if (value == null)
                            {
                                continue;
                            }

                            var dataModificationWrapper = GetEmptyObjectDataModificationWrapper(type);
                            if (_objectMetaDataCollection_baseGame.TryGetValue(propertyName, out var metaData) && Enum.TryParse<ObjectEditorDataType>(metaData.GetValue("type")?.ToString(), true, out var enumType))
                            {
                                dataModificationWrapper.SetValue(value, enumType.ConvertToObjectDataType());
                            }
                            else
                            {
                                dataModificationWrapper.SetValue(value?.ToString() ?? "", War3Net.Build.Object.ObjectDataType.String);
                            }

                            dataModificationWrapper.Id = ((string)propertyName).FromFourCCToInt().InvertEndianness();
                            var defaultLevel = _perLevelPropertyIDs.GetValueOrDefault(type)?.Contains(propertyName) == true ? 1 : 0;
                            dataModificationWrapper.Level = levelCount > 1 ? level : defaultLevel;
                            if (metaDataAllProperties?.TryGetValue(propertyName, out var propertyMetaData) == true && int.TryParse(propertyMetaData.GetValue("data")?.ToString(), out var pointer))
                            {
                                dataModificationWrapper.Pointer = pointer;
                            }
                            dataModifications.Add(dataModificationWrapper);
                        }
                    }

                    var hasParent = !string.IsNullOrWhiteSpace(objectData.Parent);
                    if (dataModifications.Any() || !IsBaseGameObject(objectFourCC))
                    {
                        var convertedObjectData = GetEmptyObjectModificationWrapper(type);
                        convertedObjectData.OldId = hasParent ? objectData.Parent.ToObjectID() : objectFourCC.ToObjectID();
                        convertedObjectData.NewId = hasParent ? objectFourCC.ToObjectID() : 0;

                        convertedObjectData.Modifications = dataModifications.DistinctBy(x => (x.Id, x.Level)).ToList().AsReadOnly();
                        convertedRecords.Add(convertedObjectData);
                    }
                }

                var newObjectDataCollection = GetEmptyObjectDataWrapper(type, objectDataFormatVersion);
                newObjectDataCollection.OriginalOverrides = convertedRecords.Where(x => _objectDataCollection_baseGame.ContainsKey(x.ToString().Substring(0, 4))).ToList().AsReadOnly();
                newObjectDataCollection.CustomOverrides = convertedRecords.Except(newObjectDataCollection.OriginalOverrides).ToList().AsReadOnly();
                result.Add(new War3NetSkinnableObjectDataWrapper(newObjectDataCollection, GetEmptyObjectDataWrapper(type, objectDataFormatVersion)));
            }

            FixInvalidDataTypes(result);
            return result;
        }

        protected void FixInvalidDataTypes(List<War3NetSkinnableObjectDataWrapper> listOfObjectDataCollection)
        {
            foreach (var objectDataCollection in listOfObjectDataCollection)
            {
                foreach (var objectData in objectDataCollection.OriginalOverrides.Concat(objectDataCollection.CustomOverrides))
                {
                    foreach (var modification in objectData.Modifications)
                    {
                        modification.SetValue(modification.Value, modification.Type);

                        var isValid = true;
                        if (modification.Type == War3Net.Build.Object.ObjectDataType.Int && modification.Value.GetType() != typeof(int))
                        {
                            isValid = false;
                        }
                        else if (modification.Type == War3Net.Build.Object.ObjectDataType.Real && modification.Value.GetType() != typeof(float))
                        {
                            isValid = false;
                        }
                        else if (modification.Type == War3Net.Build.Object.ObjectDataType.Unreal && modification.Value.GetType() != typeof(float))
                        {
                            isValid = false;
                        }
                        else if (modification.Type == War3Net.Build.Object.ObjectDataType.Char && modification.Value.GetType() != typeof(char))
                        {
                            isValid = false;
                        }
                        else if (modification.Type == War3Net.Build.Object.ObjectDataType.String && modification.Value.GetType() != typeof(string))
                        {
                            isValid = false;
                        }

                        if (!isValid)
                        {
                            modification.SetValue(modification.Value, War3Net.Build.Object.ObjectDataType.String);
                        }
                    }
                }
            }
        }

        protected static Dictionary<FourCC, ObjectData> FromWar3NetObjectDataCollectionFormat(List<War3NetSkinnableObjectDataWrapper> objectDataCollection_Overrides)
        {
            var typePerFourCC = new Dictionary<FourCC, ObjectDataType>();
            var allObjectModifications = new List<War3NetObjectModificationWrapper>();

            foreach (var objectData in objectDataCollection_Overrides)
            {
                var newObjectModifications = objectData.CoreData.OriginalOverrides.Concat(objectData.CoreData.CustomOverrides).Concat(objectData.SkinData.OriginalOverrides).Concat(objectData.SkinData.CustomOverrides).ToList();
                foreach (var objectModification in newObjectModifications)
                {
                    typePerFourCC[objectModification.ToString()] = objectData.ObjectDataType;
                }

                allObjectModifications.AddRange(newObjectModifications);
            }

            var result = new Dictionary<FourCC, ObjectData>();
            var groupedAndSortedObjectModifications = allObjectModifications.GroupBy(x => x.ToString()).ToDictionary(x => x.Key, x => x.SelectMany(y => y.Modifications).OrderBy(y => y.ToString()).ThenBy(y => y.Level).ToList());
            foreach (var objectData in groupedAndSortedObjectModifications)
            {
                var objectFourCC = objectData.Key;
                result[objectFourCC] = new ObjectData(typePerFourCC[objectFourCC]);
                if (objectFourCC.Length == 9 && objectFourCC[4] == ':')
                {
                    result[objectFourCC].Parent = objectFourCC.Substring(5);
                }

                foreach (var objectModification in objectData.Value)
                {
                    var propertyFourCC = objectModification.ToString();
                    result[objectFourCC].AddNextLevelValue(propertyFourCC, objectModification.Value);
                }
            }

            return result;
        }

        public void ImportObjectDataCollectionFromMap(Map map, bool replaceTriggerStrings = false)
        {
            var objectDataCollection = FromWar3NetObjectDataCollectionFormat(map.GetObjectDataCollection_War3Net().Values.ToList());
            _objectDataCollection_overrides = MergeObjectDataCollections(_objectDataCollection_overrides, objectDataCollection);

            if (replaceTriggerStrings)
            {
                const string TRIGSTR_ = "TRIGSTR_";
                foreach (var objectData in _objectDataCollection_overrides)
                {
                    foreach (var property in objectData.Value.GetPropertyNames())
                    {
                        var levelCount = objectData.Value.GetLevelCount(property);
                        for (var level = 1; level <= levelCount; level++)
                        {
                            var value = (objectData.Value.GetValue(property, level)?.ToString() ?? "").Trim();
                            if (value.StartsWith(TRIGSTR_, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (int.TryParse(value.Substring(TRIGSTR_.Length), out var key))
                                {
                                    value = map.TriggerStrings.Strings.FirstOrDefault(x => x.Key == key)?.Value ?? value;
                                    objectData.Value.SetValue(property, value, level);
                                }
                            }
                        }
                    }
                }
            }
        }

        public HashSet<string> ExportAllStringValues(HashSet<FourCC> propertyFilter = null, bool includeBaseGameData = true)
        {
            var result = new HashSet<string>(_objectDataCollection_overrides.Values.SelectMany(x => x.ExportAllStringValues(propertyFilter)));

            if (includeBaseGameData)
            {
                result.AddRange(_objectDataCollection_baseGame.Values.SelectMany(x => x.ExportAllStringValues(propertyFilter)));
            }

            return result;
        }

        protected void SetObjectDataCollection(Map map, War3NetSkinnableObjectDataWrapper objectDataCollection)
        {
            switch (objectDataCollection.ObjectDataType)
            {
                case ObjectDataType.Ability:
                    map.AbilityObjectData = new AbilityObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.AbilityObjectData).OriginalOverrides = objectDataCollection.CoreData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.AbilityObjectData).CustomOverrides = objectDataCollection.CoreData.CustomOverrides;

                    map.AbilitySkinObjectData = new AbilityObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.AbilitySkinObjectData).OriginalOverrides = objectDataCollection.SkinData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.AbilitySkinObjectData).CustomOverrides = objectDataCollection.SkinData.CustomOverrides;
                    break;
                case ObjectDataType.Buff:
                    map.BuffObjectData = new BuffObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.BuffObjectData).OriginalOverrides = objectDataCollection.CoreData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.BuffObjectData).CustomOverrides = objectDataCollection.CoreData.CustomOverrides;

                    map.BuffSkinObjectData = new BuffObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.BuffSkinObjectData).OriginalOverrides = objectDataCollection.SkinData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.BuffSkinObjectData).CustomOverrides = objectDataCollection.SkinData.CustomOverrides;
                    break;
                case ObjectDataType.Destructable:
                    map.DestructableObjectData = new DestructableObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.DestructableObjectData).OriginalOverrides = objectDataCollection.CoreData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.DestructableObjectData).CustomOverrides = objectDataCollection.CoreData.CustomOverrides;

                    map.DestructableSkinObjectData = new DestructableObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.DestructableSkinObjectData).OriginalOverrides = objectDataCollection.SkinData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.DestructableSkinObjectData).CustomOverrides = objectDataCollection.SkinData.CustomOverrides;
                    break;
                case ObjectDataType.Doodad:
                    map.DoodadObjectData = new DoodadObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.DoodadObjectData).OriginalOverrides = objectDataCollection.CoreData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.DoodadObjectData).CustomOverrides = objectDataCollection.CoreData.CustomOverrides;

                    map.DoodadSkinObjectData = new DoodadObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.DoodadSkinObjectData).OriginalOverrides = objectDataCollection.SkinData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.DoodadSkinObjectData).CustomOverrides = objectDataCollection.SkinData.CustomOverrides;
                    break;
                case ObjectDataType.Item:
                    map.ItemObjectData = new ItemObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.ItemObjectData).OriginalOverrides = objectDataCollection.CoreData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.ItemObjectData).CustomOverrides = objectDataCollection.CoreData.CustomOverrides;

                    map.ItemSkinObjectData = new ItemObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.ItemSkinObjectData).OriginalOverrides = objectDataCollection.SkinData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.ItemSkinObjectData).CustomOverrides = objectDataCollection.SkinData.CustomOverrides;
                    break;
                case ObjectDataType.Unit:
                    map.UnitObjectData = new UnitObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.UnitObjectData).OriginalOverrides = objectDataCollection.CoreData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.UnitObjectData).CustomOverrides = objectDataCollection.CoreData.CustomOverrides;

                    map.UnitSkinObjectData = new UnitObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.UnitSkinObjectData).OriginalOverrides = objectDataCollection.SkinData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.UnitSkinObjectData).CustomOverrides = objectDataCollection.SkinData.CustomOverrides;
                    break;
                case ObjectDataType.Upgrade:
                    map.UpgradeObjectData = new UpgradeObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.UpgradeObjectData).OriginalOverrides = objectDataCollection.CoreData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.UpgradeObjectData).CustomOverrides = objectDataCollection.CoreData.CustomOverrides;

                    map.UpgradeSkinObjectData = new UpgradeObjectData(objectDataCollection.FormatVersion);
                    new War3NetObjectDataCollectionWrapper(map.UpgradeSkinObjectData).OriginalOverrides = objectDataCollection.SkinData.OriginalOverrides;
                    new War3NetObjectDataCollectionWrapper(map.UpgradeSkinObjectData).CustomOverrides = objectDataCollection.SkinData.CustomOverrides;
                    break;
                case ObjectDataType.GameplayConstants:
                    break; // todo: not supported by War3Net yet
                case ObjectDataType.Unknown:
                    DebugSettings.Warn("Trying to convert unknown ObjectData format to War3Net");
                    break;
                case ObjectDataType.Mix_Of_Multiple:
                    DebugSettings.Warn("Need to split ObjectData of mixed types before converting to War3Net");
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}