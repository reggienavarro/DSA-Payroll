using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    // Opened from the bell on the admin / cash management dashboard. It lists employees
    // who timed out before completing their 8-hour duty, so management can talk to them,
    // write down the reason they gave and what was decided, and mark the notice discussed.
    public class UndertimeNoticesForm : Form
    {
        private readonly string reviewer;
        private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true };
        private readonly CheckBox showDiscussed = new() { Text = "Show discussed notices", AutoSize = true, BackColor = Theme.Bg };
        private readonly Label summary = new();
        private readonly Label remarksLabel = new();
        private readonly TextBox remarks = new() { Multiline = true, MaxLength = 500, ScrollBars = ScrollBars.Vertical };
        private List<AttendanceRecord> notices = new();

        public UndertimeNoticesForm(string reviewer)
        {
            this.reviewer = reviewer;
            Text = "Undertime Notices";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(780, 520);
            ClientSize = new Size(900, 580);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            // ----- top: what this is, and how many need follow-up -----
            var top = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Bg };
            var title = new Label
            {
                Text = "Undertime Notices", Font = Theme.H2, ForeColor = Theme.TextDark,
                AutoSize = true, Location = new Point(20, 14), BackColor = Theme.Bg
            };
            var description = new Label
            {
                Text = "These employees timed out before completing their 8-hour duty. Talk to the employee, " +
                       "write down the reason they gave and what was decided, then mark the notice as discussed.",
                Font = Theme.Small, ForeColor = Theme.TextGray, Location = new Point(20, 44),
                Size = new Size(860, 34), BackColor = Theme.Bg, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            summary.Font = Theme.BodyBold;
            summary.ForeColor = Theme.AmberText;
            summary.AutoSize = true;
            summary.Location = new Point(20, 78);
            summary.BackColor = Theme.Bg;
            showDiscussed.Location = new Point(700, 16);
            showDiscussed.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            showDiscussed.CheckedChanged += (s, e) => LoadNotices();
            top.Controls.Add(title);
            top.Controls.Add(description);
            top.Controls.Add(summary);
            top.Controls.Add(showDiscussed);

            // ----- bottom: remarks + the button that closes the notice -----
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 180, BackColor = Theme.Bg };
            remarksLabel.Text = "REMARKS  (reason the employee gave and what was decided)";
            remarksLabel.Font = Theme.SmallBold;
            remarksLabel.ForeColor = Theme.TextGray;
            remarksLabel.AutoSize = true;
            remarksLabel.Location = new Point(20, 12);
            remarksLabel.BackColor = Theme.Bg;
            remarks.Location = new Point(20, 34);
            remarks.Size = new Size(860, 84);
            remarks.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var markDiscussed = new ModernButton
            {
                Text = "Mark as Discussed", Location = new Point(20, 128), Size = new Size(200, 40),
                SurroundColor = Theme.Bg
            };
            markDiscussed.Click += (s, e) => MarkDiscussed();
            bottom.Controls.Add(remarksLabel);
            bottom.Controls.Add(remarks);
            bottom.Controls.Add(markDiscussed);

            // ----- middle: the list -----
            var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(20, 0, 20, 0) };
            GridStyle.Apply(grid);
            grid.SelectionChanged += (s, e) => ShowSelectedRemarks();
            grid.CellDoubleClick += (s, e) =>
            {
                var notice = SelectedNotice();
                if (e.RowIndex < 0 || notice == null || notice.UndertimeStatus != "Resolved") return;
                using var receipt = new UndertimeReceiptForm(notice);
                receipt.ShowDialog(this);
            };
            gridHost.Controls.Add(grid);

            // Fill first, then the edges, so the edges claim their space (same order as the dashboard).
            Controls.Add(gridHost);
            Controls.Add(bottom);
            Controls.Add(top);

            LoadNotices();
        }

        private static string KeyOf(AttendanceRecord n) => $"{n.EmployeeId}|{n.AttendanceDate:yyyy-MM-dd}";

        private static string Clock(TimeSpan? time)
            => time == null ? "" : DateTime.Today.Add(time.Value).ToString("h:mm tt");

        private AttendanceRecord? SelectedNotice()
        {
            if (grid.CurrentRow?.Cells["Key"].Value is not string key) return null;
            return notices.Find(n => KeyOf(n) == key);
        }

        private void LoadNotices()
        {
            try
            {
                notices = AttendanceService.ListUndertimeNotices(openOnly: !showDiscussed.Checked);

                var table = new DataTable();
                table.Columns.Add("Key");      // employee + date: finds the record again after the list reloads (hidden)
                table.Columns.Add("Employee");
                table.Columns.Add("Date");
                table.Columns.Add("Time In");
                table.Columns.Add("Time Out");
                table.Columns.Add("Short By");
                table.Columns.Add("Status");
                foreach (var n in notices)
                    table.Rows.Add(KeyOf(n), n.EmployeeName, n.AttendanceDate.ToString("MMM d, yyyy"),
                        Clock(n.TimeIn), Clock(n.TimeOut),
                        AttendanceService.FormatDuration(AttendanceService.GetUndertimeMinutes(n)),
                        n.UndertimeStatus == "Open" ? "Needs follow-up" : "Discussed");

                grid.DataSource = table;
                grid.Columns["Key"]!.Visible = false;

                int open = notices.FindAll(n => n.UndertimeStatus == "Open").Count;
                summary.ForeColor = open > 0 ? Theme.AmberText : Theme.GreenText;
                summary.Text = open > 0
                    ? $"{open} notice(s) need follow-up"
                    : "Nothing needs follow-up. Every undertime has been discussed.";
                ShowSelectedRemarks();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load the notices: " + ex.Message, "Undertime Notices",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // An open notice gets an empty box to write in. A discussed one shows what was recorded.
        private void ShowSelectedRemarks()
        {
            var notice = SelectedNotice();
            if (notice == null)
            {
                remarks.Clear();
                remarks.ReadOnly = true;
                return;
            }

            bool isOpen = notice.UndertimeStatus == "Open";
            remarks.ReadOnly = !isOpen;
            remarks.Text = isOpen ? "" : notice.UndertimeRemarks;
            remarksLabel.Text = isOpen
                ? "REMARKS  (reason the employee gave and what was decided)"
                : $"DISCUSSED BY {notice.UndertimeReviewedBy.ToUpperInvariant()}";
        }

        private void MarkDiscussed()
        {
            var notice = SelectedNotice();
            if (notice == null)
            {
                MessageBox.Show("Select a notice first.", "Undertime Notices",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (notice.UndertimeStatus != "Open")
            {
                MessageBox.Show("This notice has already been marked as discussed.", "Undertime Notices",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                AttendanceService.ResolveUndertime(notice.EmployeeId, notice.AttendanceDate, remarks.Text, reviewer);
                LoadNotices();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Undertime Notices", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
