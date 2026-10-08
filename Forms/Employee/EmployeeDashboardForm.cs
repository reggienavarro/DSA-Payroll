using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MySqlConnector;
using PAYROLL.UI;

namespace PAYROLL
{
    // Shown to Employee-role accounts after login (Admin-role accounts still
    // go to DashboardForm). Built the same way as DashboardForm — code only,
    // no Designer.cs — so the two are easy to compare.
    public class EmployeeDashboardForm : Form
    {
        private readonly string username;
        private readonly int employeeId;

        private Label headerTitle = null!;
        private Panel contentHost = null!;
        private Label warningBanner = null!;
        private SidebarButton navPayroll = null!, navCalendar = null!, navTickets = null!, navPayslips = null!, navLeave = null!;
        private DataGridView myPayslipsGrid = null!;
        private List<Payslip> visiblePayslips = new();
        private Button openPayslipButton = null!;
        private readonly System.Windows.Forms.Timer notificationTimer;
        private Button notificationButton = null!;

        public EmployeeDashboardForm(string username, int employeeId)
        {
            this.username = username;
            this.employeeId = employeeId;

            Text = "Payroll System - My Payroll";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromPoint(Cursor.Position).Bounds;
            BackColor = Theme.Bg;
            Font = Theme.Body;

            var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            BuildMain(main);
            Controls.Add(main);          // fill first...
            Controls.Add(BuildSidebar()); // ...then left → sidebar takes the real left edge

            Navigate("payroll");
            notificationTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            notificationTimer.Tick += (s, e) => RefreshNotificationBadge();
            Shown += (s, e) => { RefreshNotificationBadge(); notificationTimer.Start(); };
            FormClosed += (s, e) => notificationTimer.Dispose();
        }

        // ------------------------------------------------------------------
        // SIDEBAR — much simpler than the admin one: only two destinations
        // ------------------------------------------------------------------
        private Panel BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 224, BackColor = Theme.Navy };

