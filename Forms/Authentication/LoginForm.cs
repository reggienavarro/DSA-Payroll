using System;
using System.Drawing;
using System.Windows.Forms;
using MySqlConnector;
using PAYROLL.UI;

namespace PAYROLL
{
    // Rebuilt in code only, matching DashboardForm's approach — no
    // LoginForm.Designer.cs or LoginForm.resx needed. Delete both of those
    // files from the project; this single class replaces them.
    public class LoginForm : Form
    {
        private readonly string connectionString = AppConfig.ConnectionString;

        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private Label errorLabel = null!;
        private Panel loginCard = null!;
        private Panel rightPanel = null!;

        public LoginForm()
        {
            Text = "Login - Payroll Management System";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(860, 560);
            Size = new Size(980, 600);
            BackColor = Color.White;
            Font = Theme.Body;

            BuildRightPanel();  // Dock.Fill — build first so the brand panel can dock over it
            Controls.Add(AuthBrandPanel.Build(
                "Company Payroll and Workforce Management",
                new[]
                {
                    (IconKind.Users, "Manage employees across every project site"),
                    (IconKind.Calendar, "Track daily time records and overtime"),
                    (IconKind.Chart, "Generate accurate, auditable payslips"),
                }));

            Load += (s, e) => CenterCard();
        }

        // ------------------------------------------------------------------
        // RIGHT PANEL — white background, centered login card
        // ------------------------------------------------------------------
        private void BuildRightPanel()
        {
            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            loginCard = new Panel { Size = new Size(360, 400), BackColor = Color.White };

            var title = new Label
            {
                Text = "Welcome Back",
                Font = new Font("Segoe UI Semibold", 22f),
                ForeColor = Theme.TextDark,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.White
            };
            var subtitle = new Label
            {
                Text = "Sign in to manage payroll and employee records.",
                Font = Theme.Body,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(0, 40),
                BackColor = Color.White
            };

            var (userField, userBox) = InputField.Create("USERNAME", false, new Size(360, 44), new Point(0, 96));
            var (passField, passBox) = InputField.Create("PASSWORD", true, new Size(360, 44), new Point(0, 176));
            txtUsername = userBox;
            txtPassword = passBox;
            passBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) BtnLogin_Click(s, EventArgs.Empty);
            };

            errorLabel = new Label
            {
                Text = "",
                ForeColor = Theme.RedText,
                Font = Theme.SmallBold,
                Size = new Size(360, 20),
                Location = new Point(0, 246),
                BackColor = Color.White,
                Visible = false
            };

            var btnLogin = new ModernButton
            {
                Text = "Login",
                Location = new Point(0, 272),
                Size = new Size(360, 44),
                SurroundColor = Color.White
            };
            btnLogin.Click += BtnLogin_Click;

            var registerRow = new FlowLayoutPanel
            {
                Location = new Point(0, 332),
                Size = new Size(360, 28),
                AutoSize = false,
                BackColor = Color.White,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var registerPrompt = new Label
            {
                Text = "Don't have an account?",
                Font = Theme.Body,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                BackColor = Color.White,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var registerLink = new LinkLabel
            {
                Text = "Create one",
                Font = Theme.BodyBold,
                AutoSize = true,
                BackColor = Color.White,
                LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.AccentDark,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Margin = new Padding(6, 0, 0, 0),
                Padding = Padding.Empty
            };
            registerLink.Click += (s, e) =>
            {
                using var register = new RegisterForm();
                register.ShowDialog(this);
            };

            loginCard.Controls.Add(title);
            loginCard.Controls.Add(subtitle);
            loginCard.Controls.Add(userField);
            loginCard.Controls.Add(passField);
            loginCard.Controls.Add(errorLabel);
            loginCard.Controls.Add(btnLogin);
            registerRow.Controls.Add(registerPrompt);
            registerRow.Controls.Add(registerLink);
            loginCard.Controls.Add(registerRow);

            rightPanel.Controls.Add(loginCard);
            rightPanel.Resize += (s, e) => CenterCard();
            Controls.Add(rightPanel);
        }

        private void CenterCard()
        {
            loginCard.Location = new Point(
                Math.Max(20, (rightPanel.Width - loginCard.Width) / 2),
                Math.Max(20, (rightPanel.Height - loginCard.Height) / 2));
        }

        // ------------------------------------------------------------------
        // LOGIN LOGIC — same query/behavior as before, nicer error display
        // ------------------------------------------------------------------
        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            errorLabel.Visible = false;
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();

            if (username.Length == 0 || password.Length == 0)
            {
                ShowError("Please enter your username and password.");
                return;
            }

            try
            {
                // Local copies of the earlier database did not yet contain
                // Employees.DepartmentID. Upgrade that schema before the
                // role query below reads the supervisor's department.
                CompanyService.EnsureDepartmentSchema();
                using var connection = new MySqlConnection(connectionString);
                const string query = @"
                    SELECT a.Role, a.EmployeeID, e.DepartmentID
                    FROM Accounts a
                    LEFT JOIN Employees e ON e.EmployeeID = a.EmployeeID
                    WHERE a.Username = @Username
                    AND a.Password = @Password
                    LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@Username", username);
                command.Parameters.AddWithValue("@Password", password);

                connection.Open();
                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    string role = reader["Role"]?.ToString() ?? "Employee";
                    int? employeeId = reader["EmployeeID"] is DBNull ? null : Convert.ToInt32(reader["EmployeeID"]);
                    int? departmentId = reader["DepartmentID"] is DBNull ? null : Convert.ToInt32(reader["DepartmentID"]);
                    reader.Close();

                    if (AccountRoles.IsHrCashManager(role))
                    {
                        var dashboard = new DashboardForm(username, employeeId);
                        dashboard.Show();
                    }
                    else if (role == AccountRoles.Employee && employeeId.HasValue)
                    {
                        var portal = new EmployeeDashboardForm(username, employeeId.Value);
                        portal.Show();
                    }
                    else if (AccountRoles.IsTeamLeaderSupervisor(role) && employeeId.HasValue)
                    {
                        var portal = new EmployeeDashboardForm(username, employeeId.Value,
                            canManageAttendance: true, attendanceDepartmentScopeId: departmentId);
                        portal.Show();
                    }
                    else
                    {
                        ShowError("This account role is not supported. Contact an admin.");
                        return;
                    }
                    Hide();
                }
                else
                {
                    ShowError("Invalid username or password.");
                }
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowError(string message)
        {
            errorLabel.Text = message;
            errorLabel.Visible = true;
        }
    }
}
