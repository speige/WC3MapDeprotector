using System.Diagnostics;
using System.Reflection;
using War3Net.IO.Mpq;
using System.Runtime.CompilerServices;

namespace WC3MapDeprotector
{
    public static class MPQHashing
    {
        private static uint[] Buffer;
        const int leftOffset = 0x100;
        const int rightOffset = 0x200;
        static MPQHashing()
        {
            var StormBuffer = typeof(MpqArchive).Assembly.GetType("War3Net.IO.Mpq.StormBuffer").GetField("_stormBuffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var StormBufferValue = StormBuffer.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance).GetValue(StormBuffer);
            Buffer = (uint[])StormBufferValue.GetType().GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(StormBufferValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong FinalizeHash(uint leftHash, uint rightHash)
        {
            return leftHash | ((ulong)rightHash << 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartHash(out uint leftHash, out uint leftSeed, out uint rightHash, out uint rightSeed)
        {
            leftHash = 0x7fed7fed;
            leftSeed = 0xeeeeeeee;
            rightHash = 0x7fed7fed;
            rightSeed = 0xeeeeeeee;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddCharToHash(uint leftHash, uint leftSeed, uint rightHash, uint rightSeed, char character, out uint newLeftHash, out uint newLeftSeed, out uint newRightHash, out uint newRightSeed)
        {
            var val = (int)character;
            newLeftHash = Buffer[leftOffset + val] ^ (leftHash + leftSeed);
            newLeftSeed = (uint)val + newLeftHash + leftSeed + (leftSeed << 5) + 3;
            newRightHash = Buffer[rightOffset + val] ^ (rightHash + rightSeed);
            newRightSeed = (uint)val + newRightHash + rightSeed + (rightSeed << 5) + 3;
        }

        public static ulong HashFileName(string fileName)
        {
            fileName = fileName.ToUpper();
            MPQHashing.StartHash(out var leftHash, out var leftSeed, out var rightHash, out var rightSeed);
            foreach (var character in fileName)
            {
                MPQHashing.AddCharToHash(leftHash, leftSeed, rightHash, rightSeed, character, out leftHash, out leftSeed, out rightHash, out rightSeed);
            }
            return MPQHashing.FinalizeHash(leftHash, rightHash);
        }
    }
}