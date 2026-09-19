using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PAYROLL.UI
{
    public static class GridStyle
    {
        public static void Apply(DataGridView grid)
        {
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Color.White;
            grid.GridColor = Theme.GridLine;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 46;
            grid.RowTemplate.Height = 42;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ShowCellToolTips = false; 

            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Theme.TextGray,
                SelectionBackColor = Color.White,
                SelectionForeColor = Theme.TextGray,
                Font = Theme.SmallBold,
                Padding = new Padding(10, 0, 0, 0),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };

            var cell = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Theme.TextDark,
                SelectionBackColor = Theme.AccentSoft,
                SelectionForeColor = Theme.TextDark,
                Font = Theme.Body,
                Padding = new Padding(10, 0, 10, 0),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.False
            };
            grid.DefaultCellStyle = cell;
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle(cell) { BackColor = Color.FromArgb(250, 251, 252) };

            typeof(DataGridView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(grid, true, null);
        }

        public static void PaintStatusBadges(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (sender is not DataGridView grid) return;

            var col = grid.Columns[e.ColumnIndex];
            if (col is null) return;

            bool isStatus = col.Name.Equals("Status", StringComparison.OrdinalIgnoreCase)
                         || col.HeaderText.Equals("Status", StringComparison.OrdinalIgnoreCase);
            if (!isStatus) return;

            string text = e.FormattedValue?.ToString() ?? "";
            if (text.Length == 0) return;

            e.Paint(e.CellBounds,
                DataGridViewPaintParts.Background | DataGridViewPaintParts.Border |
                DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.ContentBackground);

            var (bg, fg) = BadgeColors(text);
            var sz = TextRenderer.MeasureText(text, Theme.SmallBold);
            var rect = new Rectangle(e.CellBounds.X + 10, e.CellBounds.Y + (e.CellBounds.Height - 20) / 2,
                                     Math.Max(sz.Width + 18, 46), 20);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Gfx.Round(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), 9))
            using (var b = new SolidBrush(bg)) g.FillPath(b, path);
            TextRenderer.DrawText(g, text, Theme.SmallBold, rect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            e.Handled = true;
        }

        public static (Color bg, Color fg) BadgeColors(string text)
        {
            string s = text.ToLowerInvariant();
            if (s.Contains("unpaid") || s.Contains("fail") || s.Contains("overdue")) return (Theme.RedBg, Theme.RedText);
            if (s.Contains("paid"))  return (Theme.GreenBg, Theme.GreenText);
            if (s.Contains("pend"))  return (Theme.AmberBg, Theme.AmberText);
            if (s.Contains("active") || s.Contains("verified") || s.Contains("complete")) return (Theme.GreenBg, Theme.GreenText);
            if (s.Contains("inactive") || s.Contains("suspend")) return (Color.FromArgb(229, 231, 235), Theme.TextGray);
            return (Theme.AccentSoft, Theme.AccentDark);
        }
    }
}