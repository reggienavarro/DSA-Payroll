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
        private TextBox hourlyRateBox = null!, benefitNameBox = null!, benefitAmountBox = null!;
        private DataGridView benefitsGrid = null!;
        private DataGridView calendarEventsGrid = null!, calendarDepartmentsGrid = null!;
        private MonthCalendar calendarMonth = null!;
        private Label calendarDateLabel = null!, calendarHolidayLabel = null!;
        private int? selectedCalendarDepartmentId;
        private bool loadingCalendar;
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
            tabs.TabPages.Add(BuildBenefitsTab());
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
            LoadBenefits();
            LoadCalendar();
            LoadLeaveRequests();
            LoadDepartments();
        }

        // ------------------------------------------------------------------
        // COMPANY BENEFITS
        // ------------------------------------------------------------------
        private TabPage BuildBenefitsTab()
        {
            var page = new TabPage("Company Benefits") { BackColor = Theme.Bg, Padding = new Padding(20) };
            var info = new Label
            {
                Text = "Add company benefits and their default amounts. Each new payslip includes these benefits by default.",
                Font = Theme.Small, ForeColor = Theme.TextGray, Dock = DockStyle.Top,
                Height = 34, BackColor = Theme.Bg
            };
            var entryBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Bg };
            benefitNameBox = new TextBox { Location = new Point(0, 7), Width = 280, PlaceholderText = "Benefit name" };
            benefitAmountBox = new TextBox { Location = new Point(290, 7), Width = 130, PlaceholderText = "Default amount" };
            var addBenefit = new ModernButton
            {
                Text = "Add Benefit", Location = new Point(430, 4), Size = new Size(140, 36), SurroundColor = Theme.Bg
            };
            addBenefit.Click += (s, e) => AddBenefit();
            entryBar.Controls.Add(benefitNameBox);
            entryBar.Controls.Add(benefitAmountBox);
            entryBar.Controls.Add(addBenefit);

            var settingsBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Bg };
            var rateLabel = new Label
            {
                Text = "Default hourly rate for new employees",
                Location = new Point(0, 11), Size = new Size(260, 24),
                Font = Theme.Body, ForeColor = Theme.TextDark
            };
            hourlyRateBox = new TextBox { Location = new Point(270, 7), Width = 130, TextAlign = HorizontalAlignment.Right };
            var saveRate = new ModernButton
            {
                Text = "Save Rate", Location = new Point(410, 4), Size = new Size(120, 36), SurroundColor = Theme.Bg
            };
            saveRate.Click += (s, e) => SaveDefaultHourlyRate();
            settingsBar.Controls.Add(rateLabel);
            settingsBar.Controls.Add(hourlyRateBox);
            settingsBar.Controls.Add(saveRate);

            var deleteBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Bg };
            var deleteBenefit = new ModernButton
            {
                Text = "Delete Selected Benefit", Location = new Point(0, 4), Size = new Size(205, 34),
                SurroundColor = Theme.Bg, FillColor = Color.FromArgb(229, 231, 235)
            };
            deleteBenefit.ForeColor = Theme.TextDark;
            deleteBenefit.Click += (s, e) => DeleteSelectedBenefit();
            deleteBar.Controls.Add(deleteBenefit);

            benefitsGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            GridStyle.Apply(benefitsGrid);
            page.Controls.Add(benefitsGrid);
            page.Controls.Add(deleteBar);
            page.Controls.Add(settingsBar);
            page.Controls.Add(entryBar);
            page.Controls.Add(info);
            return page;
        }

        private void LoadBenefits()
        {
            try
            {
                var benefits = CompanyService.ListBenefits();
                var table = new DataTable();
                table.Columns.Add("BenefitID", typeof(int));
                table.Columns.Add("Benefit");
                table.Columns.Add("Default Amount", typeof(decimal));
                foreach (var benefit in benefits)
                    table.Rows.Add(benefit.BenefitId, benefit.BenefitName, benefit.DefaultAmount);
                benefitsGrid.DataSource = table;
                if (benefitsGrid.Columns["BenefitID"] is DataGridViewColumn idColumn)
                    idColumn.Visible = false;
                if (benefitsGrid.Columns["Default Amount"] is DataGridViewColumn amountColumn)
                    amountColumn.DefaultCellStyle.Format = "N2";
                hourlyRateBox.Text = CompanyService.GetPayrollDefaults().DefaultHourlyRate.ToString("0.00");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load company benefits: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddBenefit()
        {
            string name = benefitNameBox.Text.Trim();
            if (name.Length == 0 || !decimal.TryParse(benefitAmountBox.Text, out decimal amount) || amount < 0)
            {
                MessageBox.Show("Enter a benefit name and a non-negative amount.", "Check benefit details",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                CompanyService.AddBenefit(name, amount);
                benefitNameBox.Clear();
                benefitAmountBox.Clear();
                LoadBenefits();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not add benefit: " + ex.Message, "Company Benefits",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteSelectedBenefit()
        {
            if (benefitsGrid.CurrentRow?.Cells["BenefitID"].Value is not int id) return;
            if (MessageBox.Show(this, "Remove this benefit from future payslips? Existing saved payslips keep their recorded amount.",
                    "Delete Benefit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                CompanyService.DeleteBenefit(id);
                LoadBenefits();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not delete benefit: " + ex.Message, "Company Benefits",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveDefaultHourlyRate()
        {
            if (!decimal.TryParse(hourlyRateBox.Text, out decimal rate) || rate <= 0)
            {
                MessageBox.Show("Enter a positive hourly rate.", "Hourly Rate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                var defaults = CompanyService.GetPayrollDefaults();
                defaults.DefaultHourlyRate = rate;
                CompanyService.SavePayrollDefaults(defaults);
                MessageBox.Show("Default hourly rate saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save hourly rate: " + ex.Message, "Database Error",
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
                Text = "Calendar & Attendance",
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
            calendarDepartmentsGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            GridStyle.Apply(calendarDepartmentsGrid);
            calendarDepartmentsGrid.SelectionChanged += (s, e) =>
            {
                if (!loadingCalendar) LoadCalendarDepartmentAttendance();
            };

            var details = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                BackColor = Theme.Bg, Padding = new Padding(0)
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            calendarHolidayLabel = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.BodyBold, ForeColor = Theme.TextDark,
                BackColor = Color.White, Padding = new Padding(12, 0, 8, 0),
                AutoEllipsis = true
            };
            var departmentHeading = new Label
            {
                Text = "DEPARTMENTS  ·  ACTIVE EMPLOYEES", Dock = DockStyle.Fill,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray,
                TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.Bg
            };
            var attendanceToolbar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var attendanceHeading = new Label
            {
                Text = "Employee attendance", Font = Theme.BodyBold,
                ForeColor = Theme.TextDark, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.Bg
            };
            var approveOvertime = new ModernButton
            {
                Text = "Approve Selected OT",
                Dock = DockStyle.Right,
                Size = new Size(190, 34),
                SurroundColor = Theme.Bg
            };
            approveOvertime.Click += (s, e) => ApproveSelectedOvertime();
            attendanceToolbar.Controls.Add(attendanceHeading);
            attendanceToolbar.Controls.Add(approveOvertime);
            details.Controls.Add(calendarHolidayLabel, 0, 0);
            details.Controls.Add(departmentHeading, 0, 1);
            details.Controls.Add(calendarDepartmentsGrid, 0, 2);
            details.Controls.Add(attendanceToolbar, 0, 3);
            details.Controls.Add(calendarEventsGrid, 0, 4);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(calendarDateLabel, 1, 0);
            layout.Controls.Add(calendarMonth, 0, 1);
            layout.Controls.Add(details, 1, 1);
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
                var monthLeaveRequests = LeaveService.ListForRange(monthStart, monthEnd);
                var selectedHolidays = holidays.Where(holiday => holiday.HolidayDate.Date == selected).ToList();
                calendarHolidayLabel.Text = selectedHolidays.Count == 0
                    ? "No holiday on this date"
                    : "HOLIDAY  ·  " + string.Join("  /  ", selectedHolidays.Select(holiday =>
                        $"{holiday.HolidayName} ({holiday.HolidayType})"));
                calendarHolidayLabel.ForeColor = selectedHolidays.Count == 0
                    ? Theme.TextGray : Color.FromArgb(4, 120, 87);

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
                calendarMonth.UpdateBoldedDates();
                LoadCalendarDepartments();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load calendar: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadCalendarDepartments()
        {
            loadingCalendar = true;
            var departments = CompanyService.ListDepartments();
            var table = new DataTable();
            table.Columns.Add("DepartmentID", typeof(int));
            table.Columns.Add("Department");
            table.Columns.Add("Active Employees", typeof(int));
            foreach (var department in departments)
                table.Rows.Add(department.DepartmentId, department.DepartmentName, department.ActiveEmployeeCount);
            calendarDepartmentsGrid.DataSource = table;
            if (calendarDepartmentsGrid.Columns["DepartmentID"] is DataGridViewColumn idColumn)
                idColumn.Visible = false;
            if (calendarDepartmentsGrid.Columns["Active Employees"] is DataGridViewColumn countColumn)
            {
                countColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                countColumn.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                countColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            if (calendarDepartmentsGrid.Columns["Department"] is DataGridViewColumn nameColumn)
                nameColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            int targetDepartmentId = selectedCalendarDepartmentId ?? departments.FirstOrDefault()?.DepartmentId ?? 0;
            int targetIndex = departments.FindIndex(department => department.DepartmentId == targetDepartmentId);
            if (targetIndex < 0 && departments.Count > 0) targetIndex = 0;
            if (targetIndex >= 0 && targetIndex < calendarDepartmentsGrid.Rows.Count)
            {
                calendarDepartmentsGrid.ClearSelection();
                calendarDepartmentsGrid.Rows[targetIndex].Selected = true;
                calendarDepartmentsGrid.CurrentCell = calendarDepartmentsGrid.Rows[targetIndex].Cells["Department"];
                selectedCalendarDepartmentId = departments[targetIndex].DepartmentId;
            }
            else
            {
                selectedCalendarDepartmentId = null;
                calendarEventsGrid.DataSource = null;
            }
            loadingCalendar = false;
            LoadCalendarDepartmentAttendance();
        }

        private void LoadCalendarDepartmentAttendance()
        {
            if (loadingCalendar || calendarDepartmentsGrid.CurrentRow?.Cells["DepartmentID"].Value is not int departmentId)
                return;
            selectedCalendarDepartmentId = departmentId;
            DateTime selected = calendarMonth.SelectionStart.Date;
            var attendance = CompanyService.ListDepartmentAttendance(departmentId, selected);
            var leaveByEmployee = LeaveService.ListForRange(selected, selected)
                .GroupBy(leave => leave.EmployeeId)
                .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(leave =>
                    leave.Status == "Approved" && leave.Paid ? "Approved (Paid)" : leave.Status)));
            var table = new DataTable();
            table.Columns.Add("EmployeeID", typeof(int));
            table.Columns.Add("AttendanceDate", typeof(DateTime));
            table.Columns.Add("Employee");
            table.Columns.Add("Status");
            table.Columns.Add("Time In");
            table.Columns.Add("Break Out");
            table.Columns.Add("Break In");
            table.Columns.Add("Time Out");
            table.Columns.Add("Overtime Hours");
            table.Columns.Add("Overtime Approval");
            table.Columns.Add("Leave");
            table.Columns.Add("OvertimeMinutes", typeof(int));
            table.Columns.Add("OvertimeApproved", typeof(bool));
            table.Columns.Add("Undertime");
            foreach (var record in attendance)
            {
                int overtimeMinutes = record.TimeOut.HasValue ? AttendanceService.GetOvertimeMinutes(record) : 0;
                table.Rows.Add(record.EmployeeId, record.AttendanceDate.Date, record.EmployeeName,
                    record.Status,
                    record.TimeIn?.ToString(@"hh\:mm") ?? "—",
                    record.BreakOut?.ToString(@"hh\:mm") ?? "—",
                    record.BreakIn?.ToString(@"hh\:mm") ?? "—",
                    record.TimeOut?.ToString(@"hh\:mm") ?? "—",
                    (overtimeMinutes / 60m).ToString("0.##"),
                    overtimeMinutes == 0 ? "—" : record.OvertimeApproved ? "Approved" : "Pending",
                    leaveByEmployee.TryGetValue(record.EmployeeId, out var leaveStatus) ? leaveStatus : "—",
                    overtimeMinutes, record.OvertimeApproved, UndertimeText(record));
            }
            calendarEventsGrid.DataSource = table;
            calendarEventsGrid.Columns["EmployeeID"]!.Visible = false;
            calendarEventsGrid.Columns["AttendanceDate"]!.Visible = false;
            calendarEventsGrid.Columns["OvertimeMinutes"]!.Visible = false;
            calendarEventsGrid.Columns["OvertimeApproved"]!.Visible = false;
            if (calendarEventsGrid.Columns["Employee"] is DataGridViewColumn employeeColumn)
                employeeColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            foreach (string columnName in new[] { "Time In", "Break Out", "Break In", "Time Out", "Overtime Hours" })
                if (calendarEventsGrid.Columns[columnName] is DataGridViewColumn timeColumn)
                    timeColumn.MinimumWidth = 78;
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
