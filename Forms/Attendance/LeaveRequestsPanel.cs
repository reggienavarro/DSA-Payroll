using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    public sealed class LeaveRequestsPanel : Panel
    {
        private readonly string reviewer;
        private readonly Action? afterReview;
        private readonly DataGridView requestsGrid = new() { Dock = DockStyle.Fill, ReadOnly = true };
        private readonly ComboBox statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private readonly Label pendingSummary = new();
        private readonly ModernButton approve = new() { Text = "Approve as Paid", Size = new Size(155, 38) };
        private readonly ModernButton reject = new() { Text = "Reject", Size = new Size(110, 38), FillColor = Color.FromArgb(229, 231, 235), ForeColor = Theme.TextDark };

        public LeaveRequestsPanel(string reviewer, Action? afterReview = null)
        {
            this.reviewer = reviewer;
            this.afterReview = afterReview;
            BackColor = Theme.Bg;
            Padding = new Padding(20);

            var heading = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Theme.Bg };
            var title = new Label { Text = "Leave Requests", Font = Theme.H2, ForeColor = Theme.TextDark,
                AutoSize = true, Location = new Point(0, 2), BackColor = Theme.Bg };
            pendingSummary.Font = Theme.Small;
            pendingSummary.ForeColor = Theme.TextGray;
            pendingSummary.AutoSize = true;
            pendingSummary.Location = new Point(0, 36);
            pendingSummary.BackColor = Theme.Bg;
            statusFilter.Items.AddRange(new object[] { "Pending", "All requests", "Approved", "Rejected" });
            statusFilter.SelectedIndex = 0;
            var refresh = new ModernButton { Text = "Refresh", Size = new Size(100, 34), Anchor = AnchorStyles.Top | AnchorStyles.Right,
                SurroundColor = Theme.Bg, Location = new Point(0, 0) };
            heading.Resize += (s, e) =>
            {
                refresh.Location = new Point(heading.ClientSize.Width - refresh.Width, 3);
                statusFilter.Location = new Point(refresh.Left - statusFilter.Width - 12, 6);
            };
            heading.Controls.Add(title);
            heading.Controls.Add(pendingSummary);
            heading.Controls.Add(statusFilter);
            heading.Controls.Add(refresh);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, BackColor = Theme.Bg,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 7, 0, 0) };
            approve.SurroundColor = Theme.Bg;
            reject.SurroundColor = Theme.Bg;
            reject.Margin = new Padding(10, 0, 0, 0);
            footer.Controls.Add(approve);
            footer.Controls.Add(reject);

            GridStyle.Apply(requestsGrid);
            requestsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            requestsGrid.MultiSelect = false;
            requestsGrid.SelectionChanged += (s, e) => UpdateActionState();
            statusFilter.SelectedIndexChanged += (s, e) => RefreshRequests();
            refresh.Click += (s, e) => RefreshRequests();
            approve.Click += (s, e) => ReviewSelected(true);
            reject.Click += (s, e) => ReviewSelected(false);

            Controls.Add(requestsGrid);
            Controls.Add(footer);
            Controls.Add(heading);
        }

        public void RefreshRequests()
        {
            try
            {
                var all = LeaveService.ListForRange(new DateTime(2000, 1, 1), new DateTime(2100, 12, 31));
                int pendingCount = all.Count(item => item.Status == "Pending");
                pendingSummary.Text = $"{pendingCount} request(s) awaiting review  ·  {all.Count} total";
                string selectedStatus = statusFilter.SelectedItem?.ToString() ?? "Pending";
                var visible = all.Where(item => selectedStatus == "All requests" || item.Status == selectedStatus)
                    .OrderBy(item => item.Status == "Pending" ? 0 : 1)
                    .ThenByDescending(item => item.StartDate).ToList();
                var table = new DataTable();
                table.Columns.Add("RequestID", typeof(int));
                table.Columns.Add("Employee");
                table.Columns.Add("Start Date");
                table.Columns.Add("End Date");
                table.Columns.Add("Reason");
                table.Columns.Add("Status");
                table.Columns.Add("Paid", typeof(bool));
                foreach (var request in visible)
                    table.Rows.Add(request.LeaveRequestId, request.EmployeeName,
                        request.StartDate.ToString("MMM d, yyyy"), request.EndDate.ToString("MMM d, yyyy"),
                        request.Reason, request.Status, request.Paid);
                requestsGrid.DataSource = table;
                if (requestsGrid.Columns["RequestID"] is DataGridViewColumn idColumn) idColumn.Visible = false;
                UpdateActionState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load leave requests: " + ex.Message, "Leave Requests",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateActionState()
        {
            bool selectedPending = requestsGrid.CurrentRow?.Cells["Status"].Value?.ToString() == "Pending";
            approve.Enabled = selectedPending;
            reject.Enabled = selectedPending;
        }

        private void ReviewSelected(bool approved)
        {
            if (!approve.Enabled && !reject.Enabled)
            {
                MessageBox.Show(this, "Select a pending leave request first.", "Leave Requests",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (requestsGrid.CurrentRow?.Cells["RequestID"].Value is not int requestId) return;
            try
            {
                LeaveService.Review(requestId, approved, reviewer);
                RefreshRequests();
                afterReview?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not review leave request: " + ex.Message, "Leave Requests",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
