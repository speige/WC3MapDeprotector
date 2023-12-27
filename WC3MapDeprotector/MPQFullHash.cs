using System.Runtime.CompilerServices;

namespace WC3MapDeprotector
{
    public struct MPQFullHash
    {
        public MPQFullHash()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Calculate(string text)
        {
            var left = MPQPartialHash.Calculate(text, MPQPartialHash.LEFT_OFFSET);
            var right = MPQPartialHash.Calculate(text, MPQPartialHash.RIGHT_OFFSET);
            return GetValue(left, right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryCalculate(string text, out ulong hash)
        {
            hash = 0;
            try
            {
                hash = Calculate(text);
                return true;
            }
            catch { }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetValue(uint leftHash, uint rightHash)
        {
            return ((ulong)leftHash << 32) | rightHash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Tuple<uint, uint> SplitValue(ulong fullHash)
        {
            return new Tuple<uint, uint>((uint)((fullHash & 0xFFFFFFFF00000000) >> 32), (uint)(fullHash & 0x00000000FFFFFFFF));
        }
    }
}