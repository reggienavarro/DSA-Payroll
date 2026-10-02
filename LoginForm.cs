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
                "Civil Engineering Payroll & Workforce Management",
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

            var registerRow = new Label
            {
                Text = "Don't have an account?",
                Font = Theme.Body,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(0, 332),
                BackColor = Color.White
            };
            var registerLink = new LinkLabel
            {
                Text = "Create one",
                Font = Theme.BodyBold,
                AutoSize = true,
                Location = new Point(registerRow.Right + 6, 332),
                BackColor = Color.White,
                LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.AccentDark,
                LinkBehavior = LinkBehavior.HoverUnderline
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
            loginCard.Controls.Add(registerRow);
            loginCard.Controls.Add(registerLink);

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
                using var connection = new MySqlConnection(connectionString);
                const string query = @"
                    SELECT Role, EmployeeID
                    FROM Accounts
                    WHERE Username = @Username
                    AND Password = @Password
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
                    reader.Close();

                    if (role is "Admin" or "HR" or "Cash Management")
                    {
                        var dashboard = new DashboardForm(username);
                        dashboard.Show();
                    }
                    else if (role == "Employee" && employeeId.HasValue)
                    {
                        var portal = new EmployeeDashboardForm(username, employeeId.Value);
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