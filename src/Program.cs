// DSHView - a minimal WebView2 shell for a remote DSH web workspace on an
// isolated intranet (no system WebView2 required; the runtime ships alongside).
//
// Build:
//   csc /nologo /target:winexe /platform:anycpu /out:DSHView.exe
//       /win32icon:app.ico /win32manifest:app.manifest
//       /r:Microsoft.Web.WebView2.Core.dll /r:Microsoft.Web.WebView2.WinForms.dll
//       /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll
//       Program.cs
//
// C# 5 only (compiled by the in-box .NET Framework 4.x csc.exe).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DshView
{
    internal sealed class Settings
    {
        public string Url = "http://127.0.0.1:3080/";
        public string Runtime = "webview2";   // folder next to the exe; "system" => installed runtime
        public string Data = "profile";
        public double Zoom = 1.0;
        public string ExtraArgs = "";
        public bool SecureOrigin = true;      // promote a plain-http LAN origin to a secure context
        public bool Toolbar = false;          // DSH-Portable style: content only, no chrome
        public string Icon = "default";       // default | auto | light | dark
        public bool CloseToTray = false;      // false (default) = ask on close; true = always hide to tray
        public bool SelfTest = false;
        public bool Bare = false;             // diagnostics: no default browser flags

        public static Settings Parse(string[] args)
        {
            Settings s = new Settings();
            string ini = Path.Combine(App.BaseDir, "dshview.ini");
            if (File.Exists(ini))
            {
                try
                {
                    foreach (string raw in File.ReadAllLines(ini, Encoding.UTF8))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                        string val = line.Substring(eq + 1).Trim();
                        if (val.Length >= 2 && val.StartsWith("\"") && val.EndsWith("\""))
                            val = val.Substring(1, val.Length - 2);
                        switch (key)
                        {
                            case "url": s.Url = val; break;
                            case "runtime": s.Runtime = val; break;
                            case "data": s.Data = val; break;
                            case "zoom": s.Zoom = ParseDouble(val, 1.0); break;
                            case "args": s.ExtraArgs = val; break;
                            case "toolbar": s.Toolbar = IsTrue(val); break;
                            case "icon": s.Icon = val; break;
                            case "closetotray": s.CloseToTray = IsTrue(val); break;
                            case "secureorigin": s.SecureOrigin = IsTrue(val); break;
                        }
                    }
                }
                catch (Exception ex) { App.Log("ini parse failed: " + ex.Message); }
            }
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (a == "--selftest") { s.SelfTest = true; continue; }
                    if (a == "--bare") { s.Bare = true; continue; }
                    if (a == "--toolbar") { s.Toolbar = true; continue; }
                    if (a.StartsWith("--url=")) s.Url = a.Substring(6);
                    else if (a.StartsWith("--runtime=")) s.Runtime = a.Substring(10);
                    else if (a.StartsWith("--data=")) s.Data = a.Substring(7);
                    else if (a.StartsWith("--zoom=")) s.Zoom = ParseDouble(a.Substring(7), 1.0);
                    else if (a.StartsWith("--args=")) s.ExtraArgs = a.Substring(7);
                    else if (a.StartsWith("--icon=")) s.Icon = a.Substring(7);
                }
            }
            return s;
        }

        private static bool IsTrue(string v)
        {
            v = (v ?? "").Trim().ToLowerInvariant();
            return v == "1" || v == "true" || v == "yes" || v == "on";
        }

        private static double ParseDouble(string v, double fallback)
        {
            double d;
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }
    }

    internal static class App
    {
        public static string BaseDir = Path.GetDirectoryName(Application.ExecutablePath);
        public static Settings S;
        public static string LogFile;
        public static List<string> LastProbe = new List<string>();
        private static readonly object LogLock = new object();

        public static string IniPath
        {
            get { return Path.Combine(BaseDir, "dshview.ini"); }
        }

        public static void Log(string message)
        {
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        // The package always ships dshview.ini, but recovery copies of just the exe
        // would otherwise silently fall back to a hard-coded URL. Recreate it.
        public static void EnsureIni(string defaultUrl)
        {
            try
            {
                if (File.Exists(IniPath)) return;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# DSHView configuration / DSHView 配置文件");
                sb.AppendLine("# ----------------------------------------------------------------------------");
                sb.AppendLine("# Edit and relaunch DSHView.exe to apply. This file is recreated automatically");
                sb.AppendLine("# when missing, so deleting it is always safe.");
                sb.AppendLine("# 改完保存后重新打开 DSHView.exe 生效；文件被删除时会自动重建，删掉不会出问题。");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();
                sb.AppendLine("# DSH Web address. Defaults to dsh web's own default (same-machine setup).");
                sb.AppendLine("# 如果 dsh web 在另一台机器上，这里要写那台机器的 IP，例如：");
                sb.AppendLine("#   url=http://10.0.0.123:3080/");
                sb.AppendLine("# On first use (or after changing port/machine) include the ?token=... that");
                sb.AppendLine("# dsh web prints; afterwards a 30-day signed cookie takes over and the plain");
                sb.AppendLine("# address is enough.");
                sb.AppendLine("# 首次访问（或换端口/换机器）时带上 dsh web 打印的 ?token=...；");
                sb.AppendLine("# 拿到 cookie 之后只需填纯地址，凭据默认 30 天有效，dsh web 重启也不会失效。");
                sb.AppendLine("url=" + defaultUrl);
                sb.AppendLine();
                sb.AppendLine("# Kernel location / 内核位置:");
                sb.AppendLine("#   webview2 = the fixed-version runtime bundled in this folder (recommended)");
                sb.AppendLine("#              用本目录自带的固定版运行时（推荐，目标机无需安装 WebView2）");
                sb.AppendLine("#   system   = the WebView2 runtime installed on the machine");
                sb.AppendLine("#              用系统已安装的那份 WebView2");
                sb.AppendLine("runtime=webview2");
                sb.AppendLine();
                sb.AppendLine("# Browser data directory: holds cookies and login state.");
                sb.AppendLine("# 浏览器数据目录：保存 token 与登录 cookie。删掉它等于清空登录状态。");
                sb.AppendLine("data=profile");
                sb.AppendLine();
                sb.AppendLine("# Initial zoom factor / 初始缩放比例");
                sb.AppendLine("zoom=1.0");
                sb.AppendLine();
                sb.AppendLine("# Address bar: 0 = content-only window (default, like DSH-Portable), 1 = show chrome");
                sb.AppendLine("# 是否显示地址栏：0 = 纯内容窗口（默认）；1 = 带地址栏与前进/后退/缩放");
                sb.AppendLine("toolbar=0");
                sb.AppendLine();
                sb.AppendLine("# Icon style / 图标样式 (four values):");
                sb.AppendLine("#   default = official DSH artwork (default; readable on light and dark frames)");
                sb.AppendLine("#             DSH 官方图标，带圆角底，浅色/深色背景都看得清");
                sb.AppendLine("#   auto    = follow the system theme: dark whale on light, white whale on dark");
                sb.AppendLine("#             跟随系统主题：浅色用黑鲸、深色用白鲸（读 AppsUseLightTheme）");
                sb.AppendLine("#   light   = always the dark whale (for a light title bar)");
                sb.AppendLine("#             始终黑鲸，适合浅色标题栏");
                sb.AppendLine("#   dark    = always the white whale (for a dark title bar)");
                sb.AppendLine("#             始终白鲸，适合深色标题栏");
                sb.AppendLine("icon=default");
                sb.AppendLine();
                sb.AppendLine("# Close button: 0 = ask what to do (default) -- minimize to the notification");
                sb.AppendLine("# area or quit; 1 = always minimize to the notification area without asking.");
                sb.AppendLine("# Either way the window leaves the taskbar, double-click the tray icon to");
                sb.AppendLine("# restore it, right-click for a menu with Show / Quit.");
                sb.AppendLine("# 关闭按钮：0 = 点关闭弹出询问框（最小化到托盘 / 直接退出，默认）；");
                sb.AppendLine("#           1 = 直接最小化到托盘，不再询问。");
                sb.AppendLine("closetotray=0");
                sb.AppendLine();
                sb.AppendLine("# Extra Chromium flags, space separated. Leave empty normally.");
                sb.AppendLine("# 追加给内核的参数，空格分隔，通常留空。");
                sb.AppendLine("# If a security product blocks Chromium's sandbox IPC, try adding --no-sandbox");
                sb.AppendLine("# to diagnose. 若安全软件拦截 Chromium 沙箱 IPC，可临时加 --no-sandbox 定位。");
                sb.AppendLine("args=");
                sb.AppendLine();
                sb.AppendLine("# Promote an intranet http origin to a secure context so navigator.clipboard");
                sb.AppendLine("# works. Set to 0 when serving over HTTPS.");
                sb.AppendLine("# 把内网 http 地址提升为安全上下文，否则剪贴板 API 不可用；套了 HTTPS 可设 0。");
                sb.AppendLine("secureorigin=1");
                File.WriteAllText(IniPath, sb.ToString(), new UTF8Encoding(false));
                Log("created default ini: " + IniPath);
            }
            catch (Exception ex) { Log("EnsureIni failed: " + ex.Message); }
        }

        // icon=default uses the official DSH artwork (rounded backdrop, read anywhere).
        // icon=light|dark|auto switch between the monochrome whale variants: light =>
        // dark whale on a light frame, dark => white whale, auto => follow
        // AppsUseLightTheme. The exe's static icon is the official one, so Explorer
        // and the taskbar match icon=default.
        public static Icon PickThemeIcon(string mode)
        {
            string choice = (mode ?? "default").Trim().ToLowerInvariant();
            string resource;
            if (choice == "light") resource = "app_black.ico";
            else if (choice == "dark") resource = "app_white.ico";
            else if (choice == "auto") resource = SystemUsesLightTheme() ? "app_black.ico" : "app_white.ico";
            else
            {
                if (choice != "default") Log("unknown icon mode '" + choice + "', falling back to default");
                resource = "app_dsh.ico";
            }

            Log("icon=" + choice + " -> " + resource);
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream(resource))
                {
                    if (s != null) return new Icon(s);
                }
                Log("icon resource missing: " + resource);
            }
            catch (Exception ex) { Log("theme icon failed: " + ex.Message); }

            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return null; }
        }

        public static bool SystemUsesLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int) return (int)value != 0;
                    }
                }
            }
            catch { }
            return true;
        }

        public static string ResolveRuntime(string configured)
        {
            List<string> candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configured)
                && !configured.Equals("system", StringComparison.OrdinalIgnoreCase)
                && !configured.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                string dir = configured;
                if (!Path.IsPathRooted(dir)) dir = Path.Combine(BaseDir, configured);
                candidates.Add(Path.GetFullPath(dir));
            }
            if (string.IsNullOrWhiteSpace(configured)
                || !configured.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.Combine(BaseDir, "webview2"));
                candidates.Add(Path.Combine(BaseDir, "WebView2"));
                candidates.Add(Path.Combine(BaseDir, "runtime", "webview2"));
                try
                {
                    string parent = Path.GetDirectoryName(BaseDir);
                    if (!string.IsNullOrEmpty(parent)) candidates.Add(Path.Combine(parent, "webview2"));
                }
                catch { }
            }

            List<string> probed = new List<string>();
            foreach (string candidate in candidates)
            {
                probed.Add(candidate);
                try
                {
                    if (File.Exists(Path.Combine(candidate, "msedgewebview2.exe"))) return candidate;
                }
                catch { }
            }
            LastProbe = probed;
            if (probed.Count > 0) Log("no bundled runtime found; probed: " + string.Join(" | ", probed.ToArray()));
            return null;
        }

        public static string BuildArguments(Settings s)
        {
            List<string> parts = new List<string>();
            if (!s.Bare)
            {
                parts.Add("--disable-gpu");
                parts.Add("--disable-software-rasterizer");
                parts.Add("--disable-background-networking");
                parts.Add("--disable-sync");
                parts.Add("--no-first-run");
                parts.Add("--no-default-browser-check");
                parts.Add("--disable-features=msWebOOUI,msPdfOOUI,msSmartScreenProtection");
            }
            if (s.SecureOrigin)
            {
                string origin = OriginOf(s.Url);
                if (origin != null) parts.Add("--unsafely-treat-insecure-origin-as-secure=" + origin);
            }
            if (!string.IsNullOrWhiteSpace(s.ExtraArgs)) parts.Add(s.ExtraArgs.Trim());
            return string.Join(" ", parts.ToArray());
        }

        public static string OriginOf(string url)
        {
            try
            {
                Uri u = new Uri(url);
                if (u.Scheme != "http") return null;
                if (u.Host == "127.0.0.1" || u.Host == "localhost" || u.Host == "::1") return null;
                return u.Scheme + "://" + u.Host + (u.IsDefaultPort ? "" : ":" + u.Port.ToString(CultureInfo.InvariantCulture));
            }
            catch { return null; }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Settings _s;
        private WebView2 _view;
        private Panel _bar;
        private TextBox _address;
        private Button _back, _forward, _reload, _home, _zoomOut, _zoomIn, _menu;
        private Label _zoomLabel;
        private System.Windows.Forms.Timer _selfTestTimer;
        private bool _finished;
        private bool _failureShown;
        private NotifyIcon _tray;
        private bool _exiting;
        private FormWindowState _stateBeforeHide = FormWindowState.Normal;
        private bool _trayHintShown;
        private bool _closePromptOpen;

        public MainForm(Settings settings)
        {
            _s = settings;
            Text = "DeepSeek Harness";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1180, 820);
            MinimumSize = new Size(560, 420);
            try { Icon = App.PickThemeIcon(_s.Icon); }
            catch { }
            LoadLayout();
            if (_s.CloseToTray && !_s.SelfTest) SetupTray();

            if (_s.Toolbar) BuildToolbar();

            _view = new WebView2();
            _view.Dock = DockStyle.Fill;
            _view.DefaultBackgroundColor = Color.White;
            Controls.Add(_view);
            _view.BringToFront();

            Shown += delegate
            {
                if (_bar != null) LayoutBar(_bar);
                // Defer until the window is fully shown: the WebView2 controller needs
                // a live parent HWND, and creating it during Load races with layout.
                BeginInvoke((MethodInvoker)delegate { BeginInit(); });
            };

            FormClosing += OnFormClosing;
            KeyPreview = true;
            KeyDown += OnKeyDown;
        }

        private void BuildToolbar()
        {
            _bar = new Panel();
            _bar.Dock = DockStyle.Top;
            _bar.Height = 40;
            _bar.BackColor = Color.FromArgb(246, 247, 249);
            Controls.Add(_bar);

            _back = MakeButton("\u2190", "后退 (Alt+左)");
            _forward = MakeButton("\u2192", "前进 (Alt+右)");
            _reload = MakeButton("\u27F3", "刷新 (F5)");
            _home = MakeButton("\u2302", "回到起始地址");
            _zoomOut = MakeButton("\u2212", "缩小 (Ctrl+-)");
            _zoomIn = MakeButton("+", "放大 (Ctrl++)");
            _menu = MakeButton("\u22EF", "更多");

            _back.Click += delegate { GoBack(); };
            _forward.Click += delegate { GoForward(); };
            _reload.Click += delegate { Reload(); };
            _home.Click += delegate { Navigate(_s.Url); };
            _zoomOut.Click += delegate { SetZoom(-0.1); };
            _zoomIn.Click += delegate { SetZoom(0.1); };
            _menu.Click += delegate(object sender, EventArgs e) { ShowMenu(_menu); };

            _address = new TextBox();
            _address.BorderStyle = BorderStyle.FixedSingle;
            _address.Font = new Font("Segoe UI", 9.5f);
            _address.BackColor = Color.White;
            _address.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Navigate(_address.Text.Trim());
                }
            };

            _zoomLabel = new Label();
            _zoomLabel.Text = "100%";
            _zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            _zoomLabel.Font = new Font("Segoe UI", 8.5f);
            _zoomLabel.BackColor = Color.Transparent;

            _bar.Controls.Add(_back);
            _bar.Controls.Add(_forward);
            _bar.Controls.Add(_reload);
            _bar.Controls.Add(_home);
            _bar.Controls.Add(_address);
            _bar.Controls.Add(_zoomOut);
            _bar.Controls.Add(_zoomLabel);
            _bar.Controls.Add(_zoomIn);
            _bar.Controls.Add(_menu);
            _bar.Resize += delegate { LayoutBar(_bar); };
        }

        // The tray icon keeps the process alive with no window and no taskbar
        // button (a hidden top-level window drops out of the taskbar by itself,
        // so ShowInTaskbar is left alone -- toggling it would recreate the HWND
        // and tear down the WebView2 controller).
        private void SetupTray()
        {
            _tray = new NotifyIcon();
            try { _tray.Icon = App.PickThemeIcon(_s.Icon); }
            catch { _tray.Icon = SystemIcons.Application; }
            _tray.Text = "DeepSeek Harness";
            _tray.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示窗口", null, delegate { RestoreFromTray(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitFromTray(); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { RestoreFromTray(); };
            App.Log("tray icon created");
        }

        private void HideToTray()
        {
            if (_tray == null) SetupTray();
            SaveLayout();
            _stateBeforeHide = WindowState;
            Hide();
            App.Log("hidden to tray");

            if (!_trayHintShown)
            {
                _trayHintShown = true;
                try
                {
                    _tray.BalloonTipTitle = "DeepSeek Harness";
                    _tray.BalloonTipText = "已最小化到托盘。双击图标可恢复，右键菜单可退出。";
                    _tray.ShowBalloonTip(4000);
                }
                catch { }
            }
        }

        private void RestoreFromTray()
        {
            Show();
            if (_stateBeforeHide == FormWindowState.Maximized) WindowState = FormWindowState.Maximized;
            else if (_stateBeforeHide == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            App.Log("restored from tray");
        }

        private void ExitFromTray()
        {
            _exiting = true;
            App.Log("exit requested from tray");
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            Close();
            Application.Exit();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            SaveLayout();

            bool userClosed = e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.ApplicationExitCall;
            if (!_exiting && e.CloseReason == CloseReason.UserClosing)
            {
                if (_s.CloseToTray)
                {
                    e.Cancel = true;
                    HideToTray();
                    return;
                }

                // closetotray=0 asks instead of quitting. The prompt is posted to
                // the message loop rather than opened here: running a modal dialog
                // from inside the closing handler would block a form that is
                // already tearing down.
                e.Cancel = true;
                if (!_closePromptOpen)
                {
                    _closePromptOpen = true;
                    BeginInvoke((MethodInvoker)delegate { AskCloseChoice(); });
                }
                return;
            }

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            if (!userClosed) App.Log("closing: " + e.CloseReason);
        }

        // Asks what this close should do. Only an explicit "直接退出" ends the
        // process; the dialog's X, Esc and 取消 all leave the window open.
        private void AskCloseChoice()
        {
            try
            {
                DialogResult choice;
                using (CloseDialog dlg = new CloseDialog())
                {
                    choice = dlg.ShowDialog(this);
                }
                App.Log("close prompt: " + choice);

                if (choice == DialogResult.Yes)
                {
                    HideToTray();
                    return;
                }
                if (choice == DialogResult.No)
                {
                    App.Log("exit requested from close prompt");
                    _exiting = true;
                    Close();
                    Application.Exit();
                }
            }
            finally
            {
                _closePromptOpen = false;
            }
        }

        private Button MakeButton(string text, string tip)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.Transparent;
            b.Font = new Font("Segoe UI", 11f);
            b.TabStop = false;
            new ToolTip().SetToolTip(b, tip);
            return b;
        }

        private void LayoutBar(Panel bar)
        {
            int h = 28, top = 6, x = 6;
            int right = bar.Width - 6;
            if (right < 500) return;

            int menuX = right - 32;
            int zoomInX = menuX - 32;
            int zoomLabelX = zoomInX - 46;
            int zoomOutX = zoomLabelX - 32;

            _back.SetBounds(x, top, 32, h); x += 34;
            _forward.SetBounds(x, top, 32, h); x += 34;
            _reload.SetBounds(x, top, 32, h); x += 34;
            _home.SetBounds(x, top, 32, h); x += 40;

            int width = zoomOutX - x - 8;
            if (width < 80) width = 80;
            _address.SetBounds(x, top, width, h);

            _zoomOut.SetBounds(zoomOutX, top, 32, h);
            _zoomLabel.SetBounds(zoomLabelX, top, 46, h);
            _zoomIn.SetBounds(zoomInX, top, 32, h);
            _menu.SetBounds(menuX, top, 32, h);
        }

        private async void BeginInit()
        {
            string runtime = App.ResolveRuntime(_s.Runtime);
            string dataDir = Path.Combine(App.BaseDir, string.IsNullOrWhiteSpace(_s.Data) ? "profile" : _s.Data);
            try { Directory.CreateDirectory(dataDir); }
            catch (Exception ex) { Fail("无法创建数据目录", dataDir, ex); return; }

            // A machine-wide WEBVIEW2_BROWSER_EXECUTABLE_FOLDER silently overrides the
            // folder we pass in (common on machines with a package-managed WebView2).
            string envOverride = Environment.GetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER");
            if (runtime != null && !string.IsNullOrEmpty(envOverride))
            {
                Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", null);
                App.Log("ignored WEBVIEW2_BROWSER_EXECUTABLE_FOLDER=" + envOverride + " (bundled runtime wins)");
            }

            App.Log("runtime=" + (runtime ?? "(system)") + " data=" + dataDir);
            try { App.Log("version=" + CoreWebView2Environment.GetAvailableBrowserVersionString(runtime)); }
            catch (Exception ex) { App.Log("version probe failed: " + ex.Message); }

            CoreWebView2Environment env;
            try
            {
                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions(App.BuildArguments(_s));
                App.Log("args=" + App.BuildArguments(_s));
                env = await CoreWebView2Environment.CreateAsync(runtime, dataDir, options);
            }
            catch (Exception ex)
            {
                string hint;
                if (runtime == null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("程序没有找到自带的 webview2 运行时，本机也没有安装系统 WebView2。");
                    sb.AppendLine();
                    if (App.LastProbe.Count > 0)
                    {
                        sb.AppendLine("已尝试这些目录（需包含 msedgewebview2.exe）：");
                        foreach (string p in App.LastProbe) sb.AppendLine("  " + p);
                        sb.AppendLine();
                    }
                    sb.AppendLine("请把完整的 webview2 目录放到 DSHView.exe 旁边；");
                    sb.AppendLine("或者在本机安装一次 WebView2 运行时，再把 dshview.ini 的 runtime 设为 system。");
                    hint = sb.ToString();
                }
                else
                {
                    hint = "运行时目录已找到：" + runtime + Environment.NewLine
                        + "但内核没能启动。常见原因：目录不完整（缺 msedge.dll 或版本号.manifest 文件）、"
                        + "被杀毒软件隔离、或与另一份 WebView2 冲突。详见 dshview.log。";
                }
                Fail("WebView2 运行时初始化失败", hint, ex);
                return;
            }

            try { await _view.EnsureCoreWebView2Async(env); }
            catch (Exception ex) { Fail("WebView2 控件初始化失败", "详见 dshview.log。", ex); return; }

            CoreWebView2 core = _view.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.Settings.IsPasswordAutosaveEnabled = false;

            core.NavigationStarting += delegate(object sender, CoreWebView2NavigationStartingEventArgs e)
            {
                if (_address != null) _address.Text = e.Uri;
                if (_reload != null) _reload.Text = "\u2715";
            };
            core.NavigationCompleted += delegate(object sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (_reload != null) _reload.Text = "\u27F3";
                if (_back != null) _back.Enabled = core.CanGoBack;
                if (_forward != null) _forward.Enabled = core.CanGoForward;
                try { if (_address != null) _address.Text = core.Source; } catch { }
                App.Log("nav " + (e.IsSuccess ? "ok" : "fail:" + e.WebErrorStatus) + " " + (core.Source ?? ""));
                if (!e.IsSuccess && !_s.SelfTest) ShowLoadFailure(e.WebErrorStatus);
                if (_s.SelfTest) FinishSelfTest(e.IsSuccess, core.Source, e.WebErrorStatus.ToString());
            };
            core.DocumentTitleChanged += delegate
            {
                // Keep a stable window identity in the alt-tab switcher and taskbar.
                if (_s.Toolbar)
                {
                    string t = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "DeepSeek Harness" : core.DocumentTitle;
                    Text = t + " \u2014 DeepSeek Harness";
                }
            };
            core.PermissionRequested += delegate(object sender, CoreWebView2PermissionRequestedEventArgs e)
            {
                e.State = CoreWebView2PermissionState.Allow;
                App.Log("permission " + e.PermissionKind + " allowed");
            };
            core.NewWindowRequested += delegate(object sender, CoreWebView2NewWindowRequestedEventArgs e)
            {
                e.Handled = true;
                if (IsSameOrigin(e.Uri)) Navigate(e.Uri);
                else SpawnChild(e.Uri);
            };
            core.ProcessFailed += delegate(object sender, CoreWebView2ProcessFailedEventArgs e)
            {
                App.Log("process failed: " + e.ProcessFailedKind + " exit=" + e.ExitCode);
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited && !_s.SelfTest)
                    MessageBox.Show(this, "浏览器进程异常退出，请重新打开本程序。\n详见 dshview.log。", "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            if (_s.Zoom > 0.3 && _s.Zoom < 3.0)
            {
                _view.ZoomFactor = _s.Zoom;
                if (_zoomLabel != null) _zoomLabel.Text = Math.Round(_s.Zoom * 100).ToString(CultureInfo.InvariantCulture) + "%";
            }

            if (_s.SelfTest)
            {
                _selfTestTimer = new System.Windows.Forms.Timer();
                _selfTestTimer.Interval = 30000;
                _selfTestTimer.Tick += delegate { FinishSelfTest(false, _s.Url, "timeout"); };
                _selfTestTimer.Start();
            }

            Navigate(_s.Url);
        }

        // No toolbar means no place to fix a wrong token, so surface the file to edit.
        private void ShowLoadFailure(CoreWebView2WebErrorStatus status)
        {
            if (_failureShown) return;
            _failureShown = true;
            string message = "页面没有加载成功：" + status + Environment.NewLine
                + Environment.NewLine
                + "当前地址：" + _s.Url + Environment.NewLine
                + Environment.NewLine
                + "如果 dsh web 在另一台机器上，这里不能写 127.0.0.1（那指向本机自己），"
                + "要写服务器的 IPv4 地址，例如 http://10.0.0.123:3080/ 。" + Environment.NewLine
                + "服务器那边还必须用同一地址监听：dsh web --host 10.0.0.123 --port 3080"
                + "（--host 0.0.0.0 不受支持）。" + Environment.NewLine
                + Environment.NewLine
                + "如果以前成功打开过，先按 F5 刷新（登录凭据有 30 天有效期）。" + Environment.NewLine
                + "如果提示未认证，把 dsh web 打印的 ?token=... 地址填进 ini 的 url 再刷新：" + Environment.NewLine
                + App.IniPath;
            MessageBox.Show(this, message, "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void GoBack()
        {
            try { if (_view.CoreWebView2.CanGoBack) _view.CoreWebView2.GoBack(); } catch { }
        }

        private void GoForward()
        {
            try { if (_view.CoreWebView2.CanGoForward) _view.CoreWebView2.GoForward(); } catch { }
        }

        private bool IsSameOrigin(string url)
        {
            try
            {
                Uri a = new Uri(url);
                Uri b = new Uri(_s.Url);
                return a.Scheme == b.Scheme && a.Host == b.Host && a.Port == b.Port;
            }
            catch { return false; }
        }

        private void Fail(string title, string hint, Exception ex)
        {
            App.Log("FATAL " + title + ": " + ex);
            if (_s.SelfTest) { FinishSelfTest(false, _s.Url, title + " | " + ex.Message); return; }
            MessageBox.Show(this, title + "\n\n" + hint + "\n\n" + ex.Message, "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void FinishSelfTest(bool ok, string url, string detail)
        {
            if (_finished) return;
            _finished = true;
            try
            {
                if (_selfTestTimer != null) { _selfTestTimer.Stop(); _selfTestTimer.Dispose(); }
                string body = "{\"ok\":" + (ok ? "true" : "false")
                    + ",\"url\":" + JsonString(url)
                    + ",\"detail\":" + JsonString(detail)
                    + ",\"version\":" + JsonString(ProbeVersion()) + "}";
                File.WriteAllText(Path.Combine(App.BaseDir, "selftest-result.json"), body, Encoding.UTF8);
            }
            catch { }
            Environment.ExitCode = ok ? 0 : 1;
            try { Application.Exit(); } catch { }
            Environment.Exit(Environment.ExitCode);
        }

        private static string ProbeVersion()
        {
            try { return CoreWebView2Environment.GetAvailableBrowserVersionString(App.ResolveRuntime(App.S.Runtime)); }
            catch { return "?"; }
        }

        private static string JsonString(string s)
        {
            if (s == null) s = "";
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.R) { Reload(); e.SuppressKeyPress = true; }
            else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)) { SetZoom(0.1); e.SuppressKeyPress = true; }
            else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)) { SetZoom(-0.1); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.D0) { SetZoomAbsolute(1.0); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F5) { Reload(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F12) { OpenDevTools(); e.SuppressKeyPress = true; }
            else if (_address != null && e.Control && e.KeyCode == Keys.L) { _address.Focus(); _address.SelectAll(); e.SuppressKeyPress = true; }
        }

        private void ShowMenu(Button anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("复制当前地址", null, delegate { try { Clipboard.SetText(CurrentUrl()); } catch { } });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("开发者工具 (F12)", null, delegate { OpenDevTools(); });
            menu.Items.Add("在系统浏览器中打开", null, delegate { try { Process.Start(CurrentUrl()); } catch { } });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("关于", null, delegate
            {
                string v;
                try { v = CoreWebView2Environment.GetAvailableBrowserVersionString(App.ResolveRuntime(_s.Runtime)); }
                catch { v = "未知"; }
                MessageBox.Show(this,
                    "DSHView\n\nWebView2 内核：" + v
                    + "\n数据目录：" + Path.Combine(App.BaseDir, string.IsNullOrWhiteSpace(_s.Data) ? "profile" : _s.Data)
                    + "\n配置文件：" + App.IniPath,
                    "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private string CurrentUrl()
        {
            try { return _view.CoreWebView2.Source; }
            catch { return _s.Url; }
        }

        private void OpenDevTools()
        {
            try { _view.CoreWebView2.OpenDevToolsWindow(); }
            catch (Exception ex) { App.Log("devtools failed: " + ex.Message); }
        }

        private void Reload()
        {
            try { if (_view.CoreWebView2 != null) _view.CoreWebView2.Reload(); } catch { }
        }

        private void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("file://"))
                url = "http://" + url;
            try
            {
                App.Log("navigate " + url);
                _view.CoreWebView2.Navigate(url);
            }
            catch (Exception ex) { App.Log("navigate failed: " + ex.Message); }
        }

        private void SetZoom(double delta)
        {
            if (_view == null) return;
            double z = _view.ZoomFactor + delta;
            if (z < 0.5) z = 0.5;
            if (z > 2.0) z = 2.0;
            _view.ZoomFactor = z;
            if (_zoomLabel != null) _zoomLabel.Text = Math.Round(z * 100).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void SetZoomAbsolute(double z)
        {
            if (_view == null) return;
            _view.ZoomFactor = z;
            if (_zoomLabel != null) _zoomLabel.Text = Math.Round(z * 100).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void SpawnChild(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "--url=\"" + url.Replace("\"", "") + "\"");
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch (Exception ex) { App.Log("spawn failed: " + ex.Message); }
        }

        private static string StateFile()
        {
            return Path.Combine(App.BaseDir, "dshview.state");
        }

        private void LoadLayout()
        {
            try
            {
                string f = StateFile();
                if (!File.Exists(f)) return;
                string[] p = File.ReadAllText(f).Split(',');
                if (p.Length < 5) return;
                Rectangle r = new Rectangle(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
                foreach (Screen s in Screen.AllScreens)
                {
                    if (s.WorkingArea.IntersectsWith(r)) { Bounds = r; break; }
                }
                if (p[4] == "1") WindowState = FormWindowState.Maximized;
            }
            catch { }
        }

        private void SaveLayout()
        {
            try
            {
                Rectangle r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                File.WriteAllText(StateFile(), string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4}", r.X, r.Y, r.Width, r.Height,
                    WindowState == FormWindowState.Maximized ? "1" : "0"));
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tray != null)
                {
                    try { _tray.Visible = false; _tray.Dispose(); } catch { }
                    _tray = null;
                }
                if (_view != null)
                {
                    try { _view.Dispose(); } catch { }
                }
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            App.LogFile = Path.Combine(App.BaseDir, "dshview.log");
            App.S = Settings.Parse(args);
            App.EnsureIni(App.S.Url);
            App.S = Settings.Parse(args);
            App.Log("=== start " + DateTime.Now.ToString("s") + " " + string.Join(" ", args));
            try
            {
                Application.Run(new MainForm(App.S));
            }
            catch (Exception ex)
            {
                App.Log("unhandled: " + ex);
                Environment.ExitCode = 2;
            }
        }
    }
}
