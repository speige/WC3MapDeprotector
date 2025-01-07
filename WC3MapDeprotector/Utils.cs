using System.Diagnostics;
using System.Reflection;
using System.Text;
using War3Net.Common.Providers;

namespace WC3MapDeprotector
{
    public static class Utils
    {
        //NOTE: ISO-8859-1 is a 1-to-1 match of byte to char. Important for reading/writing script files to avoid corrupting non-ascii or international characters.
        public static readonly Encoding NO_ENCODING = Encoding.GetEncoding("ISO-8859-1");
        
        public static void SafeDeleteFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return;
            }

            try
            {
                File.Delete(fileName);
            }
            catch
            {
                // swallow errors
            }
        }

        public static string ExeFolderPath
        {
            get
            {
                return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            }
        }

        public static string ToString_NoEncoding(this byte[] byteArray)
        {            
            return NO_ENCODING.GetString(byteArray);
        }

        public static string ReadFile_NoEncoding(string fileName)
        {
            return File.ReadAllBytes(fileName).ToString_NoEncoding();
        }

        public static void WriteFile_NoEncoding(string fileName, string text)
        {
            File.WriteAllText(fileName, text, NO_ENCODING);
        }

        public static Process ExecuteCommand(string exePath, string arguments, ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal)
        {
            var process = new Process();
            process.StartInfo.FileName = exePath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.RedirectStandardInput = false;
            process.StartInfo.WindowStyle = windowStyle;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
            process.Start();
            return process;
        }

        public static void WaitForProcessToExit(Process process, CancellationToken cancellationToken = default)
        {
            while (!process.HasExited)
            {
                Thread.Sleep(1000);

                if (cancellationToken.IsCancellationRequested)
                {
                    process.Kill();
                }
            }
        }
    }
}