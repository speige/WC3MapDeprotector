using System.Runtime.InteropServices;

namespace WC3MapDeprotector
{
    public static class Win32Api
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}