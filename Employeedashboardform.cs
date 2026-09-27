using System;
using System.Data;
using System.Drawing;
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
        private SidebarButton navPayroll = null!, navTickets = null!;

        public EmployeeDashboardForm(string username, int employeeId)
        {
            this.username = username;
            this.employeeId = employeeId;

            Text = "Payroll System - My Payroll";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 650);
            Size = new Size(1150, 720);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            BuildMain(main);
            Controls.Add(main);          // fill first...
            Controls.Add(BuildSidebar()); // ...then left → sidebar takes the real left edge

            Navigate("payroll");
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
            navTickets = new SidebarButton { Text = "My Tickets", Icon = IconKind.Bell, Width = 224, Height = 46, Margin = new Padding(0) };
            navPayroll.Click += (s, e) => Navigate("payroll");
            navTickets.Click += (s, e) => Navigate("tickets");
            nav.Controls.Add(navPayroll);
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
            header.Resize += (s, e) => welcome.Location = new Point(header.Width - welcome.Width - 24, 24);
            header.Controls.Add(headerTitle);
            header.Controls.Add(welcome);

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
            navTickets.Active = view == "tickets";
            navPayroll.Invalidate();
            navTickets.Invalidate();

            contentHost.Controls.Clear();
            warningBanner.Visible = false;

            if (view == "payroll")
            {
                headerTitle.Text = "My Payroll";
                ShowPayroll();
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

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };

            var nameLabel = new Label
            {
                Text = $"{name}  ·  {position}", Font = Theme.H2, ForeColor = Theme.TextDark,
                AutoSize = true, Location = new Point(0, 0), BackColor = Theme.Bg
            };

            var cardsRow = new Panel { Location = new Point(0, 34), Size = new Size(900, 130), BackColor = Theme.Bg };
            var card1 = new DashboardCard { Location = new Point(0, 0), Size = new Size(280, 120), Icon = IconKind.Card, Title = "Basic Salary", Value = Theme.Money(basicSalary) };
            var card2 = new DashboardCard { Location = new Point(296, 0), Size = new Size(280, 120), Icon = IconKind.Arrows, Title = "Gross Pay", Value = Theme.Money(grossPay) };
            var card3 = new DashboardCard { Location = new Point(592, 0), Size = new Size(280, 120), Icon = IconKind.Bank, Title = "Net Pay", Value = Theme.Money(netPay) };
            cardsRow.Controls.Add(card1);
            cardsRow.Controls.Add(card2);
            cardsRow.Controls.Add(card3);

            // Recomputed live from basic salary — always matches what Form1
            // actually deducted, since both call the same calculator.
            var d = DeductionsCalculator.Compute(basicSalary);

            var deductionsSection = new RoundedPanel { Location = new Point(0, 180), Width = 592 };
            var dTitle = new Label { Text = "Where your deductions come from", Font = Theme.H2, ForeColor = Theme.TextDark, AutoSize = true, Location = new Point(20, 16), BackColor = Color.White };
            deductionsSection.Controls.Add(dTitle);

            var rows = new (string label, decimal value, string note)[]
            {
                ("SSS", d.sss, "5% employee share — 2025 rate"),
                ("PhilHealth", d.philHealth, "2.5% employee share — 2025 rate"),
                ("Pag-IBIG", d.pagIbig, "1–2% employee share, capped at ₱200"),
                ("Withholding Tax", d.tax, "BIR TRAIN law monthly bracket"),
            };
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
            var totalVal = new Label { Text = Theme.Money(d.total), Font = Theme.ValueBig, ForeColor = Theme.Accent, AutoSize = true, BackColor = Color.White };
            totalVal.Location = new Point(552 - totalVal.Width, ry);
            deductionsSection.Controls.Add(totalLbl);
            deductionsSection.Controls.Add(totalVal);
            deductionsSection.Height = ry + 60;

            var disputeBtn = new ModernButton
            {
                Text = "File a Salary Dispute", Location = new Point(0, deductionsSection.Bottom + 20),
                Size = new Size(280, 42), SurroundColor = Theme.Bg
            };
            disputeBtn.Click += (s, e) =>
            {
                using var form = new TicketForm();
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    TicketService.Create(employeeId, form.Subject, form.Message);
                    MessageBox.Show("Your dispute has been submitted. Management will respond in My Tickets.",
                        "Ticket Submitted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            panel.Controls.Add(nameLabel);
            panel.Controls.Add(cardsRow);
            panel.Controls.Add(deductionsSection);
            panel.Controls.Add(disputeBtn);
            contentHost.Controls.Add(panel);
        }

        // ------------------------------------------------------------------
        // MY TICKETS — this employee's own filed disputes + any response
        // ------------------------------------------------------------------
        private void ShowTickets()
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