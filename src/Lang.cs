using System;
using System.Collections.Generic;

namespace DshView
{
    // Two-language string table. The active language follows the dsh web UI:
    // @deepseek-ai/dsh-client-locale mirrors its locale into <html lang>
    // ("zh-CN" for Chinese, the locale id otherwise), and MainForm reads that
    // with ExecuteScriptAsync after a page loads. lang= in the ini can pin a
    // language instead (auto | zh | en).
    //
    // Keys missing from a table fall back to the other table and finally to the
    // key itself, so a half-translated build still renders something usable.
    internal static class Lang
    {
        /// <summary>"zh" or "en"; the whole UI reads this.</summary>
        public static string Current = "zh";

        /// <summary>Maps an <html lang> value to a supported language, or null when it is unusable.</summary>
        public static string Normalize(string tag)
        {
            string v = (tag ?? "").Trim().Trim('"').ToLowerInvariant();
            if (v.Length == 0) return null;
            if (v.StartsWith("zh")) return "zh";
            if (v.StartsWith("en")) return "en";
            // dsh also ships ja/es/fr/de/ru/pt; those fall back to English here.
            return "en";
        }

        public static string T(string key)
        {
            Dictionary<string, string> table = Current == "en" ? En : Zh;
            string s;
            if (table.TryGetValue(key, out s)) return s;
            if (Zh.TryGetValue(key, out s)) return s;
            if (En.TryGetValue(key, out s)) return s;
            return key;
        }

        /// <summary>T() with {0}/{1} substitution; a bad format keeps the raw text.</summary>
        public static string F(string key, params object[] args)
        {
            string s = T(key);
            try { return string.Format(s, args); }
            catch { return s; }
        }

        /// <summary>Every key, for the coverage test.</summary>
        public static IEnumerable<string> Keys
        {
            get
            {
                List<string> keys = new List<string>();
                foreach (KeyValuePair<string, string> kv in Zh) keys.Add(kv.Key);
                return keys;
            }
        }

