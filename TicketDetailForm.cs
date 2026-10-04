using System;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    public sealed class TicketDetailForm : Form
    {
        public TicketDetailForm(TicketRow ticket)
        {
            Text = "Ticket Details";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            Size = new Size(780, 720);
            MinimumSize = new Size(560, 500);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                ColumnCount = 1,
                RowCount = 7,
                BackColor = Theme.Bg
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            var heading = new Label
            {
                Text = ticket.Subject,
                Font = Theme.H2,
                ForeColor = Theme.TextDark,
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            var metadata = new Label
            {
                Text = $"Filed {ticket.CreatedAt:MMM d, yyyy h:mm tt}   ·   Status: {ticket.Status}",
                Font = Theme.Small,
                ForeColor = Theme.TextGray,
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var employeeMessageLabel = SectionLabel("YOUR MESSAGE");
            var employeeMessage = CreateTextView(ticket.Message);
            var responseLabel = SectionLabel("MANAGEMENT RESPONSE");
            var responseText = string.IsNullOrWhiteSpace(ticket.AdminResponse)
                ? "Management has not responded yet."
                : ticket.AdminResponse;
            var response = CreateTextView(responseText);

            var footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var close = new Button
            {
                Text = "Close",
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Theme.TextDark,
                Font = Theme.SmallBold
            };
            close.FlatAppearance.BorderColor = Theme.CardBorder;
            close.Click += (s, e) => Close();
            footer.Resize += (s, e) => close.Location = new Point(footer.ClientSize.Width - close.Width, 5);
            footer.Controls.Add(close);

            layout.Controls.Add(heading, 0, 0);
            layout.Controls.Add(metadata, 0, 1);
            layout.Controls.Add(employeeMessageLabel, 0, 2);
            layout.Controls.Add(employeeMessage, 0, 3);
            layout.Controls.Add(responseLabel, 0, 4);
            layout.Controls.Add(response, 0, 5);
            layout.Controls.Add(footer, 0, 6);
            Controls.Add(layout);
        }

        private static Label SectionLabel(string text) => new()
        {
            Text = text,
            Font = Theme.SmallBold,
            ForeColor = Theme.TextGray,
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static RichTextBox CreateTextView(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            ReadOnly = true,
            DetectUrls = false,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = Theme.TextDark,
            Font = Theme.Body,
            Margin = new Padding(0, 0, 0, 8)
        };
    }
}
