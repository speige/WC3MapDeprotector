using System.Security.Cryptography;
using System.Text;

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

        public static string ToString_NoEncoding(this byte[] byteArray)
        {
            //ISO-8859-1 is a 1-to-1 match of byte to char. Important for reading script files to avoid corrupting non-ascii or international characters.
            return Encoding.GetEncoding("ISO-8859-1").GetString(byteArray);
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