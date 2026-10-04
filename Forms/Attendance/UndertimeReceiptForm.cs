using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    public sealed class UndertimeReceiptForm : Form
    {
        private readonly string receiptText;
        private readonly PrintDocument printDocument = new();

        public UndertimeReceiptForm(AttendanceRecord record)
        {
            Text = "Undertime Follow-up Record";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 450);
            MinimumSize = new Size(520, 380);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            string status = record.UndertimeStatus == "Resolved" ? "Discussed with employee" : "Pending management follow-up";
            string reviewed = record.UndertimeReviewedAt.HasValue
                ? record.UndertimeReviewedAt.Value.ToString("MMM d, yyyy h:mm tt") : "Not recorded yet";
            receiptText =
                "UNDERTIME FOLLOW-UP RECORD\r\n\r\n" +
                $"Employee: {record.EmployeeName}\r\n" +
                $"Attendance date: {record.AttendanceDate:MMMM d, yyyy}\r\n" +
                $"Time in: {Clock(record.TimeIn)}\r\n" +
                $"Time out: {Clock(record.TimeOut)}\r\n" +
                $"Undertime: {AttendanceService.FormatDuration(AttendanceService.GetUndertimeMinutes(record))}\r\n" +
                $"Status: {status}\r\n" +
                $"Recorded by: {(string.IsNullOrWhiteSpace(record.UndertimeReviewedBy) ? "Not recorded" : record.UndertimeReviewedBy)}\r\n" +
                $"Recorded on: {reviewed}\r\n\r\n" +
                "MANAGEMENT REMARKS\r\n" +
                (string.IsNullOrWhiteSpace(record.UndertimeRemarks)
                    ? "Management has not recorded a follow-up message yet."
                    : record.UndertimeRemarks);

            var title = new Label { Text = "Undertime Follow-up Record", Font = Theme.H2, ForeColor = Theme.TextDark,
                Dock = DockStyle.Top, Height = 52, Padding = new Padding(20, 12, 0, 0), BackColor = Theme.Bg };
            var body = new RichTextBox { Text = receiptText, Dock = DockStyle.Fill, ReadOnly = true,
                BackColor = Color.White, ForeColor = Theme.TextDark, Font = Theme.Body,
                BorderStyle = BorderStyle.FixedSingle, ScrollBars = RichTextBoxScrollBars.Vertical,
                Margin = new Padding(20) };
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Theme.Bg };
            var print = new ModernButton { Text = "Print / Save as PDF", Size = new Size(180, 38),
                Location = new Point(20, 8), SurroundColor = Theme.Bg };
            var close = new ModernButton { Text = "Close", Size = new Size(110, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(490, 8), SurroundColor = Theme.Bg,
                FillColor = Color.FromArgb(229, 231, 235), ForeColor = Theme.TextDark };
            footer.Resize += (s, e) => close.Left = footer.ClientSize.Width - close.Width - 20;
            print.Click += (s, e) => PrintReceipt();
            close.Click += (s, e) => Close();
            footer.Controls.Add(print);
            footer.Controls.Add(close);
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(title);

            printDocument.DocumentName = $"Undertime follow-up - {record.EmployeeName} - {record.AttendanceDate:yyyy-MM-dd}";
            printDocument.PrintPage += (s, e) =>
            {
                using var font = new Font("Segoe UI", 11f);
                e.Graphics!.DrawString(receiptText, font, Brushes.Black, e.MarginBounds);
                e.HasMorePages = false;
            };
        }

        private void PrintReceipt()
        {
            using var dialog = new PrintDialog { Document = printDocument, UseEXDialog = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                try { printDocument.Print(); }
                catch (Exception ex) { MessageBox.Show(this, "Could not print the record: " + ex.Message,
                    "Print record", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private static string Clock(TimeSpan? value)
            => value.HasValue ? DateTime.Today.Add(value.Value).ToString("h:mm tt") : "Not recorded";
    }
}
