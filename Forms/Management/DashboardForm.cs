using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
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
        private ComboBox payrollPeriodCombo = null!;
        private DashboardData? loadedDashboardData;
        private DataGridView employeeGrid = null!;
        private RoundedPanel employeeSection = null!;
        private Label employeeCountLabel = null!;
        private Label warningBanner = null!;
        private ProfileChip profileChip = null!;
        private NotificationBell bell = null!;
        private readonly System.Windows.Forms.Timer notificationTimer = new() { Interval = 30000 };
        private readonly System.Windows.Forms.Timer payrollReleaseTimer = new() { Interval = 300000 };
        private bool payrollReleaseRunning;
        private (int TicketLow, int TicketMedium, int TicketHigh, int Overtime, int Leave, int Undertime)? announcedNotifications;
        private SidebarButton[] navButtons = Array.Empty<SidebarButton>();
        private SidebarButton? leaveNavigationButton;

        private readonly Panel contentHost = new Panel();      // dashboard view OR embedded EmployeeManagementForm
        private readonly Panel dashboardContent = new Panel(); // cards + charts + employee table
        private Panel? ticketsContent;                         // built lazily on first visit
        private Panel? payslipsContent;                        // built lazily on first visit
        private CompanyPanel? companyContent;                  // built lazily on first visit
        private LeaveRequestsPanel? leaveRequestsContent;
        private TextBox ticketSearchBox = null!;
        private ComboBox ticketDepartmentFilter = null!;
        private FlowLayoutPanel pendingTicketCards = null!, resolvedTicketCards = null!;
        private Label pendingTicketCount = null!, resolvedTicketCount = null!;
        private List<TicketRow> allTickets = new();
        private bool loadingTicketFilters;
        private DataGridView payslipsGrid = null!;
        private Form? hostedEmployeeForm;                      // EmployeeManagementForm, embedded in the dashboard
        private bool loggingOut;
        private readonly int? signedInEmployeeId;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CurrentUser { get; set; }

        // Default "Admin" keeps LoginForm compiling without any change.
        public DashboardForm(string loggedInUser = "Admin", int? loggedInEmployeeId = null)
        {
            CurrentUser = loggedInUser;
            signedInEmployeeId = loggedInEmployeeId;
            Text = "Payroll Dashboard";                     // same title as before
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromPoint(Cursor.Position).Bounds;
            BackColor = Theme.Bg;
            Font = Theme.Body;

            BuildMain();
            BuildSidebar();

            Load += (s, e) => RefreshData(); // old form loaded stats on startup too

            // Pending management work: update the bell on launch and recheck every 30 seconds.
            Shown += (s, e) =>
            {
                RefreshNotifications();
                notificationTimer.Start();
                RunAutomaticPayrollRelease(showErrors: true);
                payrollReleaseTimer.Start();
            };
            notificationTimer.Tick += (s, e) => RefreshNotifications();
            payrollReleaseTimer.Tick += (s, e) => RunAutomaticPayrollRelease(showErrors: false);
            FormClosed += (s, e) =>
            {
                notificationTimer.Dispose();
                payrollReleaseTimer.Dispose();
            };

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
                ("leave", "Leave Requests", IconKind.Calendar),
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
                if (it.key == "leave") leaveNavigationButton = btn;
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
            addStaff.Click += (s, e) => Navigate("employee"); // Opens employee creation/management and selects its sidebar item

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

            // ----- content host: dashboard view OR the embedded employee management screen -----
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
            employeeGrid.CellFormatting += EmployeeGrid_CellFormatting;
            employeeGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || employeeGrid.Rows[e.RowIndex].DataBoundItem is not DataRowView row)
                    return;
                NavigateToEmployee(Convert.ToInt32(row["EmployeeID"]));
            };

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

            var trendPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0, 0, 10, 0) };
            var trendToolbar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Bg };
            var trendLabel = new Label
            {
                Text = "View", Location = new Point(4, 8), Size = new Size(42, 22),
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            payrollPeriodCombo = new ComboBox
            {
                Location = new Point(48, 4), Width = 150, Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Small
            };
            payrollPeriodCombo.Items.AddRange(new object[] { "By cutoff", "By month", "By year" });
            payrollPeriodCombo.SelectedIndex = 1; // Monthly is the most useful company-wide default.
            trendToolbar.Controls.Add(trendLabel);
            trendToolbar.Controls.Add(payrollPeriodCombo);

            payrollChart = new PaymentHistoryChart { Dock = DockStyle.Fill, Title = "Net Payroll by Month" };
            trendPanel.Controls.Add(payrollChart);
            trendPanel.Controls.Add(trendToolbar);
            payrollPeriodCombo.SelectedIndexChanged += (s, e) => UpdatePayrollTrend();

            summaryChart = new PayrollSummaryChart
            {
                Dock = DockStyle.Fill, Margin = new Padding(0), Title = "Latest Payroll by Department"
            };

            analytics.Controls.Add(trendPanel, 0, 0);
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
                Dock = DockStyle.Fill, Icon = IconKind.Users,
                Margin = new Padding(0, 0, 10, 0), ValueFont = new Font("Segoe UI Semibold", 14f)
            };
            dateCard.Title = "Employees";
            dateCard.Value = "0";
            dateCard.Sub = "Active employees";

            netPayCard = new DashboardCard
            {
                Dock = DockStyle.Fill, Icon = IconKind.Card,
                Margin = new Padding(0, 0, 10, 0), ValueFont = new Font("Segoe UI Semibold", 17f)
            };
            netPayCard.Title = "Latest Net Pay";

            grossPayCard = new DashboardCard
            {
                Dock = DockStyle.Fill, Icon = IconKind.Bank, ValueFont = new Font("Segoe UI Semibold", 16f)
            };
            grossPayCard.Title = "Latest Gross Pay";

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
            else if (key == "leave") ShowLeaveRequests();
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
            if (leaveRequestsContent != null) leaveRequestsContent.Visible = false;
            dashboardContent.Visible = true;
            headerTitle.Text = "Payroll Overview";
            RefreshData();
        }

        // Embed the employee management screen in the dashboard content area.
        private void ShowEmployee()
        {
            ShowEmployee(null);
        }

        private void ShowEmployee(int? employeeId)
        {
            if (hostedEmployeeForm != null)
            {
                if (employeeId.HasValue && hostedEmployeeForm is EmployeeManagementForm employeeForm)
                    employeeForm.FocusEmployee(employeeId.Value);
                return; // already open — keeps typed input unless a dashboard row was chosen
            }
            if (ticketsContent != null) ticketsContent.Visible = false;
            if (payslipsContent != null) payslipsContent.Visible = false;
            if (companyContent != null) companyContent.Visible = false;
            if (leaveRequestsContent != null) leaveRequestsContent.Visible = false;
            dashboardContent.Visible = false;
            headerTitle.Text = "Employee Management";
            hostedEmployeeForm = new EmployeeManagementForm(CurrentUser)
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill
            };
            contentHost.Controls.Add(hostedEmployeeForm);
            hostedEmployeeForm.Show();
            if (employeeId.HasValue && hostedEmployeeForm is EmployeeManagementForm openedEmployeeForm)
                openedEmployeeForm.FocusEmployee(employeeId.Value);
        }

        private void NavigateToEmployee(int employeeId)
        {
            foreach (var button in navButtons)
            {
                button.Active = (string?)button.Tag == "employee";
                button.Invalidate();
            }
            ShowEmployee(employeeId);
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
            if (leaveRequestsContent != null) leaveRequestsContent.Visible = false;

            if (ticketsContent == null)
            {
                ticketsContent = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(20, 18, 20, 18) };
                var filters = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Bg };
                ticketSearchBox = new TextBox
                {
                    Location = new Point(0, 7), Width = 300, Height = 30,
                    PlaceholderText = "Search subject, employee, or ticket..."
                };
                ticketDepartmentFilter = new ComboBox
                {
                    Location = new Point(316, 6), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList
                };
                ticketSearchBox.TextChanged += (s, e) => RefreshTicketBoard();
                ticketDepartmentFilter.SelectedIndexChanged += (s, e) =>
                {
                    if (!loadingTicketFilters) RefreshTicketBoard();
                };
                filters.Controls.Add(ticketSearchBox);
                filters.Controls.Add(ticketDepartmentFilter);

                var board = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                    BackColor = Theme.Bg, Padding = new Padding(0, 4, 0, 0)
                };
                board.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                board.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                var pendingLane = CreateTicketLane("PENDING", out pendingTicketCards, out pendingTicketCount);
                var resolvedLane = CreateTicketLane("RESOLVED", out resolvedTicketCards, out resolvedTicketCount);
                pendingLane.Margin = new Padding(0, 0, 8, 0);
                resolvedLane.Margin = new Padding(8, 0, 0, 0);
                board.Controls.Add(pendingLane, 0, 0);
                board.Controls.Add(resolvedLane, 1, 0);

                ticketsContent.Controls.Add(board);
                ticketsContent.Controls.Add(filters);
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
                allTickets = TicketService.LoadAll();
                loadingTicketFilters = true;
                string selectedDepartment = ticketDepartmentFilter.SelectedItem?.ToString() ?? "All departments";
                ticketDepartmentFilter.Items.Clear();
                ticketDepartmentFilter.Items.Add("All departments");
                foreach (string department in allTickets.Select(ticket => ticket.DepartmentName)
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name))
                    ticketDepartmentFilter.Items.Add(department);
                int selectedIndex = ticketDepartmentFilter.Items.IndexOf(selectedDepartment);
                ticketDepartmentFilter.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
                loadingTicketFilters = false;
                RefreshTicketBoard();
            }
            catch (Exception ex)
            {
                loadingTicketFilters = false;
                MessageBox.Show("Could not load tickets: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Panel CreateTicketLane(string title, out FlowLayoutPanel cards, out Label count)
        {
            var lane = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(10)
            };
            var header = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.White };
            var titleLabel = new Label
            {
                Text = title, Font = Theme.SmallBold, ForeColor = Theme.TextGray,
                Location = new Point(4, 8), AutoSize = true, BackColor = Color.White
            };
            var countLabel = new Label
            {
                Text = "0", Font = Theme.SmallBold, ForeColor = Theme.TextGray,
                AutoSize = true, BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            header.Controls.Add(titleLabel);
            header.Controls.Add(countLabel);
            header.Resize += (s, e) => countLabel.Location = new Point(header.Width - countLabel.Width - 8, 8);
            count = countLabel;
            cards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false,
                FlowDirection = FlowDirection.TopDown, BackColor = Theme.Bg,
                Padding = new Padding(5)
            };
            var cardList = cards;
            cardList.SizeChanged += (s, e) =>
            {
                foreach (Control card in cardList.Controls)
                    card.Width = Math.Max(250, cardList.ClientSize.Width - 28);
            };
            lane.Controls.Add(cardList);
            lane.Controls.Add(header);
            return lane;
        }

        private void RefreshTicketBoard()
        {
            if (pendingTicketCards == null || ticketDepartmentFilter == null) return;
            string query = ticketSearchBox.Text.Trim();
            string department = ticketDepartmentFilter.SelectedItem?.ToString() ?? "All departments";
            var filtered = allTickets.Where(ticket =>
                (department == "All departments" || ticket.DepartmentName.Equals(department, StringComparison.OrdinalIgnoreCase)) &&
                (query.Length == 0 || ticket.Subject.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 ticket.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 ticket.EmployeeName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 ticket.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 ticket.TicketId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(ticket => UrgencyRank(ticket.Urgency))
                .ThenByDescending(ticket => ticket.CreatedAt).ToList();
            var pending = filtered.Where(ticket => !IsTicketResolved(ticket)).ToList();
            var resolved = filtered.Where(IsTicketResolved).ToList();
            AddTicketCards(pendingTicketCards, pending, isResolved: false);
            AddTicketCards(resolvedTicketCards, resolved, isResolved: true);
            pendingTicketCount.Text = pending.Count.ToString();
            resolvedTicketCount.Text = resolved.Count.ToString();
        }

        private void AddTicketCards(FlowLayoutPanel cards, List<TicketRow> tickets, bool isResolved)
        {
            cards.SuspendLayout();
            cards.Controls.Clear();
            if (tickets.Count == 0)
            {
                cards.Controls.Add(new Label
                {
                    Text = isResolved ? "No resolved tickets." : "No pending tickets.",
                    AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextGray,
                    Margin = new Padding(8, 12, 8, 8), BackColor = Theme.Bg
                });
            }
            foreach (var ticket in tickets)
                cards.Controls.Add(CreateTicketCard(ticket, isResolved, Math.Max(250, cards.ClientSize.Width - 28)));
            cards.ResumeLayout();
        }

        private Control CreateTicketCard(TicketRow ticket, bool isResolved, int width)
        {
            var card = new Panel
            {
                Width = width, Height = 136, Margin = new Padding(3, 3, 3, 8),
                BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand
            };
            Color urgencyBack = ticket.Urgency.Equals("High", StringComparison.OrdinalIgnoreCase) ? Theme.RedBg
                : ticket.Urgency.Equals("Low", StringComparison.OrdinalIgnoreCase) ? Theme.GreenBg : Theme.AmberBg;
            Color urgencyFore = ticket.Urgency.Equals("High", StringComparison.OrdinalIgnoreCase) ? Theme.RedText
                : ticket.Urgency.Equals("Low", StringComparison.OrdinalIgnoreCase) ? Theme.GreenText : Theme.AmberText;
            var subject = new Label
            {
                Text = $"{ticket.Subject}  [{ticket.Category}]", Location = new Point(12, 10),
                Size = new Size(width - 112, 24), AutoEllipsis = true,
                Font = Theme.BodyBold, ForeColor = Theme.TextDark, BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            var urgency = new Label
            {
                Text = ticket.Urgency.ToUpperInvariant(), Location = new Point(width - 88, 8),
                Size = new Size(72, 23), TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.SmallBold, ForeColor = urgencyFore, BackColor = urgencyBack,
                Cursor = Cursors.Hand
            };
            var employee = new Label
            {
                Text = $"{ticket.EmployeeName}  ·  {ticket.DepartmentName}",
                Location = new Point(12, 39), Size = new Size(width - 26, 21),
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, AutoEllipsis = true,
                BackColor = Color.White, Cursor = Cursors.Hand
            };
            string message = ticket.Message.Replace("\r", " ").Replace("\n", " ").Trim();
            if (message.Length > 100) message = message.Substring(0, 97) + "...";
            var preview = new Label
            {
                Text = message, Location = new Point(12, 64), Size = new Size(width - 26, 40),
                Font = Theme.Small, ForeColor = Theme.TextDark, AutoEllipsis = true,
                BackColor = Color.White, Cursor = Cursors.Hand
            };
            var footer = new Label
            {
                Text = $"{ticket.CreatedAt:MMM d, yyyy}   ·   {ticket.Status}",
                Location = new Point(12, 109), Size = new Size(width - 26, 18),
                Font = Theme.Small, ForeColor = Theme.TextGray, BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            EventHandler open = (s, e) => OpenTicket(ticket, isResolved);
            card.Click += open;
            subject.Click += open;
            urgency.Click += open;
            employee.Click += open;
            preview.Click += open;
            footer.Click += open;
            card.Controls.Add(subject);
            card.Controls.Add(urgency);
            card.Controls.Add(employee);
            card.Controls.Add(preview);
            card.Controls.Add(footer);
            return card;
        }

        private static bool IsTicketResolved(TicketRow ticket)
            => ticket.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
               ticket.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase);

        private static int UrgencyRank(string urgency)
            => urgency.Equals("High", StringComparison.OrdinalIgnoreCase) ? 3
             : urgency.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

        private void OpenTicket(TicketRow ticket, bool isResolved)
        {
            using var form = new TicketResponseForm(ticket, readOnly: isResolved);
            if (!isResolved && form.ShowDialog(this) == DialogResult.OK)
            {
                TicketService.Respond(ticket.TicketId, form.Response);
                RefreshTickets();
                RefreshNotifications();
            }
            else if (isResolved)
            {
                form.ShowDialog(this);
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
            if (leaveRequestsContent != null) leaveRequestsContent.Visible = false;
            headerTitle.Text = "Payslips";

            if (payslipsContent == null)
            {
                payslipsContent = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(24) };

                var hint = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Bg };
                var hintLabel = new Label
                {
                    Text = "Management history shows payslips generated during the current company cutoff window. Older records remain stored for payroll reference. To generate a payslip, open the Employee screen and select an employee.",
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

        private async void RunAutomaticPayrollRelease(bool showErrors)
        {
            if (payrollReleaseRunning || IsDisposed) return;
            payrollReleaseRunning = true;
            try
            {
                var result = await Task.Run(() => PayrollScheduleService.ReleaseDuePayslips());
                if (result.PayslipsCreated > 0)
                {
                    RefreshData();
                    if (payslipsContent?.Visible == true) RefreshPayslips();
                    MessageBox.Show(this,
                        $"Automatically released {result.PayslipsCreated} payslip(s). They are now available in the Payslips history and employee accounts.",
                        "Payroll Release", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                if (showErrors && result.Errors.Count > 0)
                {
                    string details = string.Join(Environment.NewLine, result.Errors.Take(5));
                    if (result.Errors.Count > 5) details += Environment.NewLine + $"…and {result.Errors.Count - 5} more.";
                    MessageBox.Show(this, "Some automatic payslips could not be released. The system will retry while the app is running or next time it opens.\n\n" + details,
                        "Automatic Payroll Release", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                if (showErrors)
                    MessageBox.Show(this, "Automatic payroll release could not run: " + ex.Message,
                        "Automatic Payroll Release", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                payrollReleaseRunning = false;
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
            if (leaveRequestsContent != null) leaveRequestsContent.Visible = false;
            headerTitle.Text = "Company Settings";

            if (companyContent == null)
            {
                companyContent = new CompanyPanel(signedInEmployeeId) { Dock = DockStyle.Fill };
                contentHost.Controls.Add(companyContent);
            }
            companyContent.Visible = true;
            companyContent.BringToFront();
            companyContent.RefreshAll();
        }

        private void ShowLeaveRequests()
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
            if (companyContent != null) companyContent.Visible = false;
            headerTitle.Text = "Leave Requests";
            if (leaveRequestsContent == null)
            {
                leaveRequestsContent = new LeaveRequestsPanel(CurrentUser, RefreshNotifications) { Dock = DockStyle.Fill };
                contentHost.Controls.Add(leaveRequestsContent);
            }
            leaveRequestsContent.Visible = true;
            leaveRequestsContent.BringToFront();
            leaveRequestsContent.RefreshRequests();
        }

        // Old btnLogout_Click, preserved.
        // ------------------------------------------------------------------
        // NOTIFICATIONS: pending tickets, overtime approvals, leave requests,
        // and undertime follow-ups are combined in the header bell.
        // ------------------------------------------------------------------
        private void RefreshNotifications()
        {
            (int TicketLow, int TicketMedium, int TicketHigh, int Overtime, int Leave, int Undertime) current;
            try
            {
                var ticketCounts = TicketService.CountPendingByUrgency();
                current = (ticketCounts.Low, ticketCounts.Medium, ticketCounts.High,
                    AttendanceService.ListPendingOvertimeApprovals().Count,
                    LeaveService.CountPending(),
                    AttendanceService.CountOpenUndertimeNotices());
            }
            catch { return; }   // a failed check shouldn't interrupt the dashboard; the next one retries

            int ticketTotal = current.TicketLow + current.TicketMedium + current.TicketHigh;
            int total = ticketTotal + current.Overtime + current.Leave + current.Undertime;
            bell.Count = total;
            bell.Invalidate();
            if (leaveNavigationButton != null)
            {
                leaveNavigationButton.Text = current.Leave > 0 ? $"Leave Requests ({current.Leave})" : "Leave Requests";
                leaveNavigationButton.Invalidate();
            }

            var previous = announcedNotifications ?? (0, 0, 0, 0, 0, 0);
            bool hasNewItems = current.TicketLow > previous.Item1 || current.TicketMedium > previous.Item2 ||
                current.TicketHigh > previous.Item3 || current.Overtime > previous.Item4 ||
                current.Leave > previous.Item5 || current.Undertime > previous.Item6;
            if (!hasNewItems) { announcedNotifications = current; return; }

            // Announce new items only while this dashboard is active, so the prompt
            // never appears on top of a different screen the user is working in.
            if (ActiveForm != this) return;
            announcedNotifications = current;
            var newItems = new List<string>();
            if (current.TicketLow > previous.Item1 || current.TicketMedium > previous.Item2 || current.TicketHigh > previous.Item3)
                newItems.Add($"Tickets: {current.TicketHigh} High, {current.TicketMedium} Medium, {current.TicketLow} Low");
            if (current.Overtime > previous.Item4) newItems.Add($"{current.Overtime} overtime approval(s)");
            if (current.Leave > previous.Item5) newItems.Add($"{current.Leave} pending leave request(s)");
            if (current.Undertime > previous.Item6) newItems.Add($"{current.Undertime} undertime follow-up(s)");
            if (MessageBox.Show(this,
                    "New management items are waiting:\n\n" + string.Join("\n", newItems) +
                    "\n\nOpen notifications now?",
                    "Pending Notifications", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                OpenNotifications();
        }

        private void OpenNotifications()
        {
            try
            {
                var ticketCounts = TicketService.CountPendingByUrgency();
                var current = (TicketLow: ticketCounts.Low, TicketMedium: ticketCounts.Medium,
                    TicketHigh: ticketCounts.High, Overtime: AttendanceService.ListPendingOvertimeApprovals().Count,
                    Leave: LeaveService.CountPending(), Undertime: AttendanceService.CountOpenUndertimeNotices());
                using var notifications = new ManagementNotificationsForm(
                    current.TicketLow, current.TicketMedium, current.TicketHigh,
                    current.Overtime, current.Leave, current.Undertime);
                if (notifications.ShowDialog(this) == DialogResult.OK)
                {
                    switch (notifications.SelectedAction)
                    {
                        case "tickets":
                            Navigate("tickets");
                            break;
                        case "overtime":
                            using (var overtime = new PendingOvertimeForm()) overtime.ShowDialog(this);
                            break;
                        case "leave":
                            Navigate("leave");
                            break;
                        case "undertime":
                            using (var notices = new UndertimeNoticesForm(CurrentUser)) notices.ShowDialog(this);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load notifications: " + ex.Message,
                    "Notifications", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
                d.Unavailable.Add("database connection failed");
            }

            loadedDashboardData = d;
            dateCard.Value = d.TotalEmployees.ToString("N0");
            dateCard.Sub = "Active employees";

            netPayCard.Value = Theme.Money(d.TotalNetPay);
            netPayCard.Sub = "Across active employees";

            grossPayCard.Value = Theme.Money(d.TotalGrossPay);
            grossPayCard.Sub = "incl. " + Theme.Money(d.TotalOvertimePay) + " overtime";

            UpdatePayrollTrend();
            summaryChart.SetData(d.PayByDepartment);

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

        private void UpdatePayrollTrend()
        {
            if (payrollChart == null || loadedDashboardData == null) return;
            string view = payrollPeriodCombo?.SelectedItem?.ToString() ?? "By month";
            List<PayrollTrendPoint> points;
            if (view == "By cutoff")
            {
                points = loadedDashboardData.PayrollByCutoff;
                payrollChart.Title = "Net Payroll by Cutoff";
            }
            else if (view == "By year")
            {
                points = loadedDashboardData.PayrollByYear;
                payrollChart.Title = "Net Payroll by Year";
            }
            else
            {
                points = loadedDashboardData.PayrollByMonth;
                payrollChart.Title = "Net Payroll by Month";
            }

            var chartRows = points.Select(point => (point.Label, point.Total)).ToList();
            payrollChart.SetData(chartRows, Theme.Money(points.Sum(point => point.Total)));
        }

        private void FormatGridColumns()
        {
            employeeGrid.ReadOnly = true; // editing stays inside EmployeeManagementForm
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

        }

        private void EmployeeGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.ColumnIndex >= employeeGrid.Columns.Count)
                return;

            DataGridViewColumn column = employeeGrid.Columns[e.ColumnIndex];
            string name = (column.DataPropertyName + " " + column.Name).ToLowerInvariant();
            if (name.Contains("salary") || name.Contains("grosspay") || name.Contains("netpay"))
            {
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                e.CellStyle.Format = Theme.Currency + "#,##0.00";
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
