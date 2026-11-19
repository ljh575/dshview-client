using System;
using System.Drawing;
using System.Windows.Forms;

namespace DshView
{
    // A flat, glyph-only title-bar button (minimize/maximize/close), drawn by us so
    // the custom title bar recolours instantly. The close button turns red on hover.
    internal sealed class ChromeButton : Control
    {
        /// <summary>Whether the title bar is in dark mode; drives glyph colours.</summary>
        public static bool Dark;

        private string _glyph;
        private bool _hover;
        private bool _pressed;
        public bool IsClose;

        public string Glyph { get { return _glyph; } set { _glyph = value; Invalidate(); } }

        public ChromeButton(string glyph)
        {
            _glyph = glyph;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
            TabStop = false;
            Font = new Font("Segoe UI Symbol", 9f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color bg, fg;
            if (IsClose && _hover)
            {
                bg = _pressed ? Color.FromArgb(180, 20, 25) : Color.FromArgb(232, 17, 35);
                fg = Color.White;
            }
            else
            {
                bg = _hover ? (Dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(229, 229, 229)) : Color.Transparent;
                fg = Dark ? Color.White : Color.Black;
            }
            if (bg != Color.Transparent)
                using (SolidBrush b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, _glyph, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    }
}
