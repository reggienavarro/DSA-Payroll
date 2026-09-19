using System;
using System.Drawing;

namespace PAYROLL.UI
{
    // One central place for colors + fonts so the whole UI stays consistent.
    internal static class Theme
    {
        public static readonly Color Navy        = Color.FromArgb(23, 27, 46);
        public static readonly Color NavyHover   = Color.FromArgb(34, 40, 66);
        public static readonly Color SidebarText = Color.FromArgb(154, 163, 184);
        public static readonly Color Accent      = Color.FromArgb(59, 130, 246);
        public static readonly Color AccentDark  = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentSoft  = Color.FromArgb(239, 246, 255);
        public static readonly Color Bg          = Color.FromArgb(243, 244, 246);
        public static readonly Color CardBorder  = Color.FromArgb(229, 231, 235);
        public static readonly Color TextDark    = Color.FromArgb(17, 24, 39);
        public static readonly Color TextGray    = Color.FromArgb(107, 114, 128);
        public static readonly Color GreenBg     = Color.FromArgb(220, 252, 231);
        public static readonly Color GreenText   = Color.FromArgb(21, 128, 61);
        public static readonly Color AmberBg     = Color.FromArgb(254, 243, 199);
        public static readonly Color AmberText   = Color.FromArgb(180, 83, 9);
        public static readonly Color Amber       = Color.FromArgb(245, 158, 11);
        public static readonly Color RedBg       = Color.FromArgb(254, 226, 226);
        public static readonly Color RedText     = Color.FromArgb(185, 28, 28);
        public static readonly Color GridLine    = Color.FromArgb(236, 240, 243);

        // ₱ = Philippine Peso (your existing code uses ₱).
        public static string Currency = "\u20B1";

        public static readonly Font H1        = new Font("Segoe UI Semibold", 15f);
        public static readonly Font H2        = new Font("Segoe UI Semibold", 12.5f);
        public static readonly Font ValueBig  = new Font("Segoe UI Semibold", 16f);
        public static readonly Font Body      = new Font("Segoe UI", 9.75f);
        public static readonly Font BodyBold  = new Font("Segoe UI Semibold", 9.75f);
        public static readonly Font Small     = new Font("Segoe UI", 8.75f);
        public static readonly Font SmallBold = new Font("Segoe UI Semibold", 8.75f);

        public static string Money(decimal v) => Currency + v.ToString("N2");
    }

    internal static class Gfx
    {
        public static System.Drawing.Drawing2D.GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Lighten(Color c, float amount) =>
            Color.FromArgb(c.A,
                (int)(c.R + (255 - c.R) * amount),
                (int)(c.G + (255 - c.G) * amount),
                (int)(c.B + (255 - c.B) * amount));
    }
}