using War3Net.CodeAnalysis.Decompilers;
using War3Net.CodeAnalysis.Jass.Extensions;

namespace WC3MapDeprotector
{
    public class FourCC
    {
        public string Value { get; init; }

        public FourCC(string fourCC)
        {
            if (fourCC == null)
            {
                DebugSettings.Warn("empty FourCC");
            }

            fourCC ??= "";
            while (fourCC.Length < 4)
            {
                fourCC += '\0';
            }

            if (fourCC.Length == 4)
            {
                Value = fourCC;
            }
            else if (fourCC.Length == 9 && fourCC[4] == ':')
            {
                Value = fourCC.Substring(0, 4);
            }
            else
            {
                DebugSettings.Warn("Invalid FourCC syntax");
            }
        }

        public static FourCC FromJassRawCode(int jassRawCode)
        {
            return new FourCC(jassRawCode.ToFourCC());
        }

        public static FourCC FromObjectID(int objectID)
        {
            return new FourCC(objectID.InvertEndianness().ToFourCC());
        }

        public int ToObjectID()
        {
            return ToJassRawCode().InvertEndianness();
        }

        public int ToJassRawCode()
        {
            return Value?.FromFourCCToInt() ?? 0;
        }

        public override string ToString()
        {
            return Value;
        }

        public override bool Equals(object obj)
        {
            return Value == (obj as FourCC)?.Value;
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }

        public static implicit operator string(FourCC fourCC)
        {
            return fourCC?.Value;
        }

        public static implicit operator FourCC(string value)
        {
            return new FourCC(value);
        }
    }
}