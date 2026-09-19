using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace PAYROLL.UI
{
    public class PaymentHistoryChart : Control
    {
        private List<(string Label, decimal Amount)> data = new List<(string, decimal)>();
        private readonly List<PointF> pointPos = new List<PointF>();
        private int hoverIndex = -1;
        private decimal maxValue = 1;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "Payment History";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TotalText { get; set; } = "--";
        public PaymentHistoryChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetData(List<(string, decimal)>? rows, string totalText)
        {
            data = rows ?? new List<(string, decimal)>();
            TotalText = totalText;
            maxValue = data.Count == 0 ? 1 : NiceMax(data.Max(r => r.Amount));
            hoverIndex = -1;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int best = -1;
            float bestDist = 20;
            for (int i = 0; i < pointPos.Count; i++)
            {
                float d = Math.Abs(pointPos[i].X - e.X);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            if (best != hoverIndex) { hoverIndex = best; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverIndex != -1) { hoverIndex = -1; Invalidate(); }
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
            var totalSz = TextRenderer.MeasureText(TotalText, Theme.BodyBold);
            TextRenderer.DrawText(g, TotalText, Theme.BodyBold,
                new Rectangle(Width - 20 - totalSz.Width, 18, totalSz.Width, totalSz.Height), Theme.TextDark);

            var plot = new RectangleF(26, 58, Width - 52, Height - 98);
            if (plot.Height < 10) return;

            for (int i = 0; i <= 4; i++)
            {
                float y = plot.Top + plot.Height * i / 4f;
                using (var p = new Pen(Theme.GridLine)) g.DrawLine(p, plot.Left, y, plot.Right, y);
            }

            pointPos.Clear();
            if (data.Count == 0)
            {
                TextRenderer.DrawText(g, "No payroll data available", Theme.Small,
                    Rectangle.Round(plot), Theme.TextGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            float stepX = plot.Width / Math.Max(1, data.Count - 1);
            for (int i = 0; i < data.Count; i++)
            {
                float x = plot.Left + stepX * i;
                float y = plot.Bottom - (float)(data[i].Amount / maxValue) * plot.Height;
                pointPos.Add(new PointF(x, y));
            }

            if (pointPos.Count > 1)
            {
                var pts = pointPos.ToArray();
                var areaPts = new List<PointF>(pts)
                {
                    new PointF(pts[pts.Length - 1].X, plot.Bottom),
                    new PointF(pts[0].X, plot.Bottom)
                };
                using (var area = new GraphicsPath()) 
                {
                    area.AddPolygon(areaPts.ToArray());
                    using (var b = new SolidBrush(Color.FromArgb(34, 59, 130, 246))) g.FillPath(b, area);
                }
                using (var line = new GraphicsPath())
                {
                    line.AddLines(pts);
                    using (var pen = new Pen(Theme.Accent, 2.2f)
                             { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawPath(pen, line);
                }
            }

            // highlighted last point
            var last = pointPos[pointPos.Count - 1];
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, last.X - 6, last.Y - 6, 12, 12);
            using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, last.X - 4, last.Y - 4, 8, 8);

            // x labels
            for (int i = 0; i < data.Count; i++)
            {
                if (stepX < 26 && i % 2 == 1) continue;
                string lbl = data[i].Label;
                if (lbl.Length > 10) lbl = lbl.Substring(0, 9) + "…"; 
                var sz = TextRenderer.MeasureText(lbl, Theme.Small);
                int lx = Math.Max(2, Math.Min(Width - sz.Width - 2, (int)(pointPos[i].X - sz.Width / 2f)));
                TextRenderer.DrawText(g, lbl, Theme.Small,
                    new Rectangle(lx, (int)plot.Bottom + 10, sz.Width, sz.Height), Theme.TextGray);
            }

            // hover tooltip
            if (hoverIndex >= 0 && hoverIndex < pointPos.Count)
            {
                var pt = pointPos[hoverIndex];
                string tip = data[hoverIndex].Label + "   " + Theme.Money(data[hoverIndex].Amount);
                var sz = TextRenderer.MeasureText(tip, Theme.SmallBold);
                var rect = new RectangleF(pt.X - (sz.Width + 16) / 2f, pt.Y - sz.Height - 18, sz.Width + 16, sz.Height + 10);
                if (rect.Left < 6) rect.X = 6;
                if (rect.Right > Width - 6) rect.X = Width - 6 - rect.Width;
                if (rect.Top < 50) rect.Y = pt.Y + 14;
                using (var path = Gfx.Round(rect, 8))
                {
                    using (var b = new SolidBrush(Theme.Navy)) g.FillPath(b, path);
                    TextRenderer.DrawText(g, tip, Theme.SmallBold, Rectangle.Round(rect), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private static decimal NiceMax(decimal v)
        {
            if (v <= 0) return 100;
            decimal pow = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)v)));
            foreach (decimal s in new[] { 1m, 2m, 2.5m, 5m, 10m })
                if (v <= pow * s) return pow * s;
            return pow * 10;
        }
    }
}