using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PAYROLL.UI
{
    public class SidebarButton : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IconKind Icon { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Active { get; set; }
        private bool hover;

        public SidebarButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 46;
            Cursor = Cursors.Hand;
            Font = Theme.Body;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Navy); // same color as the sidebar behind it

            var highlight = new RectangleF(10, 5, Width - 20, Height - 10);
            if (Active)
                using (var b = new SolidBrush(Theme.Accent))
                using (var p = Gfx.Round(highlight, 9)) g.FillPath(b, p);
            else if (hover)
                using (var b = new SolidBrush(Theme.NavyHover))
                using (var p = Gfx.Round(highlight, 9)) g.FillPath(b, p);

            Icons.Draw(g, Icon, new Rectangle(20, (Height - 18) / 2, 18, 18),
                Active ? Color.White : Theme.SidebarText, 1.7f);

            var textColor = Active ? Color.White : hover ? Color.FromArgb(226, 232, 240) : Theme.SidebarText;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(48, 0, Width - 52, Height),
                textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}