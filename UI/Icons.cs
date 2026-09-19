using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PAYROLL.UI
{
    public enum IconKind { Grid, Users, Card, Arrows, Chart, Sliders, Logout, Bell, Calendar, Bank, Plus, Line, Building }

    internal static class Icons
    {
        public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color, float penWidth = 1.7f)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(bounds.X, bounds.Y);
            float s = Math.Min(bounds.Width, bounds.Height) / 24f;
            g.ScaleTransform(s, s);
            using (var pen = new Pen(color, penWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var fill = new SolidBrush(color))
            {
                switch (kind)
                {
                    case IconKind.Grid:
                        Box(g, pen, 3, 3, 7, 7); Box(g, pen, 14, 3, 7, 7);
                        Box(g, pen, 3, 14, 7, 7); Box(g, pen, 14, 14, 7, 7);
                        break;
                    case IconKind.Users:
                        g.DrawEllipse(pen, 4, 4, 7, 7);
                        g.DrawArc(pen, 2, 13, 11, 8, 180, 180);
                        g.DrawEllipse(pen, 15, 6, 5, 5);
                        g.DrawArc(pen, 14, 15, 8, 6, 180, 180);
                        break;
                    case IconKind.Card:
                        Box(g, pen, 3, 6, 18, 13);
                        g.DrawLine(pen, 3, 10.5f, 21, 10.5f);
                        break;
                    case IconKind.Arrows:
                        g.DrawLine(pen, 5, 10, 17, 10);
                        Head(g, pen, 17, 10, true);
                        g.DrawLine(pen, 7, 15, 19, 15);
                        Head(g, pen, 7, 15, false);
                        break;
                    case IconKind.Chart:
                        g.DrawRectangle(pen, 4f, 12f, 4f, 8f);
                        g.DrawRectangle(pen, 10f, 8f, 4f, 12f);
                        g.DrawRectangle(pen, 16f, 4f, 4f, 16f);
                        break;
                    case IconKind.Sliders:
                        g.DrawLine(pen, 4, 6, 20, 6);   g.FillEllipse(fill, 10.5f, 3.5f, 5, 5);
                        g.DrawLine(pen, 4, 12, 20, 12); g.FillEllipse(fill, 5.5f, 9.5f, 5, 5);
                        g.DrawLine(pen, 4, 18, 20, 18); g.FillEllipse(fill, 14.5f, 15.5f, 5, 5);
                        break;
                    case IconKind.Logout:
                        g.DrawLines(pen, new[] { new PointF(9, 4), new PointF(5, 4), new PointF(5, 20), new PointF(9, 20) });
                        g.DrawLine(pen, 11, 12, 20, 12);
                        Head(g, pen, 20, 12, true);
                        break;
                    case IconKind.Bell:
                        g.DrawArc(pen, 7, 4, 10, 10, 180, 180);
                        g.DrawLine(pen, 7, 9, 6, 17);
                        g.DrawLine(pen, 17, 9, 18, 17);
                        g.DrawLine(pen, 5, 17, 19, 17);
                        g.DrawArc(pen, 10, 18, 4, 4, 0, 180);
                        break;
                    case IconKind.Calendar:
                        Box(g, pen, 3, 5, 18, 15);
                        g.DrawLine(pen, 3, 10, 21, 10);
                        g.DrawLine(pen, 8, 2.5f, 8, 6.5f);
                        g.DrawLine(pen, 16, 2.5f, 16, 6.5f);
                        break;
                    case IconKind.Bank:
                        g.DrawLines(pen, new[] { new PointF(12, 3), new PointF(2, 8), new PointF(22, 8), new PointF(12, 3) });
                        g.DrawLine(pen, 4, 18, 20, 18);
                        g.DrawLine(pen, 6, 10, 6, 16);
                        g.DrawLine(pen, 12, 10, 12, 16);
                        g.DrawLine(pen, 18, 10, 18, 16);
                        break;
                    case IconKind.Plus:
                        g.DrawLine(pen, 12, 5, 12, 19);
                        g.DrawLine(pen, 5, 12, 19, 12);
                        break;
                    case IconKind.Line:
                        g.DrawLines(pen, new[] { new PointF(4, 17), new PointF(9, 12), new PointF(13, 15), new PointF(20, 7) });
                        g.FillEllipse(fill, 18, 5, 4, 4);
                        break;
                    case IconKind.Building:
                        Box(g, pen, 5, 3, 14, 18);
                        g.DrawLine(pen, 5, 21, 19, 21); 
                        for (int row = 0; row < 3; row++)
                            for (int col = 0; col < 2; col++)
                                Box(g, pen, 8 + col * 5, 6.5f + row * 5, 3, 3);
                        break;
                }
            }
            g.ResetTransform();
        }

        private static void Head(Graphics g, Pen pen, float tipX, float tipY, bool pointingRight)
        {
            float back = pointingRight ? -3 : 3;
            g.DrawLines(pen, new[] { new PointF(tipX + back, tipY - 3), new PointF(tipX, tipY), new PointF(tipX + back, tipY + 3) });
        }

        private static void Box(Graphics g, Pen pen, float x, float y, float w, float h)
        {
            using (var path = Gfx.Round(new RectangleF(x, y, w, h), 2)) g.DrawPath(pen, path);
        }
    }
}