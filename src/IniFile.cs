using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DshView
{
    // Rewrites one key in dshview.ini and leaves everything else untouched:
    // comments, key order, unknown keys and quoting survive, because only the
    // value line itself is replaced. A missing key is appended. The write goes
    // through a temporary file so a crash cannot truncate the user's config.
    internal static class IniFile
    {
        public static bool Set(string path, string key, string value)
        {
            string temp = path + ".tmp";
            try
            {
                List<string> lines = new List<string>();
                string newline = "\r\n";
                bool trailingNewline = true;

                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    if (text.IndexOf("\r\n") < 0 && text.IndexOf('\n') >= 0) newline = "\n";
                    string[] parts = text.Replace("\r\n", "\n").Split('\n');
                    for (int i = 0; i < parts.Length; i++) lines.Add(parts[i]);
                    // The final empty element is the trailing newline, not a real line.
                    if (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                    {
                        lines.RemoveAt(lines.Count - 1);
                        trailingNewline = true;
                    }
                    else
                    {
                        trailingNewline = false;
                    }
                }

                string wanted = key.Trim().ToLowerInvariant();
                bool replaced = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    if (line.Substring(0, eq).Trim().ToLowerInvariant() != wanted) continue;
                    lines[i] = key + "=" + value;
                    replaced = true;
                    break;
                }

                if (!replaced)
                {
                    if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0) lines.Add(string.Empty);
                    lines.Add(key + "=" + value);
                    trailingNewline = true;
                }

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < lines.Count; i++)
                {
                    sb.Append(lines[i]);
                    if (i < lines.Count - 1 || trailingNewline) sb.Append(newline);
                }

                File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(false));
                if (!File.Exists(path))
                {
                    File.Move(temp, path);
                }
                else
                {
                    // File.Replace is atomic but needs DELETE access on the target,
                    // which a locked-down profile or a file-sandboxed host may deny.
                    try
                    {
                        File.Replace(temp, path, null);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        File.Copy(temp, path, true);
                        File.Delete(temp);
                    }
                    catch (IOException)
                    {
                        File.Copy(temp, path, true);
                        File.Delete(temp);
                    }
                }
                App.Log("ini: " + key + "=" + value + " written to " + path);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { }
                App.Log("ini: failed to write " + key + " (" + ex.Message + ")");
                return false;
            }
        }
    }
}
