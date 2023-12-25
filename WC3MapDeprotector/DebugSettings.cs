using System.Diagnostics;

namespace WC3MapDeprotector
{
    public static class DebugSettings
    {
        public static bool IsDebugMode
        {
            get
            {
                bool result = false;
                Debug.Assert(result = true); //NOTE: = assigns value to true, but Assert call is skipped it not debug mode
                return result;
            }
        }

        //These allow easy config changes for testing purposes. They should always default to false. Customizing your setup should always be done within the "if (IsDebugMode)" block
        public static bool BenchmarkUnknownRecovery;
        public static bool DontCleanTemp;
        public static bool BreakOnWarnings;
        static DebugSettings()
        {
            if (IsDebugMode)
            {
                //BenchmarkUnknownRecovery = true;
                //DontCleanTemp = true;
                BreakOnWarnings = true;
            }
        }

        public static void Warn(string reason)
        {
            if (!BreakOnWarnings)
            {
                return;
            }

            Console.WriteLine(reason);
            Debugger.Break();
        }
    }
}