using System.Diagnostics;
using System.Reflection;

namespace WC3MapDeprotector
{
    public static class Utils
    {
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

        public static string ReadFile_NoEncoding(string fileName)
        {
            return File.ReadAllBytes(fileName).ToString_NoEncoding();
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