        public static bool Has(string key, string language)
        {
            return (language == "en" ? En : Zh).ContainsKey(key);
        }

        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
        {
            // ---- tray ----
            { "app.name", "DSHView" },
            { "tray.show", "显示窗口" },
            { "tray.settings", "设置…" },
            { "tray.about", "关于" },
            { "tray.copyurl", "复制当前地址" },
            { "tray.devtools", "开发者工具 (F12)" },
            { "tray.browser", "在系统浏览器中打开" },
            { "tray.quit", "退出" },
            { "tray.hint", "已最小化到托盘。双击图标可恢复，右键菜单可退出。" },
            { "toolbar.back", "后退 (Alt+左)" },
            { "toolbar.forward", "前进 (Alt+右)" },
            { "toolbar.reload", "刷新 (F5)" },
            { "toolbar.home", "回到起始地址" },
            { "toolbar.zoomout", "缩小 (Ctrl+-)" },
            { "toolbar.zoomin", "放大 (Ctrl++)" },
            { "toolbar.more", "更多" },

            // ---- close prompt ----
            { "close.title", "关闭 DSHView" },
            { "close.head", "要把窗口最小化到托盘，还是直接退出？" },
            { "close.trayhint", "最小化到托盘：进程继续运行，会话不会中断，双击托盘图标即可恢复。" },
            { "close.quithint", "直接退出：关闭本程序。" },
            { "close.remember", "记住我的选择（以后不再询问）" },
            { "close.tray", "最小化到托盘" },
            { "close.quit", "直接退出" },
            { "close.cancel", "取消" },

            // ---- shared buttons ----
            { "btn.ok", "确定" },
            { "btn.cancel", "取消" },
            { "btn.close", "关闭" },
            { "btn.browse", "浏览…" },
            { "btn.openini", "打开配置文件" },

            // ---- messages ----
            { "msg.loadfail.title", "页面没有加载成功" },
            { "msg.loadfail.body", "当前地址：{0}\n\n如果 dsh web 在另一台机器上，这里不能写 127.0.0.1（那指向本机自己），要写服务器的 IPv4 地址，例如 http://10.0.0.123:3080/ 。\n服务器那边还必须用同一地址监听：dsh web --host 10.0.0.123 --port 3080（--host 0.0.0.0 不受支持）。\n\n如果以前成功打开过，先按 F5 刷新（登录凭据有 30 天有效期）。\n如果提示未认证，把 dsh web 打印的 ?token=... 地址填进 ini 的 url 再刷新：\n{1}" },
            { "msg.processfail.body", "浏览器进程异常退出，请重新打开本程序。\n详见 dshview.log。" },
            { "msg.runtimefail.title", "WebView2 运行时初始化失败" },
            { "msg.controlfail.title", "WebView2 控件初始化失败" },
            { "msg.datadirfail.title", "无法创建数据目录" },
            { "msg.runtimefail.notfound.head", "程序没有找到自带的 webview2 运行时，本机也没有安装系统 WebView2。" },
            { "msg.runtimefail.notfound.tried", "已尝试这些目录（需包含 msedgewebview2.exe）：" },
            { "msg.runtimefail.notfound.fix", "请把完整的 webview2 目录放到 DSHView.exe 旁边；或者在本机安装一次 WebView2 运行时，再把 dshview.ini 的 runtime 设为 system。" },
            { "msg.runtimefail.found", "运行时目录已找到：{0}\n但内核没能启动。常见原因：目录不完整（缺 msedge.dll 或版本号.manifest 文件）、被杀毒软件隔离、或与另一份 WebView2 冲突。" },
            { "msg.detail.hint", "详见 dshview.log。" },

            // ---- settings dialog ----
            { "settings.title", "设置" },
            { "settings.section.connection", "连接" },
            { "settings.section.appearance", "外观" },
            { "settings.section.behavior", "行为" },
            { "settings.section.advanced", "高级" },
            { "settings.url", "地址" },
            { "settings.url.hint", "首次或换端口时带上 ?token=…，认证后只写纯地址" },
            { "settings.runtime", "内核" },
            { "settings.runtime.hint", "自带内核无需在目标机安装 WebView2" },
            { "settings.runtime.bundled", "自带 webview2" },
            { "settings.runtime.system", "系统已安装" },
            { "settings.secureorigin", "内网 http 视为安全上下文" },
            { "settings.secureorigin.hint", "剪贴板 API 需要；套了 HTTPS 可关掉" },
            { "settings.icon", "窗口图标" },
            { "settings.icon.default", "官方图标" },
            { "settings.icon.auto", "跟随系统主题" },
            { "settings.icon.light", "黑鲸" },
            { "settings.icon.dark", "白鲸" },
            { "settings.zoom", "初始缩放" },
            { "settings.zoom.hint", "下次启动生效" },
            { "settings.toolbar", "显示地址栏" },
            { "settings.toolbar.hint", "默认纯内容窗口，开启后有前进/刷新/缩放按钮" },
            { "settings.closeaction", "关闭按钮" },
            { "settings.closeaction.hint", "询问框里勾选「记住我的选择」也会改写这项" },
            { "settings.closeaction.ask", "每次询问" },
            { "settings.closeaction.tray", "最小化到托盘" },
            { "settings.closeaction.quit", "直接退出" },
            { "settings.args", "附加内核参数" },
            { "settings.args.hint", "空格分隔，通常留空；排查时可用 --no-sandbox" },
            { "settings.data", "浏览器数据目录" },
            { "settings.data.hint", "保存 cookie 与登录态，删掉等于清空登录" },
            { "settings.lang", "界面语言" },
            { "settings.lang.hint", "跟随 DSH 网页的语言（读取页面的 html lang）" },
            { "settings.lang.auto", "跟随 DSH 网页" },
            { "settings.lang.zh", "中文" },
            { "settings.lang.en", "English" },
            { "settings.save", "保存" },
            { "settings.saverestart", "保存并重启" },
            { "settings.restartnote", "地址 / 内核 / 数据目录 / 缩放改动需要重启才生效。" },
            { "settings.savefailed", "配置写入失败，详见 dshview.log。" },

            // ---- about ----
            { "about.title", "关于 DSHView" },
            { "about.version", "版本" },
            { "about.webview2", "WebView2 内核" },
            { "about.runtime", "内核来源" },
            { "about.runtime.bundled", "自带" },
            { "about.runtime.system", "系统已安装" },
            { "about.datadir", "数据目录" },
            { "about.inifile", "配置文件" },
            { "about.logfile", "日志文件" },
            { "about.project", "项目地址" },
            { "about.license", "许可" },
            { "about.disclaimer", "本项目与 DeepSeek 官方无关。" },
            { "about.copy", "复制信息" },
            { "about.copied", "已复制到剪贴板。" },
        };

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            // ---- tray ----
            { "app.name", "DSHView" },
            { "tray.show", "Show window" },
            { "tray.settings", "Settings…" },
            { "tray.about", "About" },
            { "tray.copyurl", "Copy current address" },
            { "tray.devtools", "Developer tools (F12)" },
            { "tray.browser", "Open in system browser" },
            { "tray.quit", "Quit" },
            { "tray.hint", "Minimized to the tray. Double-click the icon to restore, right-click for the menu." },
            { "toolbar.back", "Back (Alt+Left)" },
            { "toolbar.forward", "Forward (Alt+Right)" },
            { "toolbar.reload", "Reload (F5)" },
            { "toolbar.home", "Go to the start address" },
            { "toolbar.zoomout", "Zoom out (Ctrl+-)" },
            { "toolbar.zoomin", "Zoom in (Ctrl++)" },
            { "toolbar.more", "More" },

            // ---- close prompt ----
            { "close.title", "Close DSHView" },
            { "close.head", "Minimize the window to the tray, or quit?" },
            { "close.trayhint", "Minimize to tray: the process keeps running, the session survives, and double-clicking the tray icon restores it." },
            { "close.quithint", "Quit: closes this application." },
            { "close.remember", "Remember my choice (do not ask again)" },
            { "close.tray", "Minimize to tray" },
            { "close.quit", "Quit" },
            { "close.cancel", "Cancel" },

