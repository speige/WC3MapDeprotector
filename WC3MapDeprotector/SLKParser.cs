using System.Globalization;

namespace WC3MapDeprotector
{
    public static class SLKParser
    {
        public static Dictionary<(int x, int y), object> Parse(string filePath)
        {
            var result = new Dictionary<(int x, int y), object>();

            try
            {
                var lines = File.ReadAllLines(filePath);

                int? maxX = null;
                int? maxY = null;

                var bLine = lines.FirstOrDefault(x => x.StartsWith("B;", StringComparison.InvariantCultureIgnoreCase));
                if (!string.IsNullOrWhiteSpace(bLine))
                {
                    var parts = bLine.Split(';');
                    foreach (var part in parts)
                    {
                        if (part.StartsWith("X", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (int.TryParse(part.Substring(1), out int x))
                            {
                                maxX = x;
                            }
                        }
                        else if (part.StartsWith("Y", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (int.TryParse(part.Substring(1), out int y))
                            {
                                maxY = y;
                            }
                        }
                        else if (part.StartsWith("D", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var dContent = part.Substring(1).Trim();
                            var dParts = dContent.Split(' ');
                            if (dParts.Length == 2)
                            {
                                if (int.TryParse(dParts[0], out int y))
                                {
                                    maxY ??= y;
                                }
                                if (int.TryParse(dParts[1], out int x))
                                {
                                    maxX ??= x;
                                }
                            }
                            else if (dParts.Length == 4)
                            {
                                int.TryParse(dParts[0], out int startY);
                                int.TryParse(dParts[1], out int startX);
                                if (int.TryParse(dParts[2], out int y))
                                {
                                    maxY ??= (y - startY);
                                }
                                if (int.TryParse(dParts[3], out int x))
                                {
                                    maxX ??= (x - startX);
                                }
                            }
                        }
                    }
                }

                int nextX = 0;
                int nextY = 0;

                foreach (var line in lines)
                {
                    var isCell = line.StartsWith("C;", StringComparison.InvariantCultureIgnoreCase);
                    var isFormatting = line.StartsWith("F;", StringComparison.InvariantCultureIgnoreCase);
                    if (isCell || isFormatting)
                    {
                        int? x = null;
                        int? y = null;
                        object value = null;

                        var parts = line.Split(';');
                        foreach (var part in parts)
                        {
                            if (part.StartsWith("X", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (int.TryParse(part.Substring(1), out int parsedX))
                                {
                                    x = parsedX - 1;
                                }
                            }
                            else if (part.StartsWith("Y", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (int.TryParse(part.Substring(1), out int parsedY))
                                {
                                    y = parsedY - 1;
                                }
                            }
                            else if (part.StartsWith("K", StringComparison.InvariantCultureIgnoreCase))
                            {
                                value = ParseValueString(part.Substring(1));
                            }
                        }

                        if (isFormatting && x == null && y == null)
                        {
                            continue;
                        }

                        x ??= nextX;
                        y ??= nextY;

                        nextX = x.Value;
                        nextY = y.Value;

                        if (isCell)
                        {
                            nextX++;
                        }

                        if (maxX.HasValue && nextX >= maxX)
                        {
                            nextX = 0;
                            nextY++;
                        }

                        if (value != null)
                        {
                            result[(x.Value, y.Value)] = value;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugSettings.Warn(e.Message);
            }

            return result;
        }

        private static object ParseValueString(string value)
        {
            if (value.StartsWith('"') && value.EndsWith('"'))
            {
                return value.Substring(1, value.Length - 2);
            }
            else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }
            else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            {
                return floatValue;
            }
            else if (string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (string.Equals(value, bool.FalseString, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (string.Equals(value, "#VALUE!", StringComparison.Ordinal) || string.Equals(value, "#REF!", StringComparison.Ordinal))
            {
                return 0;
            }
            else
            {
                throw new NotSupportedException($"Unable to parse value '{value}'. Can only parse strings, integers, floats, and booleans.");
            }
        }
    }
}
