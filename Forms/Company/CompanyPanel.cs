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
    // its content area, the same way EmployeeManagementForm gets embedded for "Employee."
    public class CompanyPanel : Panel
    {
        private TextBox hourlyRateBox = null!, benefitNameBox = null!, benefitAmountBox = null!;
        private DataGridView benefitsGrid = null!;
        private DataGridView calendarEventsGrid = null!;
        private FlowLayoutPanel calendarDepartmentCards = null!;
        private MonthCalendar calendarMonth = null!;
        private Label calendarDateLabel = null!, calendarHolidayLabel = null!;
        private int? selectedCalendarDepartmentId;
        private bool loadingCalendar;
        private DataGridView departmentsGrid = null!;
        private TextBox departmentName = null!;
        private readonly System.Windows.Forms.Timer calendarRefreshTimer = new() { Interval = 30000 };
        private TabControl companyTabs = null!;

        public CompanyPanel()
        {
            BackColor = Theme.Bg;
            Padding = new Padding(24);

            companyTabs = new TabControl { Dock = DockStyle.Fill };
            companyTabs.TabPages.Add(BuildBenefitsTab());
            companyTabs.TabPages.Add(BuildCalendarTab());
            companyTabs.TabPages.Add(BuildDepartmentsTab());
            Controls.Add(companyTabs);

            calendarRefreshTimer.Tick += (s, e) =>
            {
                if (Visible && companyTabs.SelectedTab?.Text == "Calendar") LoadCalendar();
            };
            calendarRefreshTimer.Start();
        }

        public void RefreshAll()
        {
            LoadBenefits();
            LoadCalendar();
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
            calendarEventsGrid.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || calendarEventsGrid.Columns[e.ColumnIndex].Name != "Undertime") return;
                if (calendarEventsGrid.CurrentRow?.Cells["EmployeeID"].Value is not int employeeId ||
                    calendarEventsGrid.CurrentRow.Cells["AttendanceDate"].Value is not DateTime date) return;
                try
                {
                    var record = AttendanceService.ListForDate(date, employeeId).Find(item => item.EmployeeId == employeeId);
                    if (record != null && AttendanceService.GetUndertimeMinutes(record) > 0)
                        using (var receipt = new UndertimeReceiptForm(record)) receipt.ShowDialog(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not open the undertime record: " + ex.Message,
                        "Company Calendar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            calendarEventsGrid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && calendarEventsGrid.Columns[e.ColumnIndex].Name == "Undertime" &&
                    !string.IsNullOrWhiteSpace(e.Value?.ToString()) && e.Value?.ToString() != "â€”")
                {
                    e.CellStyle.ForeColor = Theme.Accent;
                    calendarEventsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = "Click to view the undertime follow-up record";
                }
            };
            calendarDepartmentCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight, BackColor = Color.White,
                Padding = new Padding(6)
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
                Text = "CHOOSE A DEPARTMENT  ·  CHECKED IN ON THIS DATE", Dock = DockStyle.Fill,
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
            details.Controls.Add(calendarDepartmentCards, 0, 2);
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
            var checkInCounts = CompanyService.CountDepartmentCheckIns(calendarMonth.SelectionStart.Date);
            int targetDepartmentId = selectedCalendarDepartmentId ?? departments.FirstOrDefault()?.DepartmentId ?? 0;
            int targetIndex = departments.FindIndex(department => department.DepartmentId == targetDepartmentId);
            if (targetIndex < 0 && departments.Count > 0) targetIndex = 0;
            selectedCalendarDepartmentId = targetIndex >= 0 ? departments[targetIndex].DepartmentId : null;
            calendarDepartmentCards.SuspendLayout();
            calendarDepartmentCards.Controls.Clear();
            if (departments.Count == 0)
            {
                calendarDepartmentCards.Controls.Add(new Label
                {
                    Text = "No departments have been added yet.", AutoSize = true,
                    ForeColor = Theme.TextGray, Font = Theme.Body, Margin = new Padding(8, 10, 8, 8)
                });
            }
            else
            {
                foreach (var department in departments)
                {
                    int departmentId = department.DepartmentId;
                    int checkedInCount = checkInCounts.TryGetValue(departmentId, out int count) ? count : 0;
                    bool selected = selectedCalendarDepartmentId == departmentId;
                    var card = new Panel
                    {
                        Size = new Size(205, 76), Margin = new Padding(4),
                        BackColor = selected ? Color.FromArgb(239, 246, 255) : Color.White,
                        BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, Tag = departmentId
                    };
                    var name = new Label
                    {
                        Text = department.DepartmentName, Location = new Point(10, 9),
                        Size = new Size(181, 25), AutoEllipsis = true,
                        Font = Theme.BodyBold, ForeColor = selected ? Theme.Accent : Theme.TextDark,
                        Cursor = Cursors.Hand, BackColor = Color.Transparent
                    };
                    var countLabel = new Label
                    {
                        Text = $"{checkedInCount} checked in", Location = new Point(10, 39),
                        Size = new Size(181, 20), Font = Theme.Small,
                        ForeColor = selected ? Theme.Accent : Theme.TextGray,
                        Cursor = Cursors.Hand, BackColor = Color.Transparent
                    };
                    card.Click += (s, e) => SelectCalendarDepartment(departmentId);
                    name.Click += (s, e) => SelectCalendarDepartment(departmentId);
                    countLabel.Click += (s, e) => SelectCalendarDepartment(departmentId);
                    card.Controls.Add(name);
                    card.Controls.Add(countLabel);
                    calendarDepartmentCards.Controls.Add(card);
                }
            }
            calendarDepartmentCards.ResumeLayout();
            loadingCalendar = false;
            if (selectedCalendarDepartmentId.HasValue)
                LoadCalendarDepartmentAttendance();
            else
                calendarEventsGrid.DataSource = null;
        }

        private void SelectCalendarDepartment(int departmentId)
        {
            if (selectedCalendarDepartmentId == departmentId) return;
            selectedCalendarDepartmentId = departmentId;
            foreach (Control control in calendarDepartmentCards.Controls)
            {
                if (control is not Panel card || card.Tag is not int cardDepartmentId) continue;
                bool selected = cardDepartmentId == departmentId;
                card.BackColor = selected ? Color.FromArgb(239, 246, 255) : Color.White;
                foreach (Control child in card.Controls)
                {
                    child.ForeColor = selected ? Theme.Accent
                        : child is Label label && label.Text.EndsWith("checked in", StringComparison.Ordinal)
                            ? Theme.TextGray : Theme.TextDark;
                }
            }
            LoadCalendarDepartmentAttendance();
        }

        private void LoadCalendarDepartmentAttendance()
        {
            if (loadingCalendar || selectedCalendarDepartmentId is not int departmentId) return;
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
