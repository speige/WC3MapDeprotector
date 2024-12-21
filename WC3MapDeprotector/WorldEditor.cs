using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA2;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WC3MapDeprotector
{
    public partial class WorldEditor : IDisposable
    {
        //NOTE: Can use FlaUI.Core for UI Automation if need to execute things that can't be done with hotkeys
        protected Process Process;
        public string MapFileName { get; protected set; }

        public WorldEditor()
        {
        }

        protected void SleepUntilTrue(Func<bool> predicate, int maxSleepMS, string errorMessageOnFailure, int sleepBetweenChecksMS = 1000)
        {
            while (!predicate())
            {
                if (Process.HasExited || maxSleepMS < 0)
                {
                    throw new Exception(errorMessageOnFailure);
                }

                Thread.Sleep(sleepBetweenChecksMS);
                maxSleepMS -= sleepBetweenChecksMS;
            }
        }

        public void LoadMapFile(string mapFileName)
        {
            MapFileName = mapFileName;
            Process editorInstance = null;
            do
            {
                editorInstance?.Kill();
                editorInstance = WorldEditor.GetRunningInstanceOfEditor();
            } while (editorInstance != null);

            Process = Utils.ExecuteCommand(UserSettings.WorldEditExePath, $"-launch -nowfpause -loadfile \"{mapFileName}\" -hd 0", ProcessWindowStyle.Maximized);

            SleepUntilTrue(() =>
            {
                RefreshProcess();
                if (!string.IsNullOrWhiteSpace(Process.MainWindowTitle))
                {
                    return true;
                }

                return false;
            }, (int)TimeSpan.FromMinutes(1).TotalMilliseconds, "Unable to load map file in World Editor");

            SleepUntilTrue(() => IsMapLoaded(mapFileName), (int)TimeSpan.FromMinutes(30).TotalMilliseconds, "Unable to load map file in World Editor");
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

        protected void RefreshProcess()
        {
            Process = Process.GetProcessById(Process.Id);
        }

        [GeneratedRegex(@"^[^[]+\[(.*)\]\s*$", RegexOptions.IgnoreCase)]
        protected static partial Regex Regex_GetMapFileName();
        public string GetLoadedMapFileName()
        {
            RefreshProcess();
            var mainWindowTitle = Process.MainWindowTitle;
            var match = Regex_GetMapFileName().Match(mainWindowTitle);
            return match.Success ? match.Groups[1].Value : "";
        }

        public bool IsMapLoaded(string mapFileName)
        {
            var loadedMapFileName = GetLoadedMapFileName().TrimEnd("*").TrimEnd(" ");
            return !string.IsNullOrWhiteSpace(loadedMapFileName) && Path.GetFileName(mapFileName).StartsWith(Path.GetFileName(loadedMapFileName));
        }

        public DateTime GetLastWriteTime()
        {
            var fileInfo = new FileInfo(MapFileName);
            return fileInfo.LastWriteTime;
        }

        public void SaveMap()
        {
            var originalWriteTime = GetLastWriteTime();

            using (var app = FlaUI.Core.Application.Attach(Process.Id))
            using (var automation = new UIA2Automation())
            {
                var mainWindow = app.GetMainWindow(automation);

                SleepUntilTrue(() =>
                {
                    try
                    {
                        SleepUntilTrue(() =>
                        {
                            Win32Api.SetForegroundWindow(Process.MainWindowHandle);
                            Thread.Sleep(1000);
                            if (Win32Api.GetForegroundWindow() == Process.MainWindowHandle)
                            {
                                SendKeys.SendWait("^s");
                                Thread.Sleep(1000);
                                return true;
                            }

                            return false;
                        }, (int)TimeSpan.FromMinutes(1).TotalMilliseconds, "");
                    }
                    catch { }

                    try
                    {
                        SleepUntilTrue(() =>
                        {
                            var warningWindow = mainWindow.ModalWindows.Where(x => x.Name.Equals("warning", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                            if (warningWindow != null)
                            {
                                var children = warningWindow.FindAllChildren();
                                if (children.Any(x => x.ControlType == ControlType.Text && x.Name.Contains("starting location", StringComparison.InvariantCultureIgnoreCase)))
                                {
                                    var yesButton = children.FirstOrDefault(x => x.ControlType == ControlType.Button && x.Name.Equals("yes", StringComparison.InvariantCultureIgnoreCase));
                                    if (yesButton != null)
                                    {
                                        yesButton.Click();
                                        Thread.Sleep(1000);
                                    }
                                }
                            }

                            return GetLastWriteTime() != originalWriteTime;
                        }, (int)TimeSpan.FromMinutes(1).TotalMilliseconds, "");
                    }
                    catch { }

                    return GetLastWriteTime() != originalWriteTime;
                }, (int)TimeSpan.FromMinutes(5).TotalMilliseconds, "Unable to save map file in World Editor");
            }
        }

        public void Dispose()
        {
            Process?.Kill();
        }
    }
}