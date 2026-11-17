using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace DshView
{
    // Read-only facts about this build, in the same card style as the settings
    // dialog. Copy puts the whole block on the clipboard so a user can paste it
    // into a bug report without retyping paths.
    internal sealed class AboutDialog : Form
    {
        private const int LabelX = 18;
        private const int ValueX = 150;
        private const int RowH = 30;

        private readonly Settings _s;
        private readonly StringBuilder _clipboard = new StringBuilder();
        private int _y = 18;

        public AboutDialog(Settings settings)
        {
            _s = settings;

            Text = Lang.T("about.title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 420);
            Font = SystemFonts.MessageBoxFont;
            BackColor = Color.White;

            Label title = new Label();
            title.Text = Lang.T("app.name");
            title.Font = new Font(Font.FontFamily, Font.Size + 3f, FontStyle.Bold);
            title.AutoSize = false;
            title.Bounds = new Rectangle(LabelX, _y, 500, 30);
            Controls.Add(title);
            _y += 38;

            Row("about.version", Version());
            Row("about.webview2", WebViewVersion());
            Row("about.runtime", _s.Runtime == "system" ? Lang.T("about.runtime.system") : Lang.T("about.runtime.bundled"));
            Row("about.datadir", Path.Combine(App.BaseDir, string.IsNullOrWhiteSpace(_s.Data) ? "profile" : _s.Data));
            Row("about.inifile", App.IniPath);
            Row("about.logfile", App.LogFile);
            Row("about.project", "https://github.com/ljh575/dshview-client");
            Row("about.license", "MIT");

            Label disclaimer = new Label();
            disclaimer.Text = Lang.T("about.disclaimer");
            disclaimer.AutoSize = false;
            disclaimer.Bounds = new Rectangle(LabelX, _y + 6, 520, 22);
            disclaimer.ForeColor = Color.FromArgb(107, 114, 128);
            Controls.Add(disclaimer);

            Button copy = new Button();
            copy.Text = Lang.T("about.copy");
            copy.Bounds = new Rectangle(330, 380, 100, 28);
            copy.FlatStyle = FlatStyle.System;
            copy.Click += delegate { CopyInfo(); };
            Controls.Add(copy);

            Button close = new Button();
            close.Text = Lang.T("btn.close");
            close.Bounds = new Rectangle(440, 380, 100, 28);
            close.FlatStyle = FlatStyle.System;
            close.DialogResult = DialogResult.OK;
            Controls.Add(close);

            AcceptButton = close;
            CancelButton = close;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            int available = Screen.FromControl(this).WorkingArea.Height - 80;
            if (Height > available && available >= 320) Height = Math.Max(320, available);
        }

        private void Row(string labelKey, string value)
        {
            Label label = new Label();
            label.Text = Lang.T(labelKey);
            label.AutoSize = false;
            label.Bounds = new Rectangle(LabelX, _y, 128, 22);
            label.ForeColor = Color.FromArgb(107, 114, 128);
            Controls.Add(label);

            Label text = new Label();
            text.Text = value;
            text.AutoSize = false;
            text.Bounds = new Rectangle(ValueX, _y, 520 - ValueX, 22);
            text.ForeColor = Color.FromArgb(17, 24, 39);
            Controls.Add(text);

            _clipboard.AppendLine(Lang.T(labelKey) + ": " + value);
            _y += RowH;
        }

        private static string Version()
        {
            try { return FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion; }
            catch { return "?"; }
        }

        private string WebViewVersion()
        {
            try { return CoreWebView2Environment.GetAvailableBrowserVersionString(App.ResolveRuntime(_s.Runtime)); }
            catch { return "-"; }
        }

        private void CopyInfo()
        {
            try
            {
                Clipboard.SetText(_clipboard.ToString());
                App.Log("about: info copied");
            }
            catch (Exception ex) { App.Log("about copy failed: " + ex.Message); }
        }
    }
}