            var logoBar = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Navy };
            logoBar.Paint += (s, e) =>
            {
                using var path = Gfx.Round(new RectangleF(20, 20, 38, 38), 11);
                using var b = new SolidBrush(Theme.Accent);
                e.Graphics.FillPath(b, path);
                Icons.Draw(e.Graphics, IconKind.Building, new Rectangle(29, 29, 20, 20), Color.White, 1.7f);
            };
            var logoText = new Label
            {
                Text = "PAYROLL", Font = new Font("Segoe UI Semibold", 12f), ForeColor = Color.White,
                Location = new Point(70, 30), AutoSize = true, BackColor = Theme.Navy
            };
            logoBar.Controls.Add(logoText);

            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, BackColor = Theme.Navy, Padding = new Padding(0, 12, 0, 0)
            };
            navPayroll = new SidebarButton { Text = "My Payroll", Icon = IconKind.Chart, Width = 224, Height = 46, Margin = new Padding(0), Active = true };
            navPayslips = new SidebarButton { Text = "My Payslips", Icon = IconKind.Card, Width = 224, Height = 46, Margin = new Padding(0) };
            navCalendar = new SidebarButton { Text = "My Calendar", Icon = IconKind.Calendar, Width = 224, Height = 46, Margin = new Padding(0) };
            navLeave = new SidebarButton { Text = "My Leave", Icon = IconKind.Calendar, Width = 224, Height = 46, Margin = new Padding(0) };
            navTickets = new SidebarButton { Text = "My Tickets", Icon = IconKind.Bell, Width = 224, Height = 46, Margin = new Padding(0) };
            navPayroll.Click += (s, e) => Navigate("payroll");
            navPayslips.Click += (s, e) => Navigate("payslips");
            navCalendar.Click += (s, e) => Navigate("calendar");
            navLeave.Click += (s, e) => Navigate("leave");
            navTickets.Click += (s, e) => Navigate("tickets");
            nav.Controls.Add(navPayroll);
            nav.Controls.Add(navPayslips);
            nav.Controls.Add(navCalendar);
            nav.Controls.Add(navLeave);
            nav.Controls.Add(navTickets);

            var logoutBar = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Theme.Navy };
            var logoutBtn = new SidebarButton
            { Text = "Logout", Icon = IconKind.Logout, Width = 224, Height = 46, Location = new Point(0, 7) };
            logoutBtn.Click += (s, e) =>
            {
                var login = new LoginForm();
                login.Show();
                Close();
            };
            logoutBar.Controls.Add(logoutBtn);

            sidebar.Controls.Add(nav);
            sidebar.Controls.Add(logoutBar);
            sidebar.Controls.Add(logoBar);
            return sidebar;
        }

        // ------------------------------------------------------------------
        // HEADER + CONTENT AREA
        // ------------------------------------------------------------------
        private void BuildMain(Panel main)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.White };
            headerTitle = new Label
            {
                Text = "My Payroll", Font = Theme.H1, ForeColor = Theme.TextDark,
                Location = new Point(24, 20), AutoSize = true, BackColor = Color.White
            };
            var welcome = new Label
            {
                Text = "Signed in as " + username, Font = Theme.Small, ForeColor = Theme.TextGray,
                AutoSize = true, BackColor = Color.White
            };
            notificationButton = new Button
            {
                Text = "Notifications", Size = new Size(112, 34), FlatStyle = FlatStyle.Flat,
                BackColor = Color.White, ForeColor = Theme.TextDark, Font = Theme.SmallBold,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            notificationButton.FlatAppearance.BorderColor = Theme.CardBorder;
            notificationButton.Click += (s, e) => OpenNotifications();
            header.Resize += (s, e) =>
            {
                welcome.Location = new Point(header.Width - welcome.Width - 24, 24);
                notificationButton.Location = new Point(welcome.Left - notificationButton.Width - 16, 15);
            };
            header.Controls.Add(notificationButton);
            header.Controls.Add(headerTitle);
            header.Controls.Add(welcome);
            welcome.Location = new Point(header.Width - welcome.Width - 24, 24);
            notificationButton.Location = new Point(welcome.Left - notificationButton.Width - 16, 15);

            warningBanner = new Label
            {
                Dock = DockStyle.Top, Height = 32, Visible = false,
                BackColor = Theme.RedBg, ForeColor = Theme.RedText, Font = Theme.SmallBold,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0)
            };

            contentHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(24) };

            main.Controls.Add(contentHost);   // fill first...
            main.Controls.Add(warningBanner); // ...strip just below the header...
            main.Controls.Add(header);        // ...then top → header sits above everything
        }

        private void Navigate(string view)
        {
            navPayroll.Active = view == "payroll";
            navPayslips.Active = view == "payslips";
            navCalendar.Active = view == "calendar";
            navLeave.Active = view == "leave";
            navTickets.Active = view == "tickets";
            navPayroll.Invalidate();
            navPayslips.Invalidate();
            navCalendar.Invalidate();
            navLeave.Invalidate();
            navTickets.Invalidate();

            contentHost.Controls.Clear();
            warningBanner.Visible = false;

            if (view == "payroll")
            {
                headerTitle.Text = "My Payroll";
                ShowPayroll();
            }
            else if (view == "calendar")
            {
                headerTitle.Text = "My Calendar";
                ShowCalendar();
            }
            else if (view == "payslips")
            {
                headerTitle.Text = "My Payslips";
                ShowPayslips();
            }
            else if (view == "leave")
            {
                headerTitle.Text = "My Leave";
                ShowMyLeave();
            }
            else
            {
                headerTitle.Text = "My Tickets";
                ShowTickets();
            }
        }

        // ------------------------------------------------------------------
        // MY PAYROLL — own salary + itemized deduction breakdown
        // ------------------------------------------------------------------
        private void ShowPayroll()
        {
            string name = "", position = "";
            decimal basicSalary = 0, grossPay = 0, netPay = 0;
            bool found = false;

            try
            {
                using var con = new MySqlConnection(AppConfig.ConnectionString);
                using var cmd = new MySqlCommand(
                    "SELECT EmployeeName, Position, BasicSalary, GrossPay, NetPay FROM Employees WHERE EmployeeID=@id", con);
                cmd.Parameters.AddWithValue("@id", employeeId);
                con.Open();
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    found = true;
                    name = reader["EmployeeName"].ToString() ?? "";
                    position = reader["Position"].ToString() ?? "";
                    basicSalary = Convert.ToDecimal(reader["BasicSalary"]);
                    grossPay = Convert.ToDecimal(reader["GrossPay"]);
                    netPay = Convert.ToDecimal(reader["NetPay"]);
                }
            }
            catch (Exception ex)
            {
                warningBanner.Text = "⚠ Could not load your payroll: " + ex.Message;
                warningBanner.Visible = true;
                return;
            }

            if (!found)
            {
                warningBanner.Text = "⚠ No payroll record found for your account yet. Check with an admin.";
                warningBanner.Visible = true;
                return;
            }

            var latestPayslip = PayslipService.LoadForEmployee(employeeId).FirstOrDefault();
            if (latestPayslip != null)
            {
                basicSalary = latestPayslip.BasicPay;
                grossPay = latestPayslip.Total;
                netPay = latestPayslip.NetPay;
            }

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };

            var nameLabel = new Label
            {
                Text = $"{name}  ·  {position}", Font = Theme.H2, ForeColor = Theme.TextDark,
                AutoSize = true, Location = new Point(0, 0), BackColor = Theme.Bg
            };

            var cardsRow = new Panel { Location = new Point(0, 34), Size = new Size(900, 130), BackColor = Theme.Bg };
            var card1 = new DashboardCard { Location = new Point(0, 0), Size = new Size(280, 120), Icon = IconKind.Card, Title = "Basic Pay", Value = Theme.Money(basicSalary) };
            var card2 = new DashboardCard { Location = new Point(296, 0), Size = new Size(280, 120), Icon = IconKind.Arrows, Title = "Gross Pay", Value = Theme.Money(grossPay) };
            var card3 = new DashboardCard { Location = new Point(592, 0), Size = new Size(280, 120), Icon = IconKind.Bank, Title = "Net Pay", Value = Theme.Money(netPay) };
            cardsRow.Controls.Add(card1);
            cardsRow.Controls.Add(card2);
            cardsRow.Controls.Add(card3);

            bool hasPayslip = latestPayslip != null;
            decimal sss = latestPayslip?.SssContribution ?? 0m;
            decimal philHealth = latestPayslip?.PhilHealthContribution ?? 0m;
            decimal pagIbig = latestPayslip?.HmdfContribution ?? 0m;
            decimal overbreak = latestPayslip?.LateUtOb ?? 0m;
            decimal totalDeductions = latestPayslip?.TotalDeductions ?? 0m;

            var deductionsSection = new RoundedPanel { Location = new Point(0, 180), Width = 592 };
            var dTitle = new Label
            {
                Text = hasPayslip ? "Deductions on latest payslip" : "No payslip deductions yet",
                Font = Theme.H2, ForeColor = Theme.TextDark, AutoSize = true,
                Location = new Point(20, 16), BackColor = Color.White
            };
            deductionsSection.Controls.Add(dTitle);

            var rows = new List<(string label, decimal value, string note)>
            {
                ("Overbreak", overbreak, hasPayslip ? "Excess break time above 60 minutes" : "No payslip generated yet"),
                ("SSS", sss, hasPayslip ? "Employee share for this cutoff" : "No payslip generated yet"),
                ("PhilHealth", philHealth, hasPayslip ? "Employee share for this cutoff" : "No payslip generated yet"),
                ("Pag-IBIG", pagIbig, hasPayslip ? "Employee share for this cutoff" : "No payslip generated yet"),
            };
            if (latestPayslip?.Loans > 0)
                rows.Add(("Legacy Loan", latestPayslip.Loans, "Saved on an earlier payslip"));
            int ry = 56;
            foreach (var row in rows)
            {
                var lbl = new Label { Text = row.label, Font = Theme.BodyBold, ForeColor = Theme.TextDark, AutoSize = true, Location = new Point(20, ry), BackColor = Color.White };
                var note = new Label { Text = row.note, Font = Theme.Small, ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(20, ry + 18), BackColor = Color.White };
                var val = new Label { Text = Theme.Money(row.value), Font = Theme.BodyBold, ForeColor = Theme.TextDark, AutoSize = true, BackColor = Color.White };
                val.Location = new Point(552 - val.Width, ry + 4);
                deductionsSection.Controls.Add(lbl);
                deductionsSection.Controls.Add(note);
                deductionsSection.Controls.Add(val);
                ry += 44;
            }
            var totalLbl = new Label { Text = "Total Deductions", Font = Theme.BodyBold, ForeColor = Theme.TextDark, AutoSize = true, Location = new Point(20, ry + 6), BackColor = Color.White };
            var totalVal = new Label { Text = Theme.Money(totalDeductions), Font = Theme.ValueBig, ForeColor = Theme.Accent, AutoSize = true, BackColor = Color.White };
            totalVal.Location = new Point(552 - totalVal.Width, ry);
            deductionsSection.Controls.Add(totalLbl);
            deductionsSection.Controls.Add(totalVal);
            deductionsSection.Height = ry + 60;

            var disputeBtn = new ModernButton
            {
                Text = "Create a Ticket", Location = new Point(0, deductionsSection.Bottom + 20),
                Size = new Size(280, 42), SurroundColor = Theme.Bg
            };

            var viewPayslipsBtn = new ModernButton
            {
                Text = "View / Print My Payslips",
                Location = new Point(300, deductionsSection.Bottom + 20),
                Size = new Size(280, 42),
                SurroundColor = Theme.Bg
            };
            viewPayslipsBtn.Click += (s, e) => Navigate("payslips");
            disputeBtn.Click += (s, e) =>
            {
                using var form = new TicketForm();
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    TicketService.Create(employeeId, form.Subject, form.Message, form.Urgency, form.Category);
                    MessageBox.Show("Your ticket has been submitted. Management will respond in My Tickets.",
                        "Ticket Submitted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            panel.Controls.Add(nameLabel);
            panel.Controls.Add(cardsRow);
            panel.Controls.Add(deductionsSection);
            panel.Controls.Add(disputeBtn);
            panel.Controls.Add(viewPayslipsBtn);
            contentHost.Controls.Add(panel);
        }

        private void ShowPayslips()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var title = new Label
            {
                Text = "My payslip history",
                Font = Theme.H2,
                ForeColor = Theme.TextDark,
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Theme.Bg
            };
            var hint = new Label
            {
                Text = "Select a payslip and open it to print or save it as a PDF.",
                Font = Theme.Small,
                ForeColor = Theme.TextGray,
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Theme.Bg
            };
            myPayslipsGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false };
            GridStyle.Apply(myPayslipsGrid);
            myPayslipsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cutoff", HeaderText = "Cutoff period", FillWeight = 110 });
            myPayslipsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Generated", HeaderText = "Generated", FillWeight = 90 });
            myPayslipsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NetPay", HeaderText = "Net pay", FillWeight = 80 });
            myPayslipsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Receivable", HeaderText = "Total receivable", FillWeight = 90 });
            myPayslipsGrid.CellDoubleClick += (s, e) => OpenSelectedPayslip();
            myPayslipsGrid.SelectionChanged += (s, e) =>
            {
                if (openPayslipButton != null)
                    openPayslipButton.Enabled = myPayslipsGrid.CurrentRow?.DataBoundItem is DataRowView;
            };

            var table = new DataTable();
            table.Columns.Add("PayslipID", typeof(int));
            table.Columns.Add("Cutoff");
            table.Columns.Add("Generated");
            table.Columns.Add("NetPay");
            table.Columns.Add("Receivable");
            try
            {
                visiblePayslips = PayslipService.LoadForEmployee(employeeId);
                foreach (var payslip in visiblePayslips)
                    table.Rows.Add(payslip.PayslipId,
                        $"{payslip.CutoffStart:MMM d} - {payslip.CutoffEnd:MMM d, yyyy}",
                        payslip.CreatedAt.ToString("MMM d, yyyy h:mm tt"),
                        Theme.Money(payslip.NetPay),
                        Theme.Money(payslip.TotalAmountReceivable));
                myPayslipsGrid.DataSource = table;
                if (visiblePayslips.Count == 0)
                    hint.Text = "No payslips are available for your account yet. Payslips remain in your history while your employee account is active.";
            }
            catch (Exception ex)
            {
                warningBanner.Text = "Could not load your payslips: " + ex.Message;
                warningBanner.Visible = true;
            }

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Theme.Bg, Padding = new Padding(0, 8, 0, 0) };
            openPayslipButton = new Button
            {
                Text = "Open / Print / Save PDF",
                Size = new Size(220, 38),
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                Font = Theme.SmallBold
            };
            openPayslipButton.FlatAppearance.BorderSize = 0;
            openPayslipButton.Click += (s, e) => OpenSelectedPayslip();
            openPayslipButton.Enabled = myPayslipsGrid.CurrentRow?.DataBoundItem is DataRowView;
            footer.Controls.Add(openPayslipButton);

            panel.Controls.Add(myPayslipsGrid);
            panel.Controls.Add(footer);
            panel.Controls.Add(hint);
            panel.Controls.Add(title);
            contentHost.Controls.Add(panel);
        }

        private void OpenSelectedPayslip()
        {
            if (myPayslipsGrid.CurrentRow?.DataBoundItem is not DataRowView row) return;
            int id = Convert.ToInt32(row["PayslipID"]);
            var payslip = visiblePayslips.Find(item => item.PayslipId == id && item.EmployeeId == employeeId);
            if (payslip == null) return;
            using var preview = new PayslipPreviewForm(payslip);
            preview.ShowDialog(this);
        }

        private void ShowMyLeave()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Bg };
            var title = new Label { Text = "My Leave Requests", Font = Theme.H2, ForeColor = Theme.TextDark,
                AutoSize = true, Location = new Point(0, 0), BackColor = Theme.Bg };
            var subtitle = new Label { Text = "Submit a request and follow its review status here.", Font = Theme.Small,
                ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(0, 36), BackColor = Theme.Bg };
            try
            {
                var balance = LeaveService.GetCreditBalance(employeeId, DateTime.Today.Year);
                subtitle.Text = $"{DateTime.Today.Year} paid leave credits: {balance.AvailableDays:0.##} available of {balance.EntitledDays:0.##} days. Submit a request and follow its review status here.";
            }
            catch { /* Keep leave requests usable if the balance lookup is unavailable. */ }
            var requestButton = new ModernButton { Text = "Request Leave", Size = new Size(160, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(0, 4), SurroundColor = Theme.Bg };
            header.Resize += (s, e) => requestButton.Left = header.ClientSize.Width - requestButton.Width;
            requestButton.Click += (s, e) =>
            {
                using var form = new LeaveRequestForm(employeeId);
                if (form.ShowDialog(this) == DialogResult.OK) ShowMyLeave();
            };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(requestButton);

            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
            GridStyle.Apply(grid);
            try
            {
                var requests = LeaveService.ListForRange(new DateTime(2000, 1, 1), new DateTime(2100, 12, 31), employeeId)
                    .OrderByDescending(request => request.StartDate).ToList();
                var table = new DataTable();
                table.Columns.Add("RequestID", typeof(int));
                table.Columns.Add("Start Date");
                table.Columns.Add("End Date");
                table.Columns.Add("Reason");
                table.Columns.Add("Status");
                table.Columns.Add("Paid", typeof(bool));
                foreach (var request in requests)
                    table.Rows.Add(request.LeaveRequestId, request.StartDate.ToString("MMM d, yyyy"),
                        request.EndDate.ToString("MMM d, yyyy"), request.Reason, request.Status, request.Paid);
                grid.DataSource = table;
                if (grid.Columns["RequestID"] is DataGridViewColumn idColumn) idColumn.Visible = false;
                if (grid.Columns["Reason"] is DataGridViewColumn reasonColumn)
                    reasonColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                if (requests.Count == 0) subtitle.Text = "You have no leave requests yet. Use Request Leave to submit one.";
            }
            catch (Exception ex)
            {
                warningBanner.Text = "Could not load your leave requests: " + ex.Message;
                warningBanner.Visible = true;
            }

            panel.Controls.Add(grid);
            panel.Controls.Add(header);
            contentHost.Controls.Add(panel);
        }

        private void ShowCalendar()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var calendar = new MonthCalendar
            {
                Location = new Point(0, 0),
                MaxSelectionCount = 1,
                CalendarDimensions = new Size(1, 1),
                FirstDayOfWeek = Day.Sunday,
                BackColor = Color.White
            };
            var timeInLabel = new Label
            {
                Text = "TIME IN", Location = new Point(250, 8), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            var timeInValue = new DateTimePicker
            {
                Location = new Point(250, 30), Width = 120, Format = DateTimePickerFormat.Time,
                ShowUpDown = true, Value = DateTime.Now
            };
            var timeIn = new ModernButton
            {
                Text = "Record", Location = new Point(380, 27), Size = new Size(100, 36), SurroundColor = Theme.Bg
            };
            var breakOutLabel = new Label
            {
                Text = "BREAK OUT", Location = new Point(500, 8), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            var breakOutValue = new DateTimePicker
            {
                Location = new Point(500, 30), Width = 120, Format = DateTimePickerFormat.Time,
                ShowUpDown = true, Value = DateTime.Now
            };
            var breakOut = new ModernButton
            {
                Text = "Record", Location = new Point(630, 27), Size = new Size(100, 36), SurroundColor = Theme.Bg
            };
            var breakInLabel = new Label
            {
                Text = "BREAK IN", Location = new Point(250, 76), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            var breakInValue = new DateTimePicker
            {
                Location = new Point(250, 98), Width = 120, Format = DateTimePickerFormat.Time,
                ShowUpDown = true, Value = DateTime.Now
            };
            var breakIn = new ModernButton
            {
                Text = "Record", Location = new Point(380, 95), Size = new Size(100, 36), SurroundColor = Theme.Bg
            };
            var timeOutLabel = new Label
            {
                Text = "TIME OUT", Location = new Point(500, 76), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            var timeOutValue = new DateTimePicker
            {
                Location = new Point(500, 98), Width = 120, Format = DateTimePickerFormat.Time,
                ShowUpDown = true, Value = DateTime.Now
            };
            var timeOut = new ModernButton
            {
                Text = "Record", Location = new Point(630, 95), Size = new Size(100, 36), SurroundColor = Theme.Bg
            };
            var reportAbsence = new ModernButton
            {
                Text = "Report Absence", Location = new Point(250, 150), Size = new Size(160, 36), SurroundColor = Theme.Bg
            };
            var recordsGrid = new DataGridView
            {
                Location = new Point(0, 210), Size = new Size(850, 360),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                ReadOnly = true
            };
            panel.Resize += (s, e) => recordsGrid.Size = new Size(panel.ClientSize.Width, Math.Max(120, panel.ClientSize.Height - 220));
            GridStyle.Apply(recordsGrid);
            recordsGrid.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || recordsGrid.Columns[e.ColumnIndex].Name != "Undertime") return;
                if (recordsGrid.Rows[e.RowIndex].Cells["Type"].Value?.ToString() != "Attendance") return;
                string text = recordsGrid.Rows[e.RowIndex].Cells["Undertime"].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(text)) return;
                try
                {
                    DateTime date = DateTime.Parse(recordsGrid.Rows[e.RowIndex].Cells["Date"].Value?.ToString() ?? "");
                    var record = AttendanceService.ListForDate(date, employeeId).Find(item => item.EmployeeId == employeeId);
                    if (record != null)
                        using (var receipt = new UndertimeReceiptForm(record)) receipt.ShowDialog(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open the undertime record: " + ex.Message, "Attendance",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            recordsGrid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && recordsGrid.Columns[e.ColumnIndex].Name == "Undertime" &&
                    !string.IsNullOrWhiteSpace(e.Value?.ToString()))
                {
                    e.CellStyle.ForeColor = Theme.Accent;
                    recordsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = "Click to view the undertime follow-up record";
                }
            };

            void RefreshCalendar()
            {
                DateTime selected = calendar.SelectionStart.Date;
                DateTime monthStart = new DateTime(selected.Year, selected.Month, 1);
                DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);
                List<Holiday> holidays;
                List<AttendanceRecord> attendance;
                List<LeaveRequestRecord> leaveRequests;
                try
                {
                    holidays = CompanyService.ListHolidays(selected.Year);
                    attendance = AttendanceService.ListForRange(monthStart, monthEnd, employeeId);
                    leaveRequests = LeaveService.ListForRange(monthStart, monthEnd, employeeId);
                }
                catch (Exception ex)
                {
                    warningBanner.Text = "Could not load your calendar: " + ex.Message;
                    warningBanner.Visible = true;
                    return;
                }
                var records = new DataTable();
                records.Columns.Add("Date");
                records.Columns.Add("Event");
                records.Columns.Add("Type");
                records.Columns.Add("Time In");
                records.Columns.Add("Break Out");
                records.Columns.Add("Break In");
                records.Columns.Add("Time Out");
                records.Columns.Add("Undertime");
                calendar.RemoveAllBoldedDates();

                foreach (var holiday in holidays)
                {
                    if (holiday.HolidayDate < monthStart || holiday.HolidayDate > monthEnd) continue;
                    calendar.AddBoldedDate(holiday.HolidayDate.Date);
                }
                foreach (var holiday in holidays.Where(item => item.HolidayDate == selected))
                    records.Rows.Add(selected.ToString("MMM d, yyyy"), holiday.HolidayName,
                        holiday.HolidayType + " Holiday", "", "", "", "");
                foreach (var record in attendance)
                {
                    if (record.AttendanceDate.Date != selected) continue;
                    records.Rows.Add(record.AttendanceDate.ToString("MMM d, yyyy"), record.Status,
                        "Attendance", record.TimeIn?.ToString(@"hh\:mm") ?? "",
                        record.BreakOut?.ToString(@"hh\:mm") ?? "",
                        record.BreakIn?.ToString(@"hh\:mm") ?? "",
                        record.TimeOut?.ToString(@"hh\:mm") ?? "",
                        UndertimeText(record));
                }
                foreach (var leave in leaveRequests)
                {
                    for (DateTime date = leave.StartDate.Date; date <= leave.EndDate.Date; date = date.AddDays(1))
                        if (date >= monthStart && date <= monthEnd) calendar.AddBoldedDate(date);

                    if (selected >= leave.StartDate.Date && selected <= leave.EndDate.Date)
                    {
                        string status = leave.Status == "Approved" && leave.Paid
                            ? "Approved - Paid"
                            : leave.Status;
                        records.Rows.Add(
                            $"{leave.StartDate:MMM d} - {leave.EndDate:MMM d, yyyy}",
                            leave.Reason,
                            "Leave: " + status,
                            "", "", "", "");
                    }
                }
                foreach (var record in attendance)
                    calendar.AddBoldedDate(record.AttendanceDate.Date);
                calendar.UpdateBoldedDates();
                recordsGrid.DataSource = records;
                timeIn.Enabled = selected == DateTime.Today;
                breakOut.Enabled = selected == DateTime.Today;
                breakIn.Enabled = selected == DateTime.Today;
                timeOut.Enabled = selected == DateTime.Today;
                reportAbsence.Enabled = selected <= DateTime.Today;
            }

            calendar.DateChanged += (s, e) => RefreshCalendar();
            timeIn.Click += (s, e) =>
            {
                try { AttendanceService.RecordTimeIn(employeeId, timeInValue.Value.TimeOfDay); RefreshCalendar(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Attendance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            breakOut.Click += (s, e) =>
            {
                try { AttendanceService.RecordBreakOut(employeeId, breakOutValue.Value.TimeOfDay); RefreshCalendar(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Attendance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            breakIn.Click += (s, e) =>
            {
                try
                {
                    var returnTime = breakInValue.Value.TimeOfDay;

                    // An early break-in is allowed, but the employee is warned first.
                    int unfinished = AttendanceService.GetUnfinishedBreakMinutes(employeeId, returnTime);
                    if (unfinished > 0 && MessageBox.Show(
                            $"Your one-hour break isn't finished yet ({unfinished} minute(s) left).\n\n" +
                            "You can still break in, but the unfinished part of your break won't be counted on " +
                            "your payslip. The full hour is still taken as your break, so working through it earns no extra pay.\n\n" +
                            "Break in now?",
                            "Break not finished", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;

                    AttendanceService.RecordBreakIn(employeeId, returnTime);
                    RefreshCalendar();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Attendance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            timeOut.Click += (s, e) =>
            {
                try
                {
                    var leaveTime = timeOutValue.Value.TimeOfDay;

                    // Timing out early is allowed, but the employee is warned first, and
                    // management gets a notice to follow up on.
                    int shortBy = AttendanceService.GetUndertimeMinutes(employeeId, leaveTime);
                    if (shortBy > 0 && MessageBox.Show(
                            $"You haven't completed your {AttendanceService.RegularDayMinutes / 60}-hour duty. " +
                            $"You are {AttendanceService.FormatDuration(shortBy)} short.\n\n" +
                            "That time will be deducted from your salary. Management will also be notified " +
                            "of this undertime and may talk to you about the reason.\n\n" +
                            "Time out anyway?",
                            "Undertime", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;

                    AttendanceService.RecordTimeOut(employeeId, leaveTime);
                    RefreshCalendar();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Attendance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            reportAbsence.Click += (s, e) =>
            {
                try
                {
                    AttendanceService.ReportAbsence(employeeId, calendar.SelectionStart);
                    RefreshCalendar();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Attendance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            panel.Controls.Add(calendar);
            panel.Controls.Add(timeInLabel);
            panel.Controls.Add(timeInValue);
            panel.Controls.Add(timeIn);
            panel.Controls.Add(breakOutLabel);
            panel.Controls.Add(breakOutValue);
            panel.Controls.Add(breakOut);
            panel.Controls.Add(breakInLabel);
            panel.Controls.Add(breakInValue);
            panel.Controls.Add(breakIn);
            panel.Controls.Add(timeOutLabel);
            panel.Controls.Add(timeOutValue);
            panel.Controls.Add(timeOut);
            panel.Controls.Add(reportAbsence);
            panel.Controls.Add(recordsGrid);
            contentHost.Controls.Add(panel);
            RefreshCalendar();
        }

        // ------------------------------------------------------------------
        // MY TICKETS — this employee's own filed disputes + any response
        // ------------------------------------------------------------------
        // What the employee sees in their own calendar: how short they were, and whether
        // management has followed up yet.
        private static string UndertimeText(AttendanceRecord record)
        {
            int minutes = AttendanceService.GetUndertimeMinutes(record);
            if (minutes == 0) return "";
            string state = record.UndertimeStatus == "Open" ? " (management notified)"
                         : record.UndertimeStatus == "Resolved" ? " (discussed with management)" : "";
            return AttendanceService.FormatDuration(minutes) + state;
        }

        private void ShowTickets()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Bg
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            var title = new Label
            {
                Text = "My Tickets",
                Font = Theme.H2,
                ForeColor = Theme.TextDark,
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
            GridStyle.Apply(grid);
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenSelectedTicket(grid); };

            var footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(0, 8, 0, 0) };
            var detailsButton = new ModernButton
            {
                Text = "View full ticket and response",
                Size = new Size(270, 38),
                Location = new Point(0, 8),
                SurroundColor = Theme.Bg
            };
            detailsButton.Click += (s, e) => OpenSelectedTicket(grid);
            footer.Controls.Add(detailsButton);

            try
            {
                var tickets = TicketService.LoadForEmployee(employeeId);
                var table = new DataTable();
                table.Columns.Add("TicketID", typeof(int));
                table.Columns.Add("Subject");
                table.Columns.Add("Category");
                table.Columns.Add("Urgency");
                table.Columns.Add("Status");
                table.Columns.Add("Filed On");
                table.Columns.Add("Management Response");
                foreach (var ticket in tickets)
                {
                    string? adminResponse = ticket.AdminResponse;
                    string response = string.IsNullOrWhiteSpace(adminResponse)
                        ? "Awaiting response"
                        : adminResponse.Replace("\r", " ").Replace("\n", " ");
                    if (response.Length > 100) response = response[..100] + "...";
                    table.Rows.Add(ticket.TicketId, ticket.Subject, ticket.Category, ticket.Urgency, ticket.Status,
                        ticket.CreatedAt.ToString("MMM d, yyyy"), response);
                }
                grid.DataSource = table;
                var ticketIdColumn = grid.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(column => column.DataPropertyName == "TicketID");
                if (ticketIdColumn != null) ticketIdColumn.Visible = false;
                var responseColumn = grid.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(column => column.DataPropertyName == "Management Response");
                if (responseColumn != null) responseColumn.FillWeight = 150;
                grid.Tag = tickets;
                if (tickets.Count == 0)
                    title.Text = "My Tickets - no tickets yet";
            }
            catch (Exception ex)
            {
                warningBanner.Text = "Could not load your tickets: " + ex.Message;
                warningBanner.Visible = true;
            }

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(grid, 0, 1);
            layout.Controls.Add(footer, 0, 2);
            panel.Controls.Add(layout);
            contentHost.Controls.Add(panel);
        }

        private void RefreshNotificationBadge()
        {
            try
            {
                int unread = EmployeeNotificationService.CountUnread(employeeId);
                notificationButton.Text = unread > 0 ? $"Notifications ({unread})" : "Notifications";
            }
            catch { notificationButton.Text = "Notifications"; }
        }

        private void OpenNotifications()
        {
            try
            {
                var notices = EmployeeNotificationService.List(employeeId);
                using var form = new EmployeeNotificationsForm(notices);
                form.ShowDialog(this);
                EmployeeNotificationService.MarkAllRead(employeeId);
                RefreshNotificationBadge();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load notifications: " + ex.Message, "Notifications",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenSelectedTicket(DataGridView grid)
        {
            if (grid.CurrentRow?.DataBoundItem is not DataRowView row || grid.Tag is not List<TicketRow> tickets)
                return;
            int ticketId = Convert.ToInt32(row["TicketID"]);
            var ticket = tickets.Find(item => item.TicketId == ticketId && item.EmployeeId == employeeId);
            if (ticket == null) return;
            using var details = new TicketDetailForm(ticket);
            details.ShowDialog(this);
        }

        private void ShowTicketsLegacy()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var title = new Label { Text = "My Salary Dispute Tickets", Font = Theme.H2, ForeColor = Theme.TextDark, AutoSize = true, Location = new Point(0, 0), BackColor = Theme.Bg };

            var grid = new DataGridView
            {
                Location = new Point(0, 40),
                Size = new Size(panel.Width, panel.Height - 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                ReadOnly = true
            };
            GridStyle.Apply(grid);

            try
            {
                var tickets = TicketService.LoadForEmployee(employeeId);
                var table = new DataTable();
                table.Columns.Add("Subject");
                table.Columns.Add("Status");
                table.Columns.Add("Filed On");
                table.Columns.Add("Management Response");
                foreach (var t in tickets)
                    table.Rows.Add(t.Subject, t.Status, t.CreatedAt.ToString("MMM d, yyyy"), t.AdminResponse ?? "— awaiting response —");
                grid.DataSource = table;
            }
            catch (Exception ex)
            {
                warningBanner.Text = "⚠ Could not load your tickets: " + ex.Message;
                warningBanner.Visible = true;
            }

            panel.Controls.Add(title);
            panel.Controls.Add(grid);
            contentHost.Controls.Add(panel);
        }
    }
}
