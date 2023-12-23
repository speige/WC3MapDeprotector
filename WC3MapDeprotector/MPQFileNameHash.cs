using System.Reflection;
using War3Net.IO.Mpq;
using System.Runtime.CompilerServices;

namespace WC3MapDeprotector
{
    public struct MPQFileNameHash
    {
        private static uint[] Buffer;
        static MPQFileNameHash()
        {
            var StormBuffer = typeof(MpqArchive).Assembly.GetType("War3Net.IO.Mpq.StormBuffer").GetField("_stormBuffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var StormBufferValue = StormBuffer.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance).GetValue(StormBuffer);
            Buffer = (uint[])StormBufferValue.GetType().GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(StormBufferValue);
        }

        const int leftOffset = 0x100;
        const int rightOffset = 0x200;

        uint leftHash = 0x7fed7fed;
        uint rightHash = 0x7fed7fed;
        uint leftSeed = 0xeeeeeeee;
        uint rightSeed = 0xeeeeeeee;

        public MPQFileNameHash()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetValue()
        {
            return leftHash | ((ulong)rightHash << 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddChar(char character)
        {
            var val = (int)character;
            leftHash = Buffer[leftOffset + val] ^ (leftHash + leftSeed);
            leftSeed = (uint)val + leftHash + leftSeed + (leftSeed << 5) + 3;
            rightHash = Buffer[rightOffset + val] ^ (rightHash + rightSeed);
            rightSeed = (uint)val + rightHash + rightSeed + (rightSeed << 5) + 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddChar(char character)
        {
            try
            {
                AddChar(character);
                return true;
            }
            catch { }

            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddString(string text)
        {
            text = text.ToUpper();
            foreach (var character in text)
            {
                AddChar(character);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddString(string text)
        {
            try
            {
                AddString(text);
                return true;
            }
            catch { }

            return false;
        }

        public static bool TryCalculate(string text, out ulong hash)
        {
            hash = 0;

            var result = new MPQFileNameHash();
            if (!result.TryAddString(text))
            {
                return false;
            }

            hash = result.GetValue();
            return true;
        }
    }
}