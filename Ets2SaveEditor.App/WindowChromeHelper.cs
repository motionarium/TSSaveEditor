using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Ets2SaveEditor.App
{
    /// <summary>Forces a dark Win10/11 title bar to match the app chrome.</summary>
    internal static class WindowChromeHelper
    {
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaCaptionColor = 35; // Win11 22H2+
        private const int DwmwaBorderColor = 34;

        // Matches Window Background="#0F1117"
        private const uint CaptionColorBgr = 0x0017110F;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);

        public static void ApplyDarkTitleBar(Window window)
        {
            if (window == null) return;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDark = 1;
            // Prefer attribute 20; fall back to 19 on older builds.
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));

            // Win11: paint caption/border the same as the client area.
            uint color = CaptionColorBgr;
            DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref color, sizeof(uint));
            DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref color, sizeof(uint));
        }
    }
}
