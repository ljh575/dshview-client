using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DshView
{
    // Sectioned settings card modelled on the sibling DSH Desktop launcher:
    // label + hint on the left, control on the right, secondary actions bottom
    // left and Cancel / Save / Save and restart bottom right.
    //
    // It never rewrites the whole ini: every changed value goes through
    // IniFile.Set, so the user's comments and ordering survive.
    internal sealed class SettingsDialog : Form
    {
        private const int LabelX = 18;
        private const int LabelW = 210;
        private const int ControlX = 240;
        private const int ControlW = 340;
        private const int RowH = 66;
        private const int HeaderH = 34;

        private readonly Settings _original;
        private readonly Panel _body;

        private readonly TextBox _url = new TextBox();
        private readonly ComboBox _runtime = new ComboBox();
        private readonly CheckBox _secureOrigin = new CheckBox();
        private readonly ComboBox _icon = new ComboBox();
        private readonly ComboBox _zoom = new ComboBox();
        private readonly CheckBox _toolbar = new CheckBox();
        private readonly ComboBox _closeAction = new ComboBox();
        private readonly TextBox _args = new TextBox();
        private readonly TextBox _data = new TextBox();
        private readonly ComboBox _lang = new ComboBox();

        private int _y;

        public SettingsDialog(Settings current)
        {
            _original = current;

            Text = Lang.T("settings.title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 660);
            Font = SystemFonts.MessageBoxFont;
            BackColor = Color.White;

            // Docked bottom footer + scrolling body: the footer keeps its buttons
            // on one row at any dialog height, and the body takes what is left.
            // (The first cut pinned the restart note and the config button to the
            // same corner, so they overlapped once the window got short.)
            _body = new Panel();
            _body.Dock = DockStyle.Fill;
            _body.AutoScroll = true;
            _body.BackColor = Color.White;
            Controls.Add(_body);

            BuildRows();
            BuildFooter();
            LoadValues(current);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Keep the whole card on screen when the display (or the owner window)
            // is short; the body simply scrolls further.
            int available = Screen.FromControl(this).WorkingArea.Height - 80;
            if (Height > available && available >= 420) Height = Math.Max(420, available);
        }

        /// <summary>The edited values, valid when ShowDialog returned OK.</summary>
        public Settings Result { get; private set; }

        /// <summary>True when the user asked to relaunch with the new values.</summary>
        public bool RestartRequested { get; private set; }

        private void BuildRows()
        {
            Section("settings.section.connection");
            Row("settings.url", "settings.url.hint", _url);
            Combo("settings.runtime", "settings.runtime.hint", _runtime);
            Check("settings.secureorigin", "settings.secureorigin.hint", _secureOrigin);

            Section("settings.section.appearance");
            Combo("settings.icon", null, _icon);
            Combo("settings.zoom", "settings.zoom.hint", _zoom);
            Check("settings.toolbar", "settings.toolbar.hint", _toolbar);

            Section("settings.section.behavior");
            Combo("settings.closeaction", "settings.closeaction.hint", _closeAction);
            Combo("settings.lang", "settings.lang.hint", _lang);

            Section("settings.section.advanced");
            Row("settings.args", "settings.args.hint", _args);
            Row("settings.data", "settings.data.hint", _data);
        }

        private void Section(string headerKey)
        {
            Label header = new Label();
            header.Text = Lang.T(headerKey);
            header.Font = new Font(Font.FontFamily, Font.Size + 1.5f, FontStyle.Bold);
            header.ForeColor = Color.FromArgb(17, 24, 39);
            header.AutoSize = false;
            header.Bounds = new Rectangle(LabelX, _y, 560, 24);
            _body.Controls.Add(header);
            _y += HeaderH;
        }

        private void Row(string labelKey, string hintKey, Control control)
        {
            AddLabelAndHint(labelKey, hintKey);
            control.Left = ControlX;
            control.Width = ControlW;
            control.Top = _y + (hintKey == null ? 0 : -4);
            _body.Controls.Add(control);
            _y += RowH;
        }

        private void Combo(string labelKey, string hintKey, ComboBox box)
        {
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            Row(labelKey, hintKey, box);
        }

        private void Check(string labelKey, string hintKey, CheckBox box)
        {
            box.AutoSize = false;
            box.Text = string.Empty;
            box.Bounds = new Rectangle(ControlX, _y, 24, 24);
            box.BackColor = Color.White;
            AddLabelAndHint(labelKey, hintKey);
            _body.Controls.Add(box);
            _y += RowH;
        }

        private void AddLabelAndHint(string labelKey, string hintKey)
        {
            Label label = new Label();
            label.Text = Lang.T(labelKey);
            label.AutoSize = false;
            label.Bounds = new Rectangle(LabelX, _y, LabelW, 20);
            label.ForeColor = Color.FromArgb(17, 24, 39);
            _body.Controls.Add(label);

            if (hintKey == null) return;
            Label hint = new Label();
            hint.Text = Lang.T(hintKey);
            hint.AutoSize = false;
            hint.Bounds = new Rectangle(LabelX, _y + 20, LabelW, 46);
            hint.ForeColor = Color.FromArgb(107, 114, 128);
            _body.Controls.Add(hint);
        }

        private void BuildFooter()
        {
            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 96;
            footer.BackColor = Color.White;
            Controls.Add(footer);

            // The note owns the row above the buttons; the buttons own the last row.
            // Nothing shares coordinates, so a short dialog cannot overlap them.
            Label note = new Label();
            note.Text = Lang.T("settings.restartnote");
            note.AutoSize = false;
            note.Bounds = new Rectangle(LabelX, 6, 584, 40);
            note.ForeColor = Color.FromArgb(107, 114, 128);
            footer.Controls.Add(note);

            int rowY = 58;
            Button openIni = new Button();
            openIni.Text = Lang.T("btn.openini");
            openIni.Bounds = new Rectangle(18, rowY, 130, 28);
            openIni.FlatStyle = FlatStyle.System;
            openIni.Click += delegate { OpenConfig(); };
            footer.Controls.Add(openIni);

            Button cancel = new Button();
            cancel.Text = Lang.T("btn.cancel");
            cancel.Bounds = new Rectangle(300, rowY, 92, 28);
            cancel.FlatStyle = FlatStyle.System;
            cancel.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(cancel);

            Button save = new Button();
            save.Text = Lang.T("settings.save");
            save.Bounds = new Rectangle(398, rowY, 88, 28);
            save.FlatStyle = FlatStyle.System;
            save.Click += delegate { Commit(false); };
            footer.Controls.Add(save);

            Button saveRestart = new Button();
            saveRestart.Text = Lang.T("settings.saverestart");
            saveRestart.Bounds = new Rectangle(492, rowY, 110, 28);
            saveRestart.FlatStyle = FlatStyle.System;
            saveRestart.Click += delegate { Commit(true); };
            footer.Controls.Add(saveRestart);

            AcceptButton = saveRestart;
            CancelButton = cancel;
        }

        private void LoadValues(Settings s)
        {
            _url.Text = s.Url ?? "";

            _runtime.Items.Add(Lang.T("settings.runtime.bundled"));
            _runtime.Items.Add(Lang.T("settings.runtime.system"));
            _runtime.SelectedIndex = (s.Runtime == "system") ? 1 : 0;

            _secureOrigin.Checked = s.SecureOrigin;

            string[] icons = { "default", "auto", "light", "dark" };
            for (int i = 0; i < icons.Length; i++) _icon.Items.Add(Lang.T("settings.icon." + icons[i]));
            _icon.SelectedIndex = Array.IndexOf(icons, s.Icon) < 0 ? 0 : Array.IndexOf(icons, s.Icon);

            string[] zooms = { "0.8", "0.9", "1.0", "1.1", "1.25", "1.5", "2.0" };
            for (int i = 0; i < zooms.Length; i++) _zoom.Items.Add(zooms[i] + "×");
            string current = s.Zoom.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            int idx = Array.IndexOf(zooms, current);
            _zoom.SelectedIndex = idx < 0 ? 2 : idx;

            _toolbar.Checked = s.Toolbar;

            string[] actions = { "ask", "tray", "quit" };
            for (int i = 0; i < actions.Length; i++) _closeAction.Items.Add(Lang.T("settings.closeaction." + actions[i]));
            int a = Array.IndexOf(actions, s.CloseAction);
            _closeAction.SelectedIndex = a < 0 ? 0 : a;

            _args.Text = s.ExtraArgs ?? "";
            _data.Text = s.Data ?? "";

            string[] langs = { "auto", "zh", "en" };
            for (int i = 0; i < langs.Length; i++) _lang.Items.Add(Lang.T("settings.lang." + langs[i]));
            int l = Array.IndexOf(langs, s.UiLang);
            _lang.SelectedIndex = l < 0 ? 0 : l;
        }

        private void Commit(bool restart)
        {
            string[] icons = { "default", "auto", "light", "dark" };
            string[] zooms = { "0.8", "0.9", "1.0", "1.1", "1.25", "1.5", "2.0" };
            string[] actions = { "ask", "tray", "quit" };
            string[] langs = { "auto", "zh", "en" };

            Settings s = new Settings();
            s.Url = _url.Text.Trim();
            s.Runtime = _runtime.SelectedIndex == 1 ? "system" : "webview2";
            s.SecureOrigin = _secureOrigin.Checked;
            s.Icon = icons[_icon.SelectedIndex < 0 ? 0 : _icon.SelectedIndex];
            s.Zoom = double.Parse(zooms[_zoom.SelectedIndex < 0 ? 2 : _zoom.SelectedIndex],
                System.Globalization.CultureInfo.InvariantCulture);
            s.Toolbar = _toolbar.Checked;
            s.CloseAction = actions[_closeAction.SelectedIndex < 0 ? 0 : _closeAction.SelectedIndex];
            s.ExtraArgs = _args.Text.Trim();
            s.Data = _data.Text.Trim();
            s.UiLang = langs[_lang.SelectedIndex < 0 ? 0 : _lang.SelectedIndex];

            int written = 0;
            if (s.Url != _original.Url) written += Save("url", s.Url);
            if (s.Runtime != _original.Runtime) written += Save("runtime", s.Runtime);
            if (s.Data != _original.Data) written += Save("data", s.Data);
            if (Math.Abs(s.Zoom - _original.Zoom) > 0.0001) written += Save("zoom", zooms[_zoom.SelectedIndex]);
            if (s.ExtraArgs != _original.ExtraArgs) written += Save("args", s.ExtraArgs);
            if (s.Toolbar != _original.Toolbar) written += Save("toolbar", s.Toolbar ? "1" : "0");
            if (s.Icon != _original.Icon) written += Save("icon", s.Icon);
            if (s.CloseAction != _original.CloseAction) written += Save("closeaction", s.CloseAction);
            if (s.SecureOrigin != _original.SecureOrigin) written += Save("secureorigin", s.SecureOrigin ? "1" : "0");
            if (s.UiLang != _original.UiLang) written += Save("lang", s.UiLang);

            App.Log("settings: " + written + " value(s) written");
            Result = s;
            RestartRequested = restart;
            DialogResult = DialogResult.OK;
            Close();
        }

        private int Save(string key, string value)
        {
            return IniFile.Set(App.IniPath, key, value) ? 1 : 0;
        }

        private void OpenConfig()
        {
            try
            {
                if (!File.Exists(App.IniPath)) File.WriteAllText(App.IniPath, "", System.Text.Encoding.UTF8);
                Process.Start(new ProcessStartInfo(App.IniPath) { UseShellExecute = true });
            }
            catch (Exception ex) { App.Log("open config failed: " + ex.Message); }
        }
    }
}
