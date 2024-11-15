using FlaUI.Core.AutomationElements;
using FlaUI.UIA2;
using System.Diagnostics;

namespace WC3MapDeprotector
{
    public class WorldEditor : IDisposable
    {
        //NOTE: Can use FlaUI.Core for UI Automation if need to execute things that can't be done with hotkeys
        protected Process Process;
        public string MapFileName { get; protected set; }

        public WorldEditor(string mapFileName)
        {
            MapFileName = mapFileName;
            Process = Utils.ExecuteCommand(UserSettings.WorldEditExePath, $"-launch -loadfile \"{mapFileName}\" -hd 0");
        }

        public static Process GetRunningInstanceOfEditor()
        {
            return Process.GetProcesses().Where(x =>
            {
                try
                {
                    return UserSettings.WorldEditExePath.Equals(x.MainModule.FileName, StringComparison.InvariantCultureIgnoreCase);
                }
                catch { return false; }
            }).FirstOrDefault();
        }

        public DateTime GetLastWriteTime()
        {
            var fileInfo = new FileInfo(MapFileName);
            return fileInfo.LastWriteTime;
        }

        public void SaveMap()
        {
            var originalWriteTime = GetLastWriteTime();
            const int sleepTime = 1000;
            var maxWait = 1000 * 30;

            do
            {
                Win32Api.SetForegroundWindow(Process.MainWindowHandle);
                SendKeys.SendWait("^s");
                Thread.Sleep(sleepTime);
                maxWait -= sleepTime;
                if (maxWait < 0)
                {
                    throw new Exception("Unable to save file in WorldEditor");
                }
            } while (GetLastWriteTime() == originalWriteTime);
        }

        public void Dispose()
        {
            Process.Kill();
        }
    }
}