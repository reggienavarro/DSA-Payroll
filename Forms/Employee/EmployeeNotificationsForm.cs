using System;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    public sealed class EmployeeNotificationsForm : Form
    {
        private readonly ListBox items = new();
        private readonly TextBox message = new();

        public EmployeeNotificationsForm(System.Collections.Generic.IReadOnlyList<EmployeeNotification> notices)
        {
            Text = "My Notifications";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 500);
            MinimumSize = new Size(560, 380);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            var heading = new Label { Text = "Notifications", Font = Theme.H2, ForeColor = Theme.TextDark,
                Dock = DockStyle.Top, Height = 52, Padding = new Padding(18, 12, 0, 0), BackColor = Theme.Bg };
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 260, BackColor = Theme.Bg, Padding = new Padding(12) };
            items.Dock = DockStyle.Fill;
            items.Font = Theme.Body;
            items.FormattingEnabled = true;
            items.DisplayMember = nameof(EmployeeNotification.Title);
            message.Dock = DockStyle.Fill;
            message.Multiline = true;
            message.ReadOnly = true;
            message.ScrollBars = ScrollBars.Vertical;
            message.BackColor = Color.White;
            message.BorderStyle = BorderStyle.FixedSingle;
            split.Panel1.Controls.Add(items);
            split.Panel2.Controls.Add(message);

            foreach (var notice in notices) items.Items.Add(notice);
            items.Format += (s, e) =>
            {
                if (e.ListItem is EmployeeNotification notice)
                    e.Value = $"{(notice.IsRead ? "" : "• ")}{notice.Title}  ·  {notice.CreatedAt:g}";
            };
            items.SelectedIndexChanged += (s, e) =>
            {
                if (items.SelectedItem is EmployeeNotification notice)
                    message.Text = notice.Title + Environment.NewLine + notice.CreatedAt.ToString("f") +
                        Environment.NewLine + Environment.NewLine + notice.Message;
            };

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Theme.Bg };
            var close = new ModernButton { Text = "Close", Size = new Size(110, 36), Anchor = AnchorStyles.Right | AnchorStyles.Top,
                SurroundColor = Theme.Bg, Location = new Point(footer.Width - 126, 8) };
            footer.Resize += (s, e) => close.Location = new Point(footer.ClientSize.Width - close.Width - 12, 8);
            close.Click += (s, e) => Close();
            footer.Controls.Add(close);
            Controls.Add(split);
            Controls.Add(footer);
            Controls.Add(heading);
            if (items.Items.Count > 0) items.SelectedIndex = 0;
        }
    }
}
