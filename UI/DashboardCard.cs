using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PAYROLL.UI
{
    public class DashboardCard : RoundedPanel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IconKind Icon { get; set; } = IconKind.Calendar;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowMiniBars { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Badge { get; set; } = "";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Font ValueFont { get; set; } = Theme.ValueBig;

        private readonly Label titleLabel, valueLabel, subLabel;

        public DashboardCard()
        {
            Padding = new Padding(18, 16, 18, 14);
            titleLabel = new Label { AutoSize = true, BackColor = Color.White, ForeColor = Theme.TextGray, Font = Theme.Small };
            valueLabel = new Label { AutoSize = true, BackColor = Color.White, ForeColor = Theme.TextDark, Font = Theme.ValueBig };
            subLabel   = new Label { AutoSize = true, BackColor = Color.White, ForeColor = Theme.TextGray, Font = Theme.Small };
            Controls.Add(titleLabel);
            Controls.Add(valueLabel);
            Controls.Add(subLabel);
            PlaceLabels();
            Resize += (s, e) => PlaceLabels();
        }

        private void PlaceLabels()
        {
            titleLabel.Location = new Point(18, 16);
            valueLabel.Location = new Point(18, 40);
            subLabel.Location   = new Point(18, 68);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { set { titleLabel.Text = value; } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Value { set { valueLabel.Text = value; valueLabel.Font = ValueFont; } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Sub   { set { subLabel.Text = value; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); // draws the white rounded card first
            var g = e.Graphics;

            // Icon in a soft blue rounded square (top-right, like the reference)
            var box = new Rectangle(Width - 58, 12, 40, 40);
            using (var path = Gfx.Round(new RectangleF(box.X, box.Y, box.Width, box.Height), 10))
            using (var b = new SolidBrush(Theme.AccentSoft)) g.FillPath(b, path);
            Icons.Draw(g, Icon, new Rectangle(box.X + 11, box.Y + 11, 18, 18), Theme.Accent, 1.7f);

            if (ShowMiniBars) // decorative mini bar chart, bottom-right
            {
                int[] bars = { 9, 15, 11, 19, 13, 22 };
                int x = Width - Padding.Right - 66, baseY = Height - 16;
                foreach (int h in bars)
                {
                    using (var b = new SolidBrush(h == bars[bars.Length - 1] ? Theme.Accent : Color.FromArgb(191, 214, 253)))
                    using (var p = Gfx.Round(new RectangleF(x, baseY - h, 7, h), 3)) g.FillPath(b, p);
                    x += 10;
                }
            }

            if (!string.IsNullOrEmpty(Badge))
            {
                var sz = TextRenderer.MeasureText(Badge, Theme.SmallBold);
                var rect = new RectangleF(18, Height - 33, sz.Width + 16, 20);
                using (var path = Gfx.Round(rect, 9))
                using (var b = new SolidBrush(Theme.GreenBg)) g.FillPath(b, path);
                TextRenderer.DrawText(g, Badge, Theme.SmallBold, Rectangle.Round(rect), Theme.GreenText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}