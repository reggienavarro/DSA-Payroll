using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PAYROLL.UI
{
    public class ModernButton : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor { get; set; } = Theme.Accent;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SurroundColor { get; set; } = Theme.Bg; 
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowPlus { get; set; }
        private bool hover, pressed;

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.BodyBold;
            ForeColor = Color.White;
            Height = 38;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(SurroundColor);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Color fill = pressed ? Theme.AccentDark : hover ? Gfx.Lighten(FillColor, 0.12f) : FillColor;
            using (var path = Gfx.Round(rect, Math.Min(Height / 2f, 10f)))
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);

            var sz = TextRenderer.MeasureText(Text, Font);
            int iconW = ShowPlus ? 12 : 0;
            int startX = Math.Max(0, (Width - sz.Width - iconW - 6) / 2);
            if (ShowPlus)
            {
                using (var pen = new Pen(ForeColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    int cy = Height / 2, cx = startX + 5;
                    g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                    g.DrawLine(pen, cx, cy - 5, cx, cy + 5);
                }
            }
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(startX + iconW + 6, 0, sz.Width, Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
        }
    }

    public class IconButton : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IconKind Icon { get; set; } = IconKind.Bell;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color IconColor { get; set; } = Theme.TextGray;
        private bool hover;

        public IconButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(38, 38);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hover)
                using (var b = new SolidBrush(Color.FromArgb(228, 232, 240)))
                using (var p = Gfx.Round(new RectangleF(1, 1, Width - 2, Height - 2), (Height - 2) / 2f))
                    g.FillPath(b, p);
            Icons.Draw(g, Icon, new Rectangle(10, 10, Width - 20, Height - 20), IconColor, 1.6f);
        }
    }
}