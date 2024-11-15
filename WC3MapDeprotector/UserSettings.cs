namespace WC3MapDeprotector
{
    public static class UserSettings
    {
        static UserSettings()
        {
            CorrectPaths();
        }            

        public static string WC3ExePath
        {
            get
            {
                return Properties.Settings.Default.WC3ExePath;
            }
            set
            {
                if (Properties.Settings.Default.WC3ExePath == value)
                {
                    return;
                }

                Properties.Settings.Default.WC3ExePath = value;
                Properties.Settings.Default.Save();
                CorrectPaths();
            }
        }

        public static string WorldEditExePath
        {
            get
            {
                return Properties.Settings.Default.WorldEditExePath;
            }
            set
            {
                if (Properties.Settings.Default.WorldEditExePath == value)
                {
                    return;
                }

                Properties.Settings.Default.WorldEditExePath = value;
                Properties.Settings.Default.Save();
                CorrectPaths();
            }
        }

        public static string WC3LocalFilesPath
        {
            get
            {
                return Path.GetDirectoryName(Path.GetDirectoryName(WC3ExePath));
            }
        }

        private static void CorrectPaths()
        {
            var directories = new List<string>() { Path.GetDirectoryName(WorldEditExePath), Path.GetDirectoryName(WC3ExePath) };

            foreach (var directory in directories)
            {
                var wc3ExePath = Path.Combine(directory, "Warcraft III.exe");
                if (File.Exists(wc3ExePath))
                {
                    WC3ExePath = wc3ExePath;
                }

                var worldEditPath = Path.Combine(directory, "World Editor.exe");
                if (File.Exists(worldEditPath))
                {
                    WorldEditExePath = worldEditPath;
                }
            }
        }
    }
}
