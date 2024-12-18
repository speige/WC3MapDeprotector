namespace WC3MapDeprotector
{
    public abstract class FuzzyHash
    {
        const double _dataLengthScalingFactor = 10.0; // Increase this value for a sharper drop

        public byte HashSize_Bits { get; }
        protected byte[] _hash { get; }
        protected int _moduloValue { get; }
        protected int OriginalDataLength { get; }

        protected FuzzyHash(byte bits, byte[] fileData)
        {
            HashSize_Bits = bits;
            _moduloValue = 1 << HashSize_Bits; // 2^Bits
            OriginalDataLength = fileData.Length;
            _hash = new byte[_moduloValue];
            foreach (byte b in fileData)
            {
                _hash[b] = (byte)((_hash[b] + 1) % (_moduloValue - 1));
            }
        }

        public double CalcMatchPercentage(FuzzyHash otherHash)
        {
            if (HashSize_Bits != otherHash.HashSize_Bits)
            {
                throw new ArgumentException("Hashes must have the same bits configuration.");
            }

            double totalDelta = 0;

            for (int i = 0; i < _hash.Length; i++)
            {
                totalDelta += Math.Abs(_hash[i] - otherHash._hash[i]) / (double)(_moduloValue - 1);
            }

            double dataLengthRatio = Math.Exp(-_dataLengthScalingFactor * Math.Abs(OriginalDataLength - otherHash.OriginalDataLength) / (double)Math.Max(OriginalDataLength, otherHash.OriginalDataLength));

            double hashSimilarity = 100 - (totalDelta / _hash.Length) * 100;

            return (hashSimilarity + dataLengthRatio * 100) / 2;
        }
    }

    public class FuzzyHash_8 : FuzzyHash
    {
        protected FuzzyHash_8(byte[] fileData) : base(8, fileData) { }

        public static FuzzyHash_8 Compute(byte[] fileData)
        {
            return new FuzzyHash_8(fileData);
        }
    }
}