using System;
using System.Drawing;
using System.Windows.Forms;
using MySqlConnector;
using PAYROLL.UI;

namespace PAYROLL
{
    // Rebuilt in code only, matching DashboardForm's approach — no
    // RegisterForm.Designer.cs or RegisterForm.resx needed. Delete both of
    // those files from the project; this single class replaces them.
    public class RegisterForm : Form
    {
        private readonly string connectionString = AppConfig.ConnectionString;

        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private TextBox txtConfirmPassword = null!;
        private Label errorLabel = null!;
        private Panel card = null!;
        private Panel rightPanel = null!;

        public RegisterForm()
        {
            Text = "Create Account - Payroll Management System";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(860, 560);
            Size = new Size(980, 600);
            BackColor = Color.White;
            Font = Theme.Body;

            BuildRightPanel();
            Controls.Add(AuthBrandPanel.Build(
                "Set up an admin account to manage payroll.",
                new[]
                {
                    (IconKind.Users, "Add and manage employee records"),
                    (IconKind.Card, "Run payroll for every cutoff period"),
                    (IconKind.Chart, "Keep accurate, auditable payroll history"),
                }));

            Load += (s, e) => CenterCard();
        }

        // ------------------------------------------------------------------
        // RIGHT PANEL — white background, centered registration card
        // ------------------------------------------------------------------
        private void BuildRightPanel()
        {
            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            card = new Panel { Size = new Size(360, 470), BackColor = Color.White };

            var title = new Label
            {
                Text = "Create Account",
                Font = new Font("Segoe UI Semibold", 22f),
                ForeColor = Theme.TextDark,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.White
            };
            var subtitle = new Label
            {
                Text = "Register a new account to access the dashboard.",
                Font = Theme.Body,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(0, 40),
                BackColor = Color.White
            };

            var (userField, userBox) = InputField.Create("USERNAME", false, new Size(360, 44), new Point(0, 96));
            var (passField, passBox) = InputField.Create("PASSWORD", true, new Size(360, 44), new Point(0, 176));
            var (confirmField, confirmBox) = InputField.Create("CONFIRM PASSWORD", true, new Size(360, 44), new Point(0, 256));
            txtUsername = userBox;
            txtPassword = passBox;
            txtConfirmPassword = confirmBox;

            errorLabel = new Label
            {
                Text = "",
                ForeColor = Theme.RedText,
                Font = Theme.SmallBold,
                Size = new Size(360, 20),
                Location = new Point(0, 326),
                BackColor = Color.White,
                Visible = false
            };

            var btnCreate = new ModernButton
            {
                Text = "Create Account",
                Location = new Point(0, 352),
                Size = new Size(360, 44),
                SurroundColor = Color.White
            };
            btnCreate.Click += BtnCreate_Click;

            var backRow = new Label
            {
                Text = "Already have an account?",
                Font = Theme.Body,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(0, 412),
                BackColor = Color.White
            };
            var backLink = new LinkLabel
            {
                Text = "Back to Login",
                Font = Theme.BodyBold,
                AutoSize = true,
                Location = new Point(backRow.Right + 6, 412),
                BackColor = Color.White,
                LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.AccentDark,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            backLink.Click += (s, e) => Close();

            card.Controls.Add(title);
            card.Controls.Add(subtitle);
            card.Controls.Add(userField);
            card.Controls.Add(passField);
            card.Controls.Add(confirmField);
            card.Controls.Add(errorLabel);
            card.Controls.Add(btnCreate);
            card.Controls.Add(backRow);
            card.Controls.Add(backLink);

            rightPanel.Controls.Add(card);
            rightPanel.Resize += (s, e) => CenterCard();
            Controls.Add(rightPanel);
        }

        private void CenterCard()
        {
            card.Location = new Point(
                Math.Max(20, (rightPanel.Width - card.Width) / 2),
                Math.Max(20, (rightPanel.Height - card.Height) / 2));
        }

        // ------------------------------------------------------------------
        // REGISTRATION LOGIC — same query/behavior as before, nicer errors
        // ------------------------------------------------------------------
        private void BtnCreate_Click(object? sender, EventArgs e)
        {
            errorLabel.Visible = false;
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();
            string confirm = txtConfirmPassword.Text.Trim();

            if (username.Length == 0 || password.Length == 0 || confirm.Length == 0)
            {
                ShowError("Please fill in all fields.");
                return;
            }
            if (password != confirm)
            {
                ShowError("Passwords do not match.");
                return;
            }

            try
            {
                using var connection = new MySqlConnection(connectionString);
                const string query = "INSERT INTO Accounts (Username, Password) VALUES (@Username, @Password)";
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@Username", username);
                command.Parameters.AddWithValue("@Password", password);

                connection.Open();
                command.ExecuteNonQuery();

                MessageBox.Show(
                    "Account created successfully! You can now log in.",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1062)
                    ShowError("That username already exists.");
                else
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