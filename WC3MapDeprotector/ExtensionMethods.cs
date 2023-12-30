using System.Security.Cryptography;

namespace WC3MapDeprotector
{
    public static class ExtensionMethods
    {
        public static string CalculateMD5(this Stream stream)
        {
            using (var md5 = MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
            }
        }
    }
}