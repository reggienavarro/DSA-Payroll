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
        private TextBox riceBox = null!, uniformBox = null!, laundryBox = null!, incentivesBox = null!, hourlyRateBox = null!;
        private DataGridView calendarEventsGrid = null!;
        private MonthCalendar calendarMonth = null!;
        private Label calendarDateLabel = null!;
        private DataGridView departmentsGrid = null!;
        private TextBox departmentName = null!;
        private readonly System.Windows.Forms.Timer calendarRefreshTimer = new() { Interval = 30000 };

        public CompanyPanel()
        {
            BackColor = Theme.Bg;
            Padding = new Padding(24);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildDefaultsTab());
            tabs.TabPages.Add(BuildCalendarTab());
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
            LoadDepartments();
        }

        // ------------------------------------------------------------------
        // PAYROLL DEFAULTS
        // ------------------------------------------------------------------
        private TabPage BuildDefaultsTab()
        {
            var page = new TabPage("Payroll Defaults") { BackColor = Theme.Bg, Padding = new Padding(20) };
            var card = new RoundedPanel { Location = new Point(0, 0), Size = new Size(420, 320), SurroundColor = Theme.Bg };

            var info = new Label
            {
                Text = "These are the starting values every new payslip is generated with. " +
                       "Management can still edit any individual payslip afterward.",
                Font = Theme.Small, ForeColor = Theme.TextGray, Location = new Point(16, 14),
                Size = new Size(388, 44), BackColor = Color.White
            };

            riceBox = AddDefaultField(card, "Rice Allowance", 70);
            uniformBox = AddDefaultField(card, "Uniform", 120);
            laundryBox = AddDefaultField(card, "Laundry", 170);
            incentivesBox = AddDefaultField(card, "Incentives", 220);
            hourlyRateBox = AddDefaultField(card, "Default Hourly Rate", 270);

            var saveBtn = new ModernButton { Text = "Save Defaults", Location = new Point(16, 316), Size = new Size(388, 40), SurroundColor = Theme.Bg };
            saveBtn.Click += (s, e) =>
            {
                try
                {
                    CompanyService.SavePayrollDefaults(new PayrollDefaultsConfig
                    {
                        RiceAllowance = ParseOrZero(riceBox),
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

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(calendarDateLabel, 1, 0);
            layout.Controls.Add(calendarMonth, 0, 1);
            layout.Controls.Add(calendarEventsGrid, 1, 1);
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
                var table = new DataTable();
                table.Columns.Add("Event");
                table.Columns.Add("Category");
                table.Columns.Add("Employee");
                table.Columns.Add("Time In");
                table.Columns.Add("Time Out");
                calendarMonth.RemoveAllBoldedDates();
                foreach (var h in holidays)
                {
                    if (h.HolidayDate < monthStart || h.HolidayDate > monthEnd) continue;
                    calendarMonth.AddBoldedDate(h.HolidayDate.Date);
                }
                foreach (var record in AttendanceService.ListForRange(monthStart, monthEnd))
                    calendarMonth.AddBoldedDate(record.AttendanceDate.Date);
                foreach (var h in holidays.Where(holiday => holiday.HolidayDate == selected))
                    table.Rows.Add(h.HolidayName, h.HolidayType + " Holiday", "", "", "");
                foreach (var record in attendance)
                {
                    table.Rows.Add(record.Status, "Attendance", record.EmployeeName,
                        record.TimeIn?.ToString(@"hh\:mm") ?? "", record.TimeOut?.ToString(@"hh\:mm") ?? "");
                }
                calendarMonth.UpdateBoldedDates();
                calendarEventsGrid.DataSource = table;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load calendar: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                departmentsGrid.Columns["DepartmentID"].Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load departments: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}