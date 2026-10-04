using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    public sealed class ManagementNotificationsForm : Form
    {
        public string SelectedAction { get; private set; } = "";

        public ManagementNotificationsForm(int ticketLow, int ticketMedium, int ticketHigh,
            int overtime, int leave, int undertime)
        {
            int tickets = ticketLow + ticketMedium + ticketHigh;
            Text = "Notifications";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 475);
            MinimumSize = new Size(500, 430);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            var title = new Label
            {
                Text = "Needs your attention", Font = Theme.H2, ForeColor = Theme.TextDark,
                Location = new Point(22, 18), AutoSize = true, BackColor = Theme.Bg
            };
            var description = new Label
            {
                Text = "Open an item below to review and resolve it.", Font = Theme.Small,
                ForeColor = Theme.TextGray, Location = new Point(22, 50), AutoSize = true,
                BackColor = Theme.Bg
            };
            var list = new FlowLayoutPanel
            {
                Location = new Point(18, 82), Size = new Size(484, 368),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
                BackColor = Theme.Bg, Padding = new Padding(0, 2, 8, 4)
            };

            AddItem(list, "Salary tickets", tickets,
                $"High: {ticketHigh}   ·   Medium: {ticketMedium}   ·   Low: {ticketLow}", "tickets");
            AddItem(list, "Overtime approvals", overtime,
                "Completed overtime hours waiting for approval.", "overtime");
            AddItem(list, "Leave requests", leave,
                "Leave requests waiting for review.", "leave");
            AddItem(list, "Undertime follow-ups", undertime,
                "Employees who timed out before completing their scheduled hours.", "undertime");

            if (tickets + overtime + leave + undertime == 0)
            {
                list.Controls.Add(new Label
                {
                    Text = "You're all caught up. There are no pending items.",
                    Font = Theme.Body, ForeColor = Theme.GreenText, AutoSize = true,
                    Margin = new Padding(8, 12, 8, 8), BackColor = Theme.Bg
                });
            }

            Controls.Add(title);
            Controls.Add(description);
            Controls.Add(list);
        }

        private void AddItem(FlowLayoutPanel list, string title, int count, string detail, string action)
        {
            if (count <= 0) return;
            var card = new Panel
            {
                Size = new Size(448, 76), Margin = new Padding(2, 3, 2, 7),
                BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle
            };
            var itemTitle = new Label
            {
                Text = title, Location = new Point(12, 8), AutoSize = true,
                Font = Theme.BodyBold, ForeColor = Theme.TextDark, BackColor = Color.White
            };
            var itemDetail = new Label
            {
                Text = detail, Location = new Point(12, 34), Size = new Size(300, 30),
                Font = Theme.Small, ForeColor = Theme.TextGray, AutoEllipsis = true,
                BackColor = Color.White
            };
            var badge = new Label
            {
                Text = count > 99 ? "99+" : count.ToString(), Location = new Point(328, 20),
                Size = new Size(42, 30), TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.BodyBold, ForeColor = Color.White, BackColor = Theme.Accent
            };
            var open = new Button
            {
                Text = "Review", Location = new Point(378, 19), Size = new Size(60, 32),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(239, 246, 255),
                ForeColor = Theme.Accent, Cursor = Cursors.Hand
            };
            open.FlatAppearance.BorderSize = 0;
            open.Click += (s, e) =>
            {
                SelectedAction = action;
                DialogResult = DialogResult.OK;
                Close();
            };
            card.Controls.Add(itemTitle);
            card.Controls.Add(itemDetail);
            card.Controls.Add(badge);
            card.Controls.Add(open);
            list.Controls.Add(card);
        }
    }
}