            // ---- shared buttons ----
            { "btn.ok", "OK" },
            { "btn.cancel", "Cancel" },
            { "btn.close", "Close" },
            { "btn.browse", "Browse…" },
            { "btn.openini", "Open config file" },

            // ---- messages ----
            { "msg.loadfail.title", "The page did not load" },
            { "msg.loadfail.body", "Current address: {0}\n\nIf dsh web runs on another machine, 127.0.0.1 will not work here (it points at this machine); use the server's IPv4 address instead, e.g. http://10.0.0.123:3080/ .\nThe server must also listen on that same address: dsh web --host 10.0.0.123 --port 3080 (--host 0.0.0.0 is not supported).\n\nIf it worked before, press F5 to reload first (the login cookie lasts 30 days).\nIf it says you are not authenticated, paste the ?token=... address printed by dsh web into url in this file and reload:\n{1}" },
            { "msg.processfail.body", "The browser process exited unexpectedly. Please reopen this application.\nSee dshview.log." },
            { "msg.runtimefail.title", "WebView2 runtime initialisation failed" },
            { "msg.controlfail.title", "WebView2 control initialisation failed" },
            { "msg.datadirfail.title", "Could not create the data directory" },
            { "msg.runtimefail.notfound.head", "No bundled webview2 runtime was found, and this machine has no system WebView2 installed." },
            { "msg.runtimefail.notfound.tried", "Tried these folders (each must contain msedgewebview2.exe):" },
            { "msg.runtimefail.notfound.fix", "Put a complete webview2 folder next to DSHView.exe, or install the WebView2 runtime on this machine and set runtime=system in dshview.ini." },
            { "msg.runtimefail.found", "The runtime folder was found at {0}\nbut the engine did not start. Common causes: an incomplete folder (missing msedge.dll or the version .manifest), a security product quarantining it, or a conflict with another WebView2 install." },
            { "msg.detail.hint", "See dshview.log." },

            // ---- settings dialog ----
            { "settings.title", "Settings" },
            { "settings.section.connection", "Connection" },
            { "settings.section.appearance", "Appearance" },
            { "settings.section.behavior", "Behaviour" },
            { "settings.section.advanced", "Advanced" },
            { "settings.url", "Address" },
            { "settings.url.hint", "First run or a new port needs ?token=…; afterwards the plain address is enough" },
            { "settings.runtime", "Runtime" },
            { "settings.runtime.hint", "The bundled runtime needs no WebView2 on the target machine" },
            { "settings.runtime.bundled", "Bundled webview2" },
            { "settings.runtime.system", "Installed on this machine" },
            { "settings.secureorigin", "Treat intranet http as a secure context" },
            { "settings.secureorigin.hint", "Needed for the clipboard API; turn off when served over HTTPS" },
            { "settings.icon", "Window icon" },
            { "settings.icon.default", "Official artwork" },
            { "settings.icon.auto", "Follow system theme" },
            { "settings.icon.light", "Dark whale" },
            { "settings.icon.dark", "White whale" },
            { "settings.zoom", "Initial zoom" },
            { "settings.zoom.hint", "Applies on the next launch" },
            { "settings.toolbar", "Show the address bar" },
            { "settings.toolbar.hint", "Default is a content-only window; enables back/reload/zoom buttons" },
            { "settings.closeaction", "Close button" },
            { "settings.closeaction.hint", "\"Remember my choice\" in the prompt writes this too" },
            { "settings.closeaction.ask", "Ask every time" },
            { "settings.closeaction.tray", "Minimize to tray" },
            { "settings.closeaction.quit", "Quit" },
            { "settings.args", "Extra Chromium arguments" },
            { "settings.args.hint", "Space separated, usually empty; --no-sandbox helps when diagnosing" },
            { "settings.data", "Browser data directory" },
            { "settings.data.hint", "Holds cookies and login state; deleting it clears the login" },
            { "settings.lang", "UI language" },
            { "settings.lang.hint", "Follows the DSH web UI by reading the page's html lang" },
            { "settings.lang.auto", "Follow DSH web" },
            { "settings.lang.zh", "中文" },
            { "settings.lang.en", "English" },
            { "settings.save", "Save" },
            { "settings.saverestart", "Save and restart" },
            { "settings.restartnote", "Address / runtime / data directory / zoom changes apply after a restart." },
            { "settings.savefailed", "Could not write the config file; see dshview.log." },

            // ---- about ----
            { "about.title", "About DSHView" },
            { "about.version", "Version" },
            { "about.webview2", "WebView2 runtime" },
            { "about.runtime", "Runtime source" },
            { "about.runtime.bundled", "Bundled" },
            { "about.runtime.system", "Installed on this machine" },
            { "about.datadir", "Data directory" },
            { "about.inifile", "Config file" },
            { "about.logfile", "Log file" },
            { "about.project", "Project" },
            { "about.license", "License" },
            { "about.disclaimer", "This project is not affiliated with DeepSeek." },
            { "about.copy", "Copy info" },
            { "about.copied", "Copied to the clipboard." },
        };
    }
}
