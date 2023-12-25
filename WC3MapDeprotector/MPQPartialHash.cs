using System.Reflection;
using War3Net.IO.Mpq;
using System.Runtime.CompilerServices;
using System.Security.Policy;

namespace WC3MapDeprotector
{
    //NOTE: Using Struct for performance reasons.
    //Struct are value types, not reference types.
    //If you call a method it will mutate the original value, so if you pre-calculate a hash for the prefix of a file (directory name) and then re-use that for multiple filenames with the same prefix, you must copy into a new variable before modifying to "clone" it so the original value stays in tact (whereas reference types don't clone when putting into a variable, they must be re-constructed or call a Clone method)

    //NOTE: PartialHash is faster, but a match is only a 99% chance the file really exists. For bruteForce or "guessing" algorithms, use PartialHash and only verify 2nd half of Hash if 1st returns success
    public struct MPQPartialHash
    {
        public const int LEFT_OFFSET = 0x200;
        public const int RIGHT_OFFSET = 0x100;

        private static uint[] _buffer;
        static MPQPartialHash()
        {
            var StormBuffer = typeof(MpqArchive).Assembly.GetType("War3Net.IO.Mpq.StormBuffer").GetField("_stormBuffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var StormBufferValue = StormBuffer.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance).GetValue(StormBuffer);
            _buffer = (uint[])StormBufferValue.GetType().GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(StormBufferValue);
        }

        private readonly int _offset;
        private uint _hash = 0x7fed7fed;
        private uint _seed = 0xeeeeeeee;

        public uint Value
        {
            get
            {
                return _hash;
            }
        }

        //NOTE: Won't compile without a parameterless constructor, but we don't want it
        public MPQPartialHash()
        {
            DebugSettings.Warn("MUST SET HASH OFFSET!");
            throw new NotImplementedException();
        }

        public MPQPartialHash(int offset)
        {
            _offset = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddChar(char character)
        {
            AddChar((byte)character);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddChar(byte character)
        {
            _hash = _buffer[_offset + character] ^ (_hash + _seed);
            _seed = character + _hash + _seed + (_seed << 5) + 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddChar(char character)
        {
            return TryAddChar((byte)character);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddChar(byte character)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Calculate(string text, int offset)
        {
            var result = new MPQPartialHash(offset);
            result.AddString(text);
            return result._hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryCalculate(string text, int offset, out uint hash)
        {
            hash = 0;
            try
            {
                hash = Calculate(text, offset);
                return true;
            }
            catch { }

            return false;
        }
    }
}