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
                sb.AppendLine("# DSHView 配置文件（本文件由程序自动生成，可随意编辑）");
                sb.AppendLine("# 改完保存，重新打开 DSHView.exe 生效。");
                sb.AppendLine();
                sb.AppendLine("# 【按需改】DSH Web 地址。默认值是 dsh web 自己的默认地址（本机场景）。");
                sb.AppendLine("# 如果 dsh web 在另一台机器上，这里要写那台机器的 IP，例如：");
                sb.AppendLine("#   url=http://10.0.0.123:3080/");
                sb.AppendLine("# 首次访问（或换端口/换机器）时，把 dsh web 打印出来的 ?token=... 一起带上；");
                sb.AppendLine("# 换到 cookie 之后浏览器已有 30 天有效期的签名 cookie，直接填纯地址即可。");
                sb.AppendLine("url=" + defaultUrl);
                sb.AppendLine();
                sb.AppendLine("# 内核位置：webview2 = 用本目录自带的固定版运行时；system = 用系统已装的那份");
                sb.AppendLine("runtime=webview2");
                sb.AppendLine();
                sb.AppendLine("# 浏览器数据目录：保存 token、登录态。删掉它就等于清空登录。");
                sb.AppendLine("data=profile");
                sb.AppendLine();
                sb.AppendLine("# 初始缩放比例");
                sb.AppendLine("zoom=1.0");
                sb.AppendLine();
                sb.AppendLine("# 是否显示地址栏：0 = 纯内容窗口（默认，像 DSH-Portable）；1 = 带地址栏");
                sb.AppendLine("toolbar=0");
                sb.AppendLine();
                sb.AppendLine("# 图标样式（四档）：");
                sb.AppendLine("#   default = DSH 官方图标（默认，带圆角底，任何背景都看得清）");
                sb.AppendLine("#   auto    = 跟随系统主题（浅色用黑鲸、深色用白鲸）");
                sb.AppendLine("#   light   = 始终黑鲸（适合浅色标题栏）   dark = 始终白鲸（适合深色标题栏）");
                sb.AppendLine("icon=default");
                sb.AppendLine();
                sb.AppendLine("# 追加给内核的参数（空格分隔），留空即可。");
                sb.AppendLine("args=");
                sb.AppendLine();
                sb.AppendLine("# 把内网 http 地址提升为安全上下文，剪贴板等 API 才能用；套了 HTTPS 可设 0。");
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

            FormClosing += delegate { SaveLayout(); };
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
            if (disposing && _view != null)
            {
                try { _view.Dispose(); } catch { }
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
