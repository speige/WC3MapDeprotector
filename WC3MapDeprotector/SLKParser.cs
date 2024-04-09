using War3Net.IO.Slk;

namespace WC3MapDeprotector
{
    public enum SLKType {
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
        public Dictionary<string, object> Data { get; protected set; } = new Dictionary<string, object>();

        public SLKType SLKType
        {
            get
            {
                var slkFilesFoundIn = Data.Keys.Where(x => x.EndsWith(".slk", StringComparison.InvariantCultureIgnoreCase)).ToList();
                //NOTE: order is important to avoid misclassifying AbilityBuffData as AbilityData
                if (slkFilesFoundIn.Any(x => x.Contains("doodad", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Doodad;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("destructable", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Destructable;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("item", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Item;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("upgrade", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Upgrade;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("buff", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Buff;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("unit", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Unit;
                }
                if (slkFilesFoundIn.Any(x => x.Contains("ability", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return SLKType.Ability;
                }

                throw new NotImplementedException();
            }
        }
    }

    public class SLKParser
    {
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
                                slkObject = new SLKObject();
                                result[objectId] = slkObject;
                            }

                            slkObject.Data[Path.GetFileName(fileName)] = true;

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
    }
}