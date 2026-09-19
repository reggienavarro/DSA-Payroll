using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace PAYROLL.UI
{
    public class PayrollSummaryChart : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "Payroll Summary";
        private readonly List<(string Label, decimal Value, Color Color)> slices = new List<(string, decimal, Color)>();

        public PayrollSummaryChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetData(List<(string, decimal)>? rows)
        {
            slices.Clear();
            foreach (var r in rows ?? new List<(string, decimal)>())
                slices.Add((r.Item1, r.Item2, ColorFor(r.Item1)));
            Invalidate();
        }

        private static Color ColorFor(string label)
        {
            string s = (label ?? "").ToLowerInvariant();
            // future support if you ever add a Status column:
            if (s.Contains("unpaid") || s.Contains("overdue") || s.Contains("fail")) return Color.FromArgb(239, 68, 68);
            if (s.Contains("paid"))  return Theme.Accent;
            if (s.Contains("pend"))  return Theme.Amber;
            // the three real slices used today:
            if (s.Contains("net"))      return Theme.Accent;   
            if (s.Contains("overtime")) return Theme.Amber;    
            return Color.FromArgb(148, 163, 184);              
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.Clear(Theme.Bg);
            using (var card = Gfx.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 14))
            {
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, card);
                using (var p = new Pen(Theme.CardBorder)) g.DrawPath(p, card);
            }
            TextRenderer.DrawText(g, Title, Theme.H2, new Point(20, 16), Theme.TextDark);

            decimal total = slices.Sum(s => s.Value);
            int dia = Math.Max(90, Math.Min(Height - 110, Width / 2 - 100));
            var donut = new Rectangle(28, (Height - dia) / 2 + 6, dia, dia);

            if (total > 0)
            {
                float start = -90;
                foreach (var s in slices)
                {
                    float sweep = (float)(360m * s.Value / total);
                    if (sweep <= 0) continue;
                    using (var b = new SolidBrush(s.Color)) g.FillPie(b, donut, start, sweep);
                    start += sweep;
                }
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(233, 236, 242))) g.FillEllipse(b, donut);
            }

            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, donut.X + 28, donut.Y + 28, dia - 56, dia - 56);

            if (total > 0 && slices.Count > 0)
            {
                var top = slices.OrderByDescending(s => s.Value).First();
                int pct = (int)Math.Round(100 * (double)(top.Value / total));
                var font = new Font("Segoe UI Semibold", 13f);
                var psz = TextRenderer.MeasureText(pct + "%", font);
                var lsz = TextRenderer.MeasureText(top.Label, Theme.Small);
                int cx = donut.X + dia / 2, cy = donut.Y + dia / 2;
                TextRenderer.DrawText(g, pct + "%", font,
                    new Rectangle(cx - psz.Width / 2, cy - psz.Height - 2, psz.Width, psz.Height),
                    Theme.TextDark, TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(g, top.Label, Theme.Small,
                    new Rectangle(cx - lsz.Width / 2 - 20, cy + 2, lsz.Width + 40, lsz.Height),
                    Theme.TextGray, TextFormatFlags.HorizontalCenter);
            }

            int lx = donut.Right + 24;
            int ly = Math.Max(58, (Height - slices.Count * 46) / 2);
            foreach (var s in slices)
            {
                using (var b = new SolidBrush(s.Color)) g.FillEllipse(b, lx, ly + 5, 10, 10);
                TextRenderer.DrawText(g, s.Label, Theme.BodyBold, new Point(lx + 20, ly - 2), Theme.TextDark);
                string detail = Theme.Money(s.Value) +
                                (total > 0 ? "  ·  " + (int)Math.Round(100 * (double)(s.Value / total)) + "%" : "");
                TextRenderer.DrawText(g, detail, Theme.Small, new Point(lx + 20, ly + 18), Theme.TextGray);
                ly += 46;
            }
            if (slices.Count == 0)
                TextRenderer.DrawText(g, "No payroll summary data", Theme.Small, new Point(lx, Height / 2), Theme.TextGray);
        }
    }
}