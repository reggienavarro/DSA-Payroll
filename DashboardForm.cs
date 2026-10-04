using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    // Redesigned dashboard matching the reference image, built in code only
    // (no .Designer.cs). The class name is unchanged, so LoginForm keeps
    // working with zero edits.
    public class DashboardForm : Form
    {
        // ----- controls created in BuildMain() (assigned there, hence null!) -----
        private Label headerTitle = null!;
        private DashboardCard dateCard = null!, netPayCard = null!, grossPayCard = null!;
        private PaymentHistoryChart payrollChart = null!;
        private PayrollSummaryChart summaryChart = null!;
        private DataGridView employeeGrid = null!;
        private RoundedPanel employeeSection = null!;
        private Label employeeCountLabel = null!;
        private Label warningBanner = null!;
        private ProfileChip profileChip = null!;
        private NotificationBell bell = null!;
        private readonly System.Windows.Forms.Timer notificationTimer = new() { Interval = 30000 };
        private int announcedNotices;   // open undertime notices management has already been told about
        private SidebarButton[] navButtons = Array.Empty<SidebarButton>();

        private readonly Panel contentHost = new Panel();      // dashboard view OR embedded Form1
        private readonly Panel dashboardContent = new Panel(); // cards + charts + employee table
        private Panel? ticketsContent;                         // built lazily on first visit
        private Panel? payslipsContent;                        // built lazily on first visit
        private CompanyPanel? companyContent;                  // built lazily on first visit
        private DataGridView ticketsGrid = null!;
        private DataGridView payslipsGrid = null!;
        private Form? hostedEmployeeForm;                      // Form1, embedded like the old app did
        private bool loggingOut;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CurrentUser { get; set; }

        // Default "Admin" keeps LoginForm compiling without any change.
        public DashboardForm(string loggedInUser = "Admin")
        {
            CurrentUser = loggedInUser;
            Text = "Payroll Dashboard";                     // same title as before
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromPoint(Cursor.Position).Bounds;
            BackColor = Theme.Bg;
            Font = Theme.Body;

            BuildMain();
            BuildSidebar();

            Load += (s, e) => RefreshData(); // old form loaded stats on startup too

            // Undertime notices: badge on the bell as soon as the dashboard opens, then re-check every 30 s.
            Shown += (s, e) => { RefreshNotifications(); notificationTimer.Start(); };
            notificationTimer.Tick += (s, e) => RefreshNotifications();
            FormClosed += (s, e) => notificationTimer.Dispose();

            FormClosing += (s, e) =>
            {
                // The old app left a hidden LoginForm running when the dashboard
                // was closed with X. This ends the app instead. Remove if unwanted.
                if (!loggingOut) Application.Exit();
            };
        }

        // ------------------------------------------------------------------
        // SIDEBAR (dark navy, 224px, like the reference)
        // ------------------------------------------------------------------
        private void BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 224, BackColor = Theme.Navy };

            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, BackColor = Theme.Navy, Padding = new Padding(0, 12, 0, 0)
            };

            // Only real destinations — your app has these three screens.
            var items = new (string key, string text, IconKind icon)[]
            {
                ("overview", "Overview", IconKind.Grid),
                ("employee", "Employee", IconKind.Users),
                ("tickets", "Tickets", IconKind.Bell),
                ("payslips", "Payslips", IconKind.Card),
                ("company", "Company", IconKind.Bank),
            };

            navButtons = new SidebarButton[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var btn = new SidebarButton
                {
                    Text = it.text, Icon = it.icon, Tag = it.key,
                    Width = 224, Height = 46, Margin = new Padding(0)
                };
                btn.Click += (s, e) => Navigate(it.key);
                nav.Controls.Add(btn);
                navButtons[i] = btn;
            }
            navButtons[0].Active = true;

            var logoutBar = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Theme.Navy };
            var logoutBtn = new SidebarButton
            { Text = "Logout", Icon = IconKind.Logout, Width = 224, Height = 46, Location = new Point(0, 7) };
            logoutBtn.Click += (s, e) => Logout();
            logoutBar.Controls.Add(logoutBtn);

            var logoBar = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Navy };
            logoBar.Paint += (s, e) =>
            {
                using (var path = Gfx.Round(new RectangleF(24, 20, 38, 38), 10))
                using (var b = new SolidBrush(Theme.Accent)) e.Graphics.FillPath(b, path);
                TextRenderer.DrawText(e.Graphics, "P", Theme.H2, new Rectangle(24, 20, 38, 38), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, "PAYROLL", new Font("Segoe UI Semibold", 11.5f),
                    new Point(72, 28), Color.White);
            };

            // Docking processes children in reverse add order:
            // logo (added last) gets the top edge, logout the bottom, nav the middle.
            sidebar.Controls.Add(nav);
            sidebar.Controls.Add(logoutBar);
            sidebar.Controls.Add(logoBar);

            Controls.Add(sidebar); // added AFTER the main panel → takes the left edge
        }

        // ------------------------------------------------------------------
        // MAIN AREA (header + content host)
        // ------------------------------------------------------------------
        private void BuildMain()
        {
            var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(24, 14, 24, 16) };

            // ----- header: title left; Add-Staff, bell, profile right -----
            var header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Theme.Bg };
            headerTitle = new Label
            {
                Text = "Payroll Overview", Font = Theme.H1, ForeColor = Theme.TextDark,
                AutoSize = true, Location = new Point(2, 14), BackColor = Theme.Bg
            };
            var right = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, AutoSize = true, WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight, BackColor = Theme.Bg,
                Padding = new Padding(0, 12, 0, 0)
            };

            var addStaff = new ModernButton { Text = "Add New Staff", ShowPlus = true, Size = new Size(162, 38) };
            addStaff.Click += (s, e) => Navigate("employee"); // Form1 contains the ADD screen; Navigate also moves the sidebar highlight

            bell = new NotificationBell { Icon = IconKind.Bell, Size = new Size(40, 40), Margin = new Padding(14, 0, 0, 0) };
            bell.Click += (s, e) => OpenNotifications();

            profileChip = new ProfileChip { Size = new Size(170, 40), Margin = new Padding(14, 0, 0, 0), UserName = CurrentUser };

            right.Controls.Add(addStaff);
            right.Controls.Add(bell);
            right.Controls.Add(profileChip);
            header.Controls.Add(headerTitle);
            header.Controls.Add(right);

            // ----- warning banner: only shows up if a DB query actually fails -----
            warningBanner = new Label
            {
                Dock = DockStyle.Top, Height = 32, Visible = false,
                BackColor = Theme.RedBg, ForeColor = Theme.RedText, Font = Theme.SmallBold,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0)
            };

            // ----- content host: dashboard view OR the embedded Form1 -----
            contentHost.Dock = DockStyle.Fill;
            contentHost.BackColor = Theme.Bg;

            dashboardContent.Dock = DockStyle.Fill;
            dashboardContent.BackColor = Theme.Bg;
            dashboardContent.AutoScroll = true; // nothing disappears at small window sizes
            contentHost.Controls.Add(dashboardContent);

            // --- employee table (added first: Dock=Top stacks in reverse order) ---
            employeeSection = new RoundedPanel
            {
                Dock = DockStyle.Top, Height = 340,
                Padding = new Padding(16, 52, 16, 14), Margin = new Padding(0)
            };
            var empTitle = new Label
            {
                Text = "Employee List", Font = Theme.H2, ForeColor = Theme.TextDark,
                AutoSize = true, BackColor = Color.White, Location = new Point(20, 18)
            };
            employeeCountLabel = new Label { Font = Theme.Small, ForeColor = Theme.TextGray, AutoSize = true, BackColor = Color.White };
            employeeSection.Resize += (s, e) =>
                employeeCountLabel.Location = new Point(employeeSection.Width - employeeCountLabel.Width - 26, 24);

            employeeGrid = new DataGridView { Dock = DockStyle.Fill };
            GridStyle.Apply(employeeGrid);
            employeeGrid.CellPainting += GridStyle.PaintStatusBadges; // activates if a Status column exists
            employeeGrid.DataError += (s, e) => { e.ThrowException = false; };
            employeeGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Navigate("employee"); };

            employeeSection.Controls.Add(employeeGrid);
            employeeSection.Controls.Add(empTitle);
            employeeSection.Controls.Add(employeeCountLabel);

            // --- analytics row: line chart (58%) + donut (42%) ---
            var analytics = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 252, ColumnCount = 2,
                BackColor = Theme.Bg, Margin = new Padding(0, 0, 0, 14)
            };
            analytics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            analytics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));

            payrollChart = new PaymentHistoryChart
            {
                Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0),
                Title = "Payroll by Position" // real data — see DashboardData
            };
            summaryChart = new PayrollSummaryChart { Dock = DockStyle.Fill, Margin = new Padding(0) };

            analytics.Controls.Add(payrollChart, 0, 0);
            analytics.Controls.Add(summaryChart, 1, 0);

            // --- top row: three summary cards ---
            var topRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 118, ColumnCount = 3,
                BackColor = Theme.Bg, Margin = new Padding(0, 0, 0, 14)
            };
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));

            dateCard = new DashboardCard
            {
                Dock = DockStyle.Fill, Icon = IconKind.Calendar, ShowMiniBars = true,
                Margin = new Padding(0, 0, 10, 0), ValueFont = new Font("Segoe UI Semibold", 14f)
            };
            dateCard.Title = "Upcoming Salary Date";
            dateCard.Value = "--";   // honest: no pay-date column exists in Employees
            dateCard.Sub = " ";

            netPayCard = new DashboardCard
            {
                Dock = DockStyle.Fill, Icon = IconKind.Card,
                Margin = new Padding(0, 0, 10, 0), ValueFont = new Font("Segoe UI Semibold", 17f)
            };
            netPayCard.Title = "Total Net Pay";

            grossPayCard = new DashboardCard
            {
                Dock = DockStyle.Fill, Icon = IconKind.Bank, ValueFont = new Font("Segoe UI Semibold", 16f)
            };
            grossPayCard.Title = "Total Gross Pay";

            topRow.Controls.Add(dateCard, 0, 0);
            topRow.Controls.Add(netPayCard, 1, 0);
            topRow.Controls.Add(grossPayCard, 2, 0);

            // reverse order on purpose: topRow ends up on top, employee section at the bottom
            dashboardContent.Controls.Add(employeeSection);
            dashboardContent.Controls.Add(analytics);
            dashboardContent.Controls.Add(topRow);

            main.Controls.Add(contentHost);   // fill first...
            main.Controls.Add(warningBanner); // ...then this strip sits just below the header...
            main.Controls.Add(header);        // ...then top → header sits above everything
            Controls.Add(main);             // added BEFORE the sidebar → sidebar takes the left edge
        }

        // ------------------------------------------------------------------
        // NAVIGATION — same behaviors as the old button handlers
        // ------------------------------------------------------------------
        private void Navigate(string key)
        {
            foreach (var b in navButtons)
            {
                b.Active = (string?)b.Tag == key;
                b.Invalidate();
            }
            if (key == "overview") ShowOverview();
            else if (key == "employee") ShowEmployee();
            else if (key == "tickets") ShowTickets();
            else if (key == "payslips") ShowPayslips();
            else if (key == "company") ShowCompany();
        }

        // Old btnDashboard_Click: show the stats and reload them.
        private void ShowOverview()
        {
            if (hostedEmployeeForm != null)
            {
                contentHost.Controls.Remove(hostedEmployeeForm);
                hostedEmployeeForm.Dispose();
                hostedEmployeeForm = null;
            }
            if (ticketsContent != null) ticketsContent.Visible = false;
            if (payslipsContent != null) payslipsContent.Visible = false;
            if (companyContent != null) companyContent.Visible = false;
            dashboardContent.Visible = true;
            headerTitle.Text = "Payroll Overview";
            RefreshData();
        }

        // Old btnEmployees_Click: embed Form1 in the content area.
        private void ShowEmployee()
        {
            if (hostedEmployeeForm != null) return; // already open — keeps typed input
            if (ticketsContent != null) ticketsContent.Visible = false;
            if (payslipsContent != null) payslipsContent.Visible = false;
            if (companyContent != null) companyContent.Visible = false;
            dashboardContent.Visible = false;
            headerTitle.Text = "Employee Management";
            hostedEmployeeForm = new Form1
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill
            };
            contentHost.Controls.Add(hostedEmployeeForm);
            hostedEmployeeForm.Show();
        }

        // ------------------------------------------------------------------
        // TICKETS — salary disputes filed by employees through their portal
        // ------------------------------------------------------------------
        private void ShowTickets()
        {
            if (hostedEmployeeForm != null)
            {
                contentHost.Controls.Remove(hostedEmployeeForm);
                hostedEmployeeForm.Dispose();
                hostedEmployeeForm = null;
            }
            dashboardContent.Visible = false;
            headerTitle.Text = "Salary Dispute Tickets";
            if (payslipsContent != null) payslipsContent.Visible = false;
            if (companyContent != null) companyContent.Visible = false;

            if (ticketsContent == null)
            {
                ticketsContent = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(24) };
                ticketsGrid = new DataGridView { Dock = DockStyle.Fill };
                GridStyle.Apply(ticketsGrid);
                ticketsGrid.ReadOnly = true;
                ticketsGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) RespondToSelectedTicket(); };
                ticketsContent.Controls.Add(ticketsGrid);
                contentHost.Controls.Add(ticketsContent);
            }
            ticketsContent.Visible = true;
            ticketsContent.BringToFront();
            RefreshTickets();
        }

        private void RefreshTickets()
        {
            try
            {
                var tickets = TicketService.LoadAll();
                var table = new DataTable();
                table.Columns.Add("TicketID", typeof(int));
                table.Columns.Add("Employee");
                table.Columns.Add("Subject");
                table.Columns.Add("Status");
                table.Columns.Add("Filed On");
                foreach (var t in tickets)
                    table.Rows.Add(t.TicketId, t.EmployeeName, t.Subject, t.Status, t.CreatedAt.ToString("MMM d, yyyy"));

                ticketsGrid.DataSource = table;
                ticketsGrid.Columns["TicketID"]!.Visible = false; // kept for lookups, not shown
                ticketsGrid.Tag = tickets; // stash full rows (Message/AdminResponse) for the respond dialog
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load tickets: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Double-click a ticket row to open, respond, and mark it resolved.
        private void RespondToSelectedTicket()
        {
            if (ticketsGrid.CurrentRow == null) return;
            int ticketId = Convert.ToInt32(ticketsGrid.CurrentRow.Cells["TicketID"].Value);
            var tickets = ticketsGrid.Tag as List<TicketRow>;
            var ticket = tickets?.Find(t => t.TicketId == ticketId);
            if (ticket == null) return;

            using var form = new TicketResponseForm(ticket);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                TicketService.Respond(ticketId, form.Response);
                RefreshTickets();
            }
        }

        // ------------------------------------------------------------------
        // PAYSLIPS — itemized, cutoff-based payslip history + generation
        // ------------------------------------------------------------------
        private void ShowPayslips()
        {
            if (hostedEmployeeForm != null)
            {
                contentHost.Controls.Remove(hostedEmployeeForm);
                hostedEmployeeForm.Dispose();
                hostedEmployeeForm = null;
            }
            dashboardContent.Visible = false;
            if (ticketsContent != null) ticketsContent.Visible = false;
            if (companyContent != null) companyContent.Visible = false;
            headerTitle.Text = "Payslips";

            if (payslipsContent == null)
            {
                payslipsContent = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(24) };

                var hint = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Bg };
                var hintLabel = new Label
                {
                    Text = "This is payslip history. To generate a new payslip, open the Employee screen and select an employee.",
                    Font = Theme.Small, ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(0, 8), BackColor = Theme.Bg
                };
                hint.Controls.Add(hintLabel);

                payslipsGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
                GridStyle.Apply(payslipsGrid);
                payslipsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                payslipsGrid.CellDoubleClick += PayslipsGrid_CellDoubleClick;

                payslipsContent.Controls.Add(payslipsGrid);
                payslipsContent.Controls.Add(hint);
                contentHost.Controls.Add(payslipsContent);
            }
            payslipsContent.Visible = true;
            payslipsContent.BringToFront();
            RefreshPayslips();
        }

        private void RefreshPayslips()
        {
            try
            {
                var payslips = PayslipService.LoadAll();
                var table = new DataTable();
                table.Columns.Add("PayslipID", typeof(int));
                table.Columns.Add("Department");
                table.Columns.Add("Employee");
                table.Columns.Add("Cutoff");
                table.Columns.Add("No. of Days", typeof(decimal));
                table.Columns.Add("Net Pay");
                table.Columns.Add("Total Amount Receivable");
                foreach (var p in payslips)
                    table.Rows.Add(
                        p.PayslipId,
                        p.DepartmentName,
                        p.EmployeeName,
                        $"{p.CutoffStart:MMM d} - {p.CutoffEnd:MMM d, yyyy}",
                        p.NoOfDays,
                        Theme.Money(p.NetPay),
                        Theme.Money(p.TotalAmountReceivable));
                payslipsGrid.DataSource = table;
                payslipsGrid.Columns["PayslipID"].Visible = false;
                payslipsGrid.Columns["Department"].HeaderText = "Department";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load payslips: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PayslipsGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || payslipsGrid.Rows[e.RowIndex].Cells["PayslipID"].Value is not int id)
                return;

            try
            {
                var payslip = PayslipService.LoadAll().Find(p => p.PayslipId == id);
                if (payslip != null)
                    using (var form = new PayslipPreviewForm(payslip)) form.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open payslip: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------
        // COMPANY — payroll defaults, holiday calendar, departments
        // ------------------------------------------------------------------
        private void ShowCompany()
        {
            if (hostedEmployeeForm != null)
            {
                contentHost.Controls.Remove(hostedEmployeeForm);
                hostedEmployeeForm.Dispose();
                hostedEmployeeForm = null;
            }
            dashboardContent.Visible = false;
            if (ticketsContent != null) ticketsContent.Visible = false;
            if (payslipsContent != null) payslipsContent.Visible = false;
            headerTitle.Text = "Company Settings";

            if (companyContent == null)
            {
                companyContent = new CompanyPanel(CurrentUser) { Dock = DockStyle.Fill };
                contentHost.Controls.Add(companyContent);
            }
            companyContent.Visible = true;
            companyContent.BringToFront();
            companyContent.RefreshAll();
        }

        // Old btnLogout_Click, preserved.
        // ------------------------------------------------------------------
        // NOTIFICATIONS: the bell counts undertime notices that management still
        // has to follow up on (an employee timed out before completing 8 hours).
        // ------------------------------------------------------------------
        private void RefreshNotifications()
        {
            int open;
            try { open = AttendanceService.CountOpenUndertimeNotices(); }
            catch { return; }   // a failed check shouldn't interrupt the dashboard; the next one retries

            bell.Count = open;
            bell.Invalidate();

            if (open <= announcedNotices) { announcedNotices = open; return; }

            // New notice(s) since the last check. Pop up, but only while this window is the one
            // being used, so it never lands on top of a form someone is filling in.
            if (ActiveForm != this) return;
            announcedNotices = open;
            if (MessageBox.Show(this,
                    $"{open} undertime notice(s) need follow-up.\n\n" +
                    "An employee timed out before completing their duty. Open the notices now so you can talk to them?",
                    "Undertime Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                OpenNotifications();
        }

        private void OpenNotifications()
        {
            using var notices = new UndertimeNoticesForm(CurrentUser);
            notices.ShowDialog(this);
            RefreshNotifications();
        }

        private void Logout()
        {
            loggingOut = true;
            LoginForm login = new LoginForm();
            login.Show();
            Close();
        }

        // ------------------------------------------------------------------
        // DATA — real values from PayrollDB, "--" when unavailable
        // ------------------------------------------------------------------
        private void RefreshData()
        {
            profileChip.UserName = CurrentUser;
            profileChip.Invalidate();

            DashboardData d;
            try { d = DashboardData.Load(); }
            catch
            {
                d = new DashboardData();
                d.Unavailable.Add("SQL Server connection failed");
            }

            dateCard.Value = d.NextPayDate;     // "--" until a pay-date column exists
            dateCard.Sub = d.NextPayNote;

            netPayCard.Value = Theme.Money(d.TotalNetPay);
            netPayCard.Sub = d.TotalEmployees.ToString("N0") + " employees";

            grossPayCard.Value = Theme.Money(d.TotalGrossPay);
            grossPayCard.Sub = "incl. " + Theme.Money(d.TotalOvertimePay) + " overtime";

            payrollChart.SetData(d.PayByPosition, Theme.Money(d.TotalNetPay));
            summaryChart.SetData(d.Summary);

            employeeCountLabel.Text = d.TotalEmployees.ToString("N0") + " employees";
            employeeCountLabel.Location = new Point(employeeSection.Width - employeeCountLabel.Width - 26, 24);

            employeeGrid.DataSource = d.Employees;
            FormatGridColumns();
            employeeGrid.ClearSelection();

            if (d.Unavailable.Count > 0)
            {
                warningBanner.Text = "⚠ Some data could not load: " + string.Join("; ", d.Unavailable) +
                                      "  — check the database connection/password in AppConfig.cs";
                warningBanner.Visible = true;
            }
            else
            {
                warningBanner.Visible = false;
            }
        }

        private void FormatGridColumns()
        {
            employeeGrid.ReadOnly = true; // editing stays inside Form1
            foreach (DataGridViewColumn col in employeeGrid.Columns)
            {
                string n = col.Name.ToLowerInvariant();
                if (n.Contains("salary") || n.Contains("pay"))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    col.DefaultCellStyle.Format = Theme.Currency + "#,##0.00";
                    col.FillWeight = 85;
                }
                else if (n.Contains("name")) col.FillWeight = 140;
                else if (n.Contains("position")) col.FillWeight = 110;
            }

            // DataGridView applies AlternatingRowsDefaultCellStyle with HIGHER
            // priority than Column.DefaultCellStyle. That style (set in
            // GridStyle.Apply) has no alignment/format of its own, so every
            // other row was falling back to plain left-aligned text — that's
            // the "not straight in one line" staggering. Setting the style on
            // each individual cell wins over every other style level, so every
            // row lines up the same way regardless of odd/even banding.
            foreach (DataGridViewRow row in employeeGrid.Rows)
            {
                foreach (DataGridViewColumn col in employeeGrid.Columns)
                {
                    string n = col.Name.ToLowerInvariant();
                    if (n.Contains("salary") || n.Contains("pay"))
                    {
                        row.Cells[col.Index].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                        row.Cells[col.Index].Style.Format = Theme.Currency + "#,##0.00";
                    }
                }
            }
        }

        // Small circle with initials + user name (top-right, like the reference).
        // The header bell, with a red badge showing how many notices are waiting.
        private class NotificationBell : IconButton
        {
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public int Count { get; set; }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Count <= 0) return;

                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var badge = new Rectangle(Width - 19, 1, 18, 18);
                using (var fill = new SolidBrush(Color.FromArgb(239, 68, 68))) g.FillEllipse(fill, badge);
                using var font = new Font("Segoe UI Semibold", 7.5f);
                TextRenderer.DrawText(g, Count > 9 ? "9+" : Count.ToString(), font, badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private class ProfileChip : Control
        {
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public string UserName { get; set; } = "Admin";

            public ProfileChip()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Theme.Navy)) g.FillEllipse(b, 0, 2, 36, 36);
                TextRenderer.DrawText(g, Initials(UserName), Theme.SmallBold, new Rectangle(0, 2, 36, 36),
                    Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, UserName, Theme.BodyBold, new Rectangle(44, 2, Width - 44, 36),
                    Theme.TextDark, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            private static string Initials(string name)
            {
                var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "?";
                return (parts[0][0] + (parts.Length > 1 ? parts[parts.Length - 1][0].ToString() : "")).ToUpper();
            }
        }
    }
}
