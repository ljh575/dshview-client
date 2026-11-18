using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace DshView
{
    // One DSHView per user session. Two instances would each create their own
    // WebView2 browser process tree over the same user-data directory, and the
    // second window is usually invisible because the first may be parked in the
    // tray -- so users "reopen" an app that is still running and collect browser
    // processes. A second launch now just wakes the window that already exists.
    internal static class Instance
    {
        private const string MutexName = @"Local\DSHView.SingleInstance";
        private const string ShowMessageName = "DSHView.ShowWindow";

        // Kept for the whole process lifetime: dropping the handle would release
        // the claim and let a later launch start a second instance.
        private static Mutex _mutex;

        /// <summary>True when this process claimed the instance slot.</summary>
        public static bool IsFirstInstance()
        {
            bool created;
            try
            {
                _mutex = new Mutex(true, MutexName, out created);
            }
            catch (Exception ex)
            {
                App.Log("instance mutex failed, continuing as sole instance: " + ex.Message);
                return true;
            }
            if (created) return true;
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        /// <summary>Window message the running instance listens for.</summary>
        public static int ShowMessage
        {
            get { return RegisterWindowMessage(ShowMessageName); }
        }

        /// <summary>Asks whichever instance is running to show and focus itself.</summary>
        public static void ActivateRunning()
        {
            try
            {
                int message = RegisterWindowMessage(ShowMessageName);
                if (message != 0) PostMessage(HwndBroadcast, message, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex) { App.Log("activate request failed: " + ex.Message); }
        }

        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string name);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
