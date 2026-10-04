using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    public sealed class PendingOvertimeForm : Form
    {
        private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true };
        private readonly Label summary = new();

        public PendingOvertimeForm()
        {
            Text = "Pending Overtime Approvals";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 540);
            MinimumSize = new Size(760, 440);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            var top = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Theme.Bg, Padding = new Padding(18, 12, 18, 8) };
            var title = new Label
            {
                Text = "Overtime awaiting approval", Font = Theme.H2, ForeColor = Theme.TextDark,
                Location = new Point(18, 10), AutoSize = true, BackColor = Theme.Bg
            };
            summary.Font = Theme.Small;
            summary.ForeColor = Theme.TextGray;
            summary.Location = new Point(20, 42);
            summary.AutoSize = true;
            summary.BackColor = Theme.Bg;
            top.Controls.Add(title);
            top.Controls.Add(summary);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Theme.Bg, Padding = new Padding(18, 8, 18, 8) };
            var approve = new ModernButton
            {
                Text = "Approve Selected Overtime", Dock = DockStyle.Left,
                Size = new Size(220, 40), SurroundColor = Theme.Bg
            };
            approve.Click += (s, e) => ApproveSelected();
            var refresh = new Button
            {
                Text = "Refresh", Dock = DockStyle.Right, Width = 100, Height = 38,
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Theme.TextDark
            };
            refresh.Click += (s, e) => LoadPending();
            bottom.Controls.Add(approve);
            bottom.Controls.Add(refresh);

            GridStyle.Apply(grid);
            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 0), BackColor = Theme.Bg };
            host.Controls.Add(grid);
            Controls.Add(host);
            Controls.Add(bottom);
            Controls.Add(top);
            LoadPending();
        }

        private void LoadPending()
        {
            try
            {
                var records = AttendanceService.ListPendingOvertimeApprovals();
                var table = new DataTable();
                table.Columns.Add("EmployeeID", typeof(int));
                table.Columns.Add("AttendanceDate", typeof(DateTime));
                table.Columns.Add("Employee");
                table.Columns.Add("Date");
                table.Columns.Add("Time In");
                table.Columns.Add("Break Out");
                table.Columns.Add("Break In");
                table.Columns.Add("Time Out");
                table.Columns.Add("Overtime");
                foreach (var record in records)
                    table.Rows.Add(record.EmployeeId, record.AttendanceDate.Date, record.EmployeeName,
                        record.AttendanceDate.ToString("MMM d, yyyy"), Clock(record.TimeIn),
                        Clock(record.BreakOut), Clock(record.BreakIn), Clock(record.TimeOut),
                        AttendanceService.FormatDuration(AttendanceService.GetOvertimeMinutes(record)));
                grid.DataSource = table;
                grid.Columns["EmployeeID"]!.Visible = false;
                grid.Columns["AttendanceDate"]!.Visible = false;
                summary.Text = records.Count == 0
                    ? "There are no completed overtime records waiting for approval."
                    : $"{records.Count} completed overtime record(s) are waiting for approval.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load pending overtime: " + ex.Message,
                    "Overtime Approvals", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApproveSelected()
        {
            if (grid.CurrentRow == null ||
                grid.CurrentRow.Cells["EmployeeID"].Value is not int employeeId ||
                grid.CurrentRow.Cells["AttendanceDate"].Value is not DateTime date)
            {
                MessageBox.Show("Select a pending overtime record first.", "Overtime Approvals",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                AttendanceService.SetOvertimeApproval(employeeId, date, true);
                LoadPending();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not approve overtime: " + ex.Message,
                    "Overtime Approvals", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string Clock(TimeSpan? time)
            => time.HasValue ? DateTime.Today.Add(time.Value).ToString("h:mm tt") : "—";
    }
}
