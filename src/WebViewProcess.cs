using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DshView
{
    // Shutting a WebView2 host down properly means waiting for the browser process
    // (msedgewebview2.exe) rather than just ending the message loop. When that wait
    // is skipped the browser process is orphaned and the next launch adds another
    // one, which is what made the process count creep up.
    internal static class WebViewProcess
    {
        /// <summary>Waits for the browser process, then reaps any direct children still around.</summary>
        public static void WaitAndReap(uint browserProcessId)
        {
            if (browserProcessId != 0)
            {
                try
                {
                    Process browser = Process.GetProcessById((int)browserProcessId);
                    if (browser.WaitForExit(3000)) App.Log("browser process exited: " + browserProcessId);
                    else App.Log("browser process still running after 3s: " + browserProcessId);
                }
                catch (ArgumentException)
                {
                    App.Log("browser process already gone: " + browserProcessId);
                }
                catch (Exception ex)
                {
                    App.Log("browser process wait failed: " + ex.Message);
                }
            }

            ReapChildren();
        }

        // Only direct children of this process are touched, so another product's
        // WebView2 (same image name, different parent) is never affected.
        private static void ReapChildren()
        {
            int self = Process.GetCurrentProcess().Id;
            int reaped = 0;
            foreach (Process candidate in Process.GetProcessesByName("msedgewebview2"))
            {
                try
                {
                    if (ParentProcessId(candidate) != self) continue;
                    candidate.Kill();
                    candidate.WaitForExit(2000);
                    reaped++;
                }
                catch (Exception ex)
                {
                    App.Log("reap failed for " + SafeId(candidate) + ": " + ex.Message);
                }
            }
            if (reaped > 0) App.Log("reaped " + reaped + " leftover webview process(es)");
        }

        private static string SafeId(Process p)
        {
            try { return p.Id.ToString(); } catch { return "?"; }
        }

        private static int ParentProcessId(Process process)
        {
            IntPtr snapshot = IntPtr.Zero;
            try
            {
                snapshot = process.Handle;
                var info = new ProcessBasicInformation();
                int returned;
                int status = NtQueryInformationProcess(process.Handle, 0, ref info, Marshal.SizeOf(info), out returned);
                if (status != 0) return -1;
                return info.InheritedFromUniqueProcessId.ToInt32();
            }
            catch { return -1; }
        }

        /// <summary>Test hook: parent PID via NtQueryInformationProcess, or -1.</summary>
        internal static int ParentProcessIdOf(int processId)
        {
            try
            {
                using (Process p = Process.GetProcessById(processId)) return ParentProcessId(p);
            }
            catch { return -1; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessBasicInformation
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr handle, int infoClass,
            ref ProcessBasicInformation info, int size, out int returned);
    }
}
