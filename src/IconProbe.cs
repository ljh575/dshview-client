// Verifies which embedded icon resource DSHView picks for each icon= mode,
// without opening a window (the desktop session may be locked).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

internal static class IconProbe
{
    private static void Main(string[] args)
    {
        string exe = args[0];
        Assembly asm = Assembly.Load(File.ReadAllBytes(exe));

        List<string> names = new List<string>();
        foreach (string n in asm.GetManifestResourceNames())
        {
            if (n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)) names.Add(n);
        }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        Console.WriteLine("embedded ico resources: " + string.Join(", ", names.ToArray()));

        // Replicates App.PickThemeIcon's selection (auto resolves via the registry).
        bool light = true;
        try
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key != null)
                {
                    object v = key.GetValue("AppsUseLightTheme");
                    if (v is int) light = (int)v != 0;
                }
            }
        }
        catch { }
        Console.WriteLine("AppsUseLightTheme => " + (light ? "light" : "dark"));

        foreach (string mode in new string[] { "default", "auto", "light", "dark" })
        {
            string resource;
            if (mode == "light") resource = "app_black.ico";
            else if (mode == "dark") resource = "app_white.ico";
            else if (mode == "auto") resource = light ? "app_black.ico" : "app_white.ico";
            else resource = "app_dsh.ico";

            using (Stream s = asm.GetManifestResourceStream(resource))
            {
                if (s == null) { Console.WriteLine(mode + " -> MISSING " + resource); continue; }
                using (Icon icon = new Icon(s))
                using (Bitmap bmp = icon.ToBitmap())
                {
                    Color corner = bmp.GetPixel(1, 1);
                    Color center = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
                    Console.WriteLine(string.Format(
                        "{0,-8} -> {1,-14} {2}x{3}  corner A={4} RGB=({5},{6},{7})  center A={8} RGB=({9},{10},{11})",
                        mode, resource, bmp.Width, bmp.Height,
                        corner.A, corner.R, corner.G, corner.B,
                        center.A, center.R, center.G, center.B));
                }
            }
        }
    }
}
