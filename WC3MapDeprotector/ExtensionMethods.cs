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

        public static string CalculateMD5(this byte[] byteArray)
        {
            using (var md5 = MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(byteArray)).Replace("-", "");
            }
        }

        public static string TrimStart(this string text, string trim)
        {
            if (text.StartsWith(trim))
            {
                return text.Substring(trim.Length);
            }

            return text;
        }

        public static string TrimEnd(this string text, string trim)
        {
            if (text.EndsWith(trim))
            {
                return text.Substring(0, text.Length - trim.Length);
            }

            return text;
        }
    }
}