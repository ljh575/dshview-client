using System;
using System.Drawing;
using System.Windows.Forms;

namespace DshView
{
    // Shown when the window is closed while closeaction=ask. Instead of deciding
    // for the user it asks which behaviour this close should use: park the window
    // in the notification area (the DSH session survives) or quit. Yes = tray,
    // No = quit, Cancel/Esc = leave the window open. The checkbox writes the
    // answer back as closeaction= so the question is asked only once.
    //
    // Kept free of App/MainForm references so it can be rendered on its own.
    internal sealed class CloseDialog : Form
    {
        private readonly CheckBox _remember;

        public CloseDialog()
        {
            Text = Lang.T("close.title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(480, 236);
            Font = SystemFonts.MessageBoxFont;
            BackColor = Color.White;

            Label message = new Label();
            message.UseMnemonic = false;
            message.AutoSize = false;
            message.Bounds = new Rectangle(18, 16, 444, 84);
            message.Text = Lang.T("close.head") + Environment.NewLine +
                Environment.NewLine +
                Lang.T("close.trayhint") + Environment.NewLine +
                Lang.T("close.quithint");
            Controls.Add(message);

            _remember = new CheckBox();
            _remember.Text = Lang.T("close.remember");
            _remember.AutoSize = false;
            _remember.Bounds = new Rectangle(18, 108, 320, 24);
            _remember.BackColor = Color.White;
            Controls.Add(_remember);

            Button tray = MakeButton(Lang.T("close.tray"), 18, 188, 152, DialogResult.Yes);
            Button quit = MakeButton(Lang.T("close.quit"), 178, 188, 100, DialogResult.No);
            Button cancel = MakeButton(Lang.T("close.cancel"), 374, 188, 88, DialogResult.Cancel);

            AcceptButton = tray;
            CancelButton = cancel;
        }

        /// <summary>True when the user asked to stop being prompted.</summary>
        public bool RememberChoice
        {
            get { return _remember.Checked; }
        }

        private Button MakeButton(string text, int x, int y, int width, DialogResult result)
        {
            Button b = new Button();
            b.Text = text;
            b.Bounds = new Rectangle(x, y, width, 30);
            b.DialogResult = result;
            b.FlatStyle = FlatStyle.System;
            Controls.Add(b);
            return b;
        }
    }
}
