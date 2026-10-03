using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    // The "Company" screen: configuration/master data that Payroll and other
    // modules read from. Self-contained so DashboardForm just drops it into
    // its content area, the same way Form1 gets embedded for "Employee."
    public class CompanyPanel : Panel
    {
        private TextBox riceBox = null!, dailyMealBox = null!, uniformBox = null!, laundryBox = null!, incentivesBox = null!, hourlyRateBox = null!;
        private DataGridView calendarEventsGrid = null!;
        private MonthCalendar calendarMonth = null!;
        private Label calendarDateLabel = null!;
        private DataGridView departmentsGrid = null!;
        private DataGridView leaveRequestsGrid = null!;
        private TextBox departmentName = null!;
        private readonly string reviewerName;
        private readonly System.Windows.Forms.Timer calendarRefreshTimer = new() { Interval = 30000 };

        public CompanyPanel(string reviewerName = "Management")
        {
            this.reviewerName = reviewerName;
            BackColor = Theme.Bg;
            Padding = new Padding(24);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildDefaultsTab());
            tabs.TabPages.Add(BuildCalendarTab());
            tabs.TabPages.Add(BuildLeaveRequestsTab());
            tabs.TabPages.Add(BuildDepartmentsTab());
            Controls.Add(tabs);

            calendarRefreshTimer.Tick += (s, e) =>
            {
                if (Visible && tabs.SelectedTab?.Text == "Calendar") LoadCalendar();
            };
            calendarRefreshTimer.Start();
        }

        public void RefreshAll()
        {
            LoadDefaults();
            LoadCalendar();
            LoadLeaveRequests();
            LoadDepartments();
        }

        // ------------------------------------------------------------------
        // PAYROLL DEFAULTS
        // ------------------------------------------------------------------
        private TabPage BuildDefaultsTab()
        {
            var page = new TabPage("Payroll Defaults") { BackColor = Theme.Bg, Padding = new Padding(20) };
            var card = new RoundedPanel { Location = new Point(0, 0), Size = new Size(420, 360), SurroundColor = Theme.Bg };

            var info = new Label
            {
                Text = "These are the starting values every new payslip is generated with. " +
                       "Management can still edit any individual payslip afterward.",
                Font = Theme.Small, ForeColor = Theme.TextGray, Location = new Point(16, 14),
                Size = new Size(388, 44), BackColor = Color.White
            };

            riceBox = AddDefaultField(card, "Rice Allowance", 70);
            dailyMealBox = AddDefaultField(card, "Daily Meal", 120);
            uniformBox = AddDefaultField(card, "Uniform", 170);
            laundryBox = AddDefaultField(card, "Laundry", 220);
            incentivesBox = AddDefaultField(card, "Incentives", 270);
            hourlyRateBox = AddDefaultField(card, "Default Hourly Rate", 320);

            var saveBtn = new ModernButton { Text = "Save Defaults", Location = new Point(16, 366), Size = new Size(388, 40), SurroundColor = Theme.Bg };
            saveBtn.Click += (s, e) =>
            {
                try
                {
                    CompanyService.SavePayrollDefaults(new PayrollDefaultsConfig
                    {
                        RiceAllowance = ParseOrZero(riceBox),
                        DailyMeal = ParseOrZero(dailyMealBox),
                        Uniform = ParseOrZero(uniformBox),
                        Laundry = ParseOrZero(laundryBox),
                        Incentives = ParseOrZero(incentivesBox),
                        DefaultHourlyRate = ParseOrZero(hourlyRateBox),
                    });
                    MessageBox.Show("Payroll defaults saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not save: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            card.Controls.Add(info);
            page.Controls.Add(saveBtn);
            page.Controls.Add(card);
            return page;
        }

        private TextBox AddDefaultField(Panel card, string label, int y)
        {
            var lbl = new Label { Text = label, Font = Theme.Body, ForeColor = Theme.TextDark, AutoSize = true, Location = new Point(16, y + 4), BackColor = Color.White };
            var box = new TextBox { Location = new Point(280, y), Width = 108, TextAlign = HorizontalAlignment.Right, Text = "0.00" };
            card.Controls.Add(lbl);
            card.Controls.Add(box);
            return box;
        }

        private static decimal ParseOrZero(TextBox box) => decimal.TryParse(box.Text, out var v) ? v : 0m;

        private void LoadDefaults()
        {
            try
            {
                var d = CompanyService.GetPayrollDefaults();
                riceBox.Text = d.RiceAllowance.ToString("0.00");
                dailyMealBox.Text = d.DailyMeal.ToString("0.00");
                uniformBox.Text = d.Uniform.ToString("0.00");
                laundryBox.Text = d.Laundry.ToString("0.00");
                incentivesBox.Text = d.Incentives.ToString("0.00");
                hourlyRateBox.Text = d.DefaultHourlyRate.ToString("0.00");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load payroll defaults: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------
        // PHILIPPINE COMPANY CALENDAR
        // ------------------------------------------------------------------
        private TabPage BuildCalendarTab()
        {
            var page = new TabPage("Calendar") { BackColor = Theme.Bg, Padding = new Padding(20) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Theme.Bg,
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Text = "Philippine Calendar",
                Font = Theme.H2,
                ForeColor = Theme.TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Theme.Bg
            };
            calendarDateLabel = new Label
            {
                Font = Theme.BodyBold,
                ForeColor = Theme.TextGray,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Theme.Bg
            };
            calendarMonth = new MonthCalendar
            {
                Dock = DockStyle.Top,
                MaxSelectionCount = 1,
                CalendarDimensions = new Size(1, 1),
                FirstDayOfWeek = Day.Sunday,
                BackColor = Color.White
            };
            calendarMonth.DateChanged += (s, e) => LoadCalendar();

            calendarEventsGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            GridStyle.Apply(calendarEventsGrid);
            var eventsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Bg };
            var approveOvertime = new ModernButton
            {
                Text = "Approve Selected OT",
                Location = new Point(0, 4),
                Size = new Size(190, 34),
                SurroundColor = Theme.Bg
            };
            approveOvertime.Click += (s, e) => ApproveSelectedOvertime();
            toolbar.Controls.Add(approveOvertime);
            eventsPanel.Controls.Add(calendarEventsGrid);
            eventsPanel.Controls.Add(toolbar);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(calendarDateLabel, 1, 0);
            layout.Controls.Add(calendarMonth, 0, 1);
            layout.Controls.Add(eventsPanel, 1, 1);
            page.Controls.Add(layout);
            return page;
        }

        private void LoadCalendar()
        {
            try
            {
                DateTime selected = calendarMonth.SelectionStart.Date;
                calendarDateLabel.Text = selected.ToString("dddd, MMMM d, yyyy");
                DateTime monthStart = new DateTime(selected.Year, selected.Month, 1);
                DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var holidays = CompanyService.ListHolidays(selected.Year);
                var attendance = AttendanceService.ListForDate(selected);
                var monthLeaveRequests = LeaveService.ListForRange(monthStart, monthEnd);
                var leaveRequests = monthLeaveRequests.Where(leave =>
                    selected >= leave.StartDate.Date && selected <= leave.EndDate.Date).ToList();
                var table = new DataTable();
                table.Columns.Add("EmployeeID", typeof(int));
                table.Columns.Add("AttendanceDate", typeof(DateTime));
                table.Columns.Add("Event");
                table.Columns.Add("Category");
                table.Columns.Add("Employee");
                table.Columns.Add("Time In");
                table.Columns.Add("Break Out");
                table.Columns.Add("Break In");
                table.Columns.Add("Time Out");
                table.Columns.Add("Overtime Hours");
                table.Columns.Add("Overtime Approval");
                table.Columns.Add("OvertimeMinutes", typeof(int));
                table.Columns.Add("OvertimeApproved", typeof(bool));
                table.Columns.Add("Undertime");
                calendarMonth.RemoveAllBoldedDates();
                foreach (var h in holidays)
                {
                    if (h.HolidayDate < monthStart || h.HolidayDate > monthEnd) continue;
                    calendarMonth.AddBoldedDate(h.HolidayDate.Date);
                }
                foreach (var record in AttendanceService.ListForRange(monthStart, monthEnd))
                    calendarMonth.AddBoldedDate(record.AttendanceDate.Date);
                foreach (var leave in monthLeaveRequests.Where(item => item.Status == "Approved" && item.Paid))
                {
                    DateTime leaveStart = leave.StartDate.Date < monthStart ? monthStart : leave.StartDate.Date;
                    DateTime leaveEnd = leave.EndDate.Date > monthEnd ? monthEnd : leave.EndDate.Date;
                    for (DateTime date = leaveStart; date <= leaveEnd; date = date.AddDays(1))
                        calendarMonth.AddBoldedDate(date);
                }
                foreach (var h in holidays.Where(holiday => holiday.HolidayDate == selected))
                    table.Rows.Add(DBNull.Value, selected, h.HolidayName, h.HolidayType + " Holiday",
                        "", "", "", "", "", "", DBNull.Value, DBNull.Value);
                foreach (var record in attendance)
                {
                    int overtimeMinutes = record.TimeOut.HasValue ? AttendanceService.GetOvertimeMinutes(record) : 0;
                    table.Rows.Add(record.EmployeeId, record.AttendanceDate.Date, record.Status, "Attendance", record.EmployeeName,
                        record.TimeIn?.ToString(@"hh\:mm") ?? "",
                        record.BreakOut?.ToString(@"hh\:mm") ?? "",
                        record.BreakIn?.ToString(@"hh\:mm") ?? "",
                        record.TimeOut?.ToString(@"hh\:mm") ?? "",
                        (overtimeMinutes / 60m).ToString("0.##"),
                        overtimeMinutes == 0 ? "—" : record.OvertimeApproved ? "Approved" : "Pending",
                        overtimeMinutes, record.OvertimeApproved, UndertimeText(record));
                }
                foreach (var leave in leaveRequests)
                {
                    string leaveStatus = leave.Status == "Approved" && leave.Paid
                        ? "Approved (Paid)"
                        : leave.Status;
                    table.Rows.Add(DBNull.Value, selected, "Leave", leaveStatus, leave.EmployeeName,
                        "", "", "", "", "", "", DBNull.Value, DBNull.Value);
                }
                calendarMonth.UpdateBoldedDates();
                calendarEventsGrid.DataSource = table;
                calendarEventsGrid.Columns["EmployeeID"]!.Visible = false;
                calendarEventsGrid.Columns["AttendanceDate"]!.Visible = false;
                calendarEventsGrid.Columns["OvertimeMinutes"]!.Visible = false;
                calendarEventsGrid.Columns["OvertimeApproved"]!.Visible = false;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load calendar: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // e.g. "1h 20m (Needs follow-up)" until management has talked to the employee.
        private static string UndertimeText(AttendanceRecord record)
        {
            int minutes = AttendanceService.GetUndertimeMinutes(record);
            if (minutes == 0) return "—";
            string state = record.UndertimeStatus == "Open" ? " (Needs follow-up)"
                         : record.UndertimeStatus == "Resolved" ? " (Discussed)" : "";
            return AttendanceService.FormatDuration(minutes) + state;
        }

        private void ApproveSelectedOvertime()
        {
            if (calendarEventsGrid.CurrentRow == null ||
                calendarEventsGrid.CurrentRow.Cells["EmployeeID"].Value is not int employeeId ||
                calendarEventsGrid.CurrentRow.Cells["OvertimeMinutes"].Value is not int overtimeMinutes ||
                overtimeMinutes <= 0)
            {
                MessageBox.Show("Select a completed attendance row with overtime.", "Overtime Approval",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (Convert.ToBoolean(calendarEventsGrid.CurrentRow.Cells["OvertimeApproved"].Value))
            {
                MessageBox.Show("This overtime has already been approved.", "Overtime Approval",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DateTime attendanceDate = Convert.ToDateTime(calendarEventsGrid.CurrentRow.Cells["AttendanceDate"].Value);
            try
            {
                AttendanceService.SetOvertimeApproval(employeeId, attendanceDate, true);
                LoadCalendar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not approve overtime: " + ex.Message, "Overtime Approval",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private TabPage BuildLeaveRequestsTab()
        {
            var page = new TabPage("Leave Requests") { BackColor = Theme.Bg, Padding = new Padding(20) };
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Bg };
            var approve = new ModernButton
            {
                Text = "Approve as Paid", Location = new Point(0, 4), Size = new Size(150, 34),
                SurroundColor = Theme.Bg
            };
            approve.Click += (s, e) => ReviewSelectedLeave(true);
            var reject = new ModernButton
            {
                Text = "Reject", Location = new Point(160, 4), Size = new Size(100, 34),
                SurroundColor = Theme.Bg, FillColor = Color.FromArgb(229, 231, 235)
            };
            reject.ForeColor = Theme.TextDark;
            reject.Click += (s, e) => ReviewSelectedLeave(false);

            leaveRequestsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            GridStyle.Apply(leaveRequestsGrid);
            toolbar.Controls.Add(approve);
            toolbar.Controls.Add(reject);
            page.Controls.Add(leaveRequestsGrid);
            page.Controls.Add(toolbar);
            return page;
        }

        private void LoadLeaveRequests()
        {
            try
            {
                var requests = LeaveService.ListForRange(new DateTime(2000, 1, 1), new DateTime(2100, 12, 31));
                var table = new DataTable();
                table.Columns.Add("LeaveRequestID", typeof(int));
                table.Columns.Add("Employee");
                table.Columns.Add("Start Date");
                table.Columns.Add("End Date");
                table.Columns.Add("Reason");
                table.Columns.Add("Status");
                table.Columns.Add("Paid", typeof(bool));
                foreach (var request in requests)
                    table.Rows.Add(request.LeaveRequestId, request.EmployeeName,
                        request.StartDate.ToString("MMM d, yyyy"), request.EndDate.ToString("MMM d, yyyy"),
                        request.Reason, request.Status, request.Paid);
                leaveRequestsGrid.DataSource = table;
                leaveRequestsGrid.Columns["LeaveRequestID"]!.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load leave requests: " + ex.Message, "Leave Requests",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ReviewSelectedLeave(bool approved)
        {
            if (leaveRequestsGrid.CurrentRow == null ||
                leaveRequestsGrid.CurrentRow.Cells["LeaveRequestID"].Value is not int requestId)
            {
                MessageBox.Show("Select a leave request first.", "Leave Requests",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                LeaveService.Review(requestId, approved, reviewerName);
                LoadLeaveRequests();
                LoadCalendar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not review leave request: " + ex.Message, "Leave Requests",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ------------------------------------------------------------------
        // DEPARTMENTS
        // ------------------------------------------------------------------
        private TabPage BuildDepartmentsTab()
        {
            var page = new TabPage("Departments") { BackColor = Theme.Bg, Padding = new Padding(20) };

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44 };
            departmentName = new TextBox { Location = new Point(0, 6), Width = 260, PlaceholderText = "Department name" };
            var addBtn = new ModernButton { Text = "Add", Location = new Point(270, 3), Size = new Size(90, 32), SurroundColor = Theme.Bg };
            addBtn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(departmentName.Text))
                {
                    MessageBox.Show("Enter a department name.", "Missing Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                try
                {
                    CompanyService.AddDepartment(departmentName.Text.Trim());
                    departmentName.Clear();
                    LoadDepartments();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not add department: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            toolbar.Controls.Add(departmentName);
            toolbar.Controls.Add(addBtn);

            var deleteBtn = new ModernButton { Text = "Delete Selected", Dock = DockStyle.Top, Height = 32, SurroundColor = Theme.Bg, FillColor = Color.FromArgb(229, 231, 235) };
            deleteBtn.ForeColor = Theme.TextDark;
            deleteBtn.Click += (s, e) =>
            {
                if (departmentsGrid.CurrentRow?.Cells["DepartmentID"].Value is not int id) return;
                try { CompanyService.DeleteDepartment(id); LoadDepartments(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };

            departmentsGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            GridStyle.Apply(departmentsGrid);

            page.Controls.Add(departmentsGrid);
            page.Controls.Add(deleteBtn);
            page.Controls.Add(toolbar);
            return page;
        }

        private void LoadDepartments()
        {
            try
            {
                var departments = CompanyService.ListDepartments();
                var table = new DataTable();
                table.Columns.Add("DepartmentID", typeof(int));
                table.Columns.Add("Name");
                foreach (var d in departments)
                    table.Rows.Add(d.DepartmentId, d.DepartmentName);
                departmentsGrid.DataSource = table;
                departmentsGrid.Columns["DepartmentID"]!.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load departments: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}