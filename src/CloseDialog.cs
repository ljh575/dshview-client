using System;
using System.Drawing;
using System.Windows.Forms;

namespace DshView
{
    // Shown when the window is closed while closetotray=0. Instead of quitting
    // straight away it asks which of the two behaviours this close should use:
    // park the window in the notification area (the DSH session survives) or
    // quit. Yes = tray, No = quit, Cancel/Esc = leave the window open.
    //
    // Kept free of App/MainForm references so it can be rendered on its own.
    internal sealed class CloseDialog : Form
    {
        private readonly Button _tray;
        private readonly Button _quit;
        private readonly Button _cancel;

        public CloseDialog()
        {
            Text = "关闭 DeepSeek Harness";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(480, 200);
            Font = SystemFonts.MessageBoxFont;
            BackColor = Color.White;

            Label message = new Label();
            message.UseMnemonic = false;
            message.AutoSize = false;
            message.Bounds = new Rectangle(18, 16, 444, 96);
            message.Text =
                "要把窗口最小化到托盘，还是直接退出？" + Environment.NewLine +
                Environment.NewLine +
                "最小化到托盘：进程继续运行，会话不会中断，双击托盘图标即可恢复。" + Environment.NewLine +
                "直接退出：关闭本程序。";
            Controls.Add(message);

            _tray = MakeButton("最小化到托盘", 18, 152, 120, DialogResult.Yes);
            _quit = MakeButton("直接退出", 146, 152, 100, DialogResult.No);
            _cancel = MakeButton("取消", 374, 152, 88, DialogResult.Cancel);

            AcceptButton = _tray;
            CancelButton = _cancel;
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
