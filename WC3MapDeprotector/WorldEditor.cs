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
            Process editorInstance = null;
            do
            {
                editorInstance?.Kill();
                editorInstance = WorldEditor.GetRunningInstanceOfEditor();
            } while (editorInstance != null);

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
            var sleepTime = (int)TimeSpan.FromSeconds(15).TotalMilliseconds;
            var maxWait = (int)TimeSpan.FromMinutes(15).TotalMilliseconds;
            var oneSecond = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;

            do
            {
                do
                {
                    Win32Api.SetForegroundWindow(Process.MainWindowHandle);
                    Thread.Sleep(oneSecond);
                    maxWait -= sleepTime;

                    if (maxWait < 0 || Process.HasExited)
                    {
                        throw new Exception("Unable to save file in WorldEditor");
                    }
                } while (Win32Api.GetForegroundWindow() != Process.MainWindowHandle);

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