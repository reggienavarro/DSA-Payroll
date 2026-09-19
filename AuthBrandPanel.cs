using System.Drawing;
using System.Windows.Forms;

namespace PAYROLL.UI
{
    public static class AuthBrandPanel
    {
        public static Panel Build(string tagline, (IconKind icon, string text)[] bullets)
        {
            var left = new Panel { Dock = DockStyle.Left, Width = 400, BackColor = Theme.Navy };

            var logoBox = new Panel { Size = new Size(56, 56), Location = new Point(48, 56), BackColor = Theme.Navy };
            logoBox.Paint += (s, e) =>
            {
                using var path = Gfx.Round(new RectangleF(0, 0, 56, 56), 14);
                using var b = new SolidBrush(Theme.Accent);
                e.Graphics.FillPath(b, path);
                Icons.Draw(e.Graphics, IconKind.Building, new Rectangle(14, 14, 28, 28), Color.White, 1.8f);
            };

            var brand = new Label
            {
                Text = "PAYROLL SYSTEM",
                Font = new Font("Segoe UI Semibold", 16f),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(48, 130),
                BackColor = Theme.Navy
            };

            var taglineLabel = new Label
            {
                Text = tagline,
                Font = Theme.Body,
                ForeColor = Theme.SidebarText,
                Size = new Size(300, 44),
                Location = new Point(48, 168),
                BackColor = Theme.Navy
            };

            left.Controls.Add(logoBox);
            left.Controls.Add(brand);
            left.Controls.Add(taglineLabel);

            int fy = 268;
            foreach (var f in bullets)
            {
                var iconHost = new Panel { Location = new Point(48, fy), Size = new Size(32, 32), BackColor = Theme.Navy };
                iconHost.Paint += (s, e) =>
                {
                    using var path = Gfx.Round(new RectangleF(0, 0, 32, 32), 8);
                    using var b = new SolidBrush(Theme.NavyHover);
                    e.Graphics.FillPath(b, path);
                    Icons.Draw(e.Graphics, f.icon, new Rectangle(7, 7, 18, 18), Theme.Accent, 1.6f);
                };
                var label = new Label
                {
                    Text = f.text,
                    Font = Theme.Body,
                    ForeColor = Color.FromArgb(226, 232, 240),
                    Location = new Point(92, fy + 6),
                    Size = new Size(260, 40),
                    BackColor = Theme.Navy
                };
                left.Controls.Add(iconHost);
                left.Controls.Add(label);
                fy += 62;
            }

            return left;
        }
    }
}