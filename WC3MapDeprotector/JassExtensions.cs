using System.Text;

namespace WC3MapDeprotector
{
    public static class JassExtensions
    {
        public static int FromFourCCToInt(this string code)
        {
            if (code.Length != 4)
            {
                return 0;
            }

            var result = 0;
            for (var i = 0; i < 4; i++)
            {
                var byteValue = (byte)code[i];
                var bytesLeft = 4 - i - 1;
                result |= byteValue << (bytesLeft * 8);
                if (byteValue >= 0x80 && bytesLeft < 3)
                {
                    result -= 1 << ((bytesLeft + 1) * 8);
                }
            }

            return result;
        }

        public static string ToFourCC(this int objectId)
        {
            var bytes = new byte[4] { 0, 0, 0, 0 };
            for (var i = 3; i >= 0; i--)
            {
                var byteValue = (byte)(objectId % 256);
                bytes[i] += byteValue;
                if (bytes[i] >= 0x80 && i > 0)
                {
                    bytes[i - 1]++;
                }
                objectId /= 256;
            }

            var result = new StringBuilder();
            foreach (var byteValue in bytes)
            {
                result.Append((char)(byteValue));
            }
            return result.ToString();
        }
    }
}