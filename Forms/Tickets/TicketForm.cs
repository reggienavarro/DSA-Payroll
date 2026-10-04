using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    // Employee-submitted ticket for salary disputes, reports, and other concerns.
    public class TicketForm : Form
    {
        private TextBox txtSubject = null!;
        private TextBox txtMessage = null!;
        private ComboBox urgencyBox = null!;
        private ComboBox categoryBox = null!;

        public string Subject => Category == "Other" ? txtSubject.Text.Trim() : Category;
        public string Message => txtMessage.Text.Trim();
        public string Urgency => urgencyBox.SelectedItem?.ToString() ?? "Medium";
        public string Category => categoryBox.SelectedItem?.ToString() ?? "Other";

        public TicketForm()
        {
            Text = "Create a Ticket";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(440, 485);
            BackColor = Color.White;
            Font = Theme.Body;

            var title = new Label
            {
                Text = "Create a Ticket",
                Font = new Font("Segoe UI Semibold", 15f),
                ForeColor = Theme.TextDark,
                AutoSize = true,
                Location = new Point(24, 20),
                BackColor = Color.White
            };
            var subtitle = new Label
            {
                Text = "Describe the issue — management will review and respond here.",
                Font = Theme.Small,
                ForeColor = Theme.TextGray,
                Size = new Size(380, 34),
                Location = new Point(24, 50),
                BackColor = Color.White
            };

            var categoryLabel = new Label
            {
                Text = "CATEGORY", Font = Theme.SmallBold, ForeColor = Theme.TextGray,
                AutoSize = true, Location = new Point(24, 92), BackColor = Color.White
            };
            categoryBox = new ComboBox
            {
                Location = new Point(24, 112), Width = 380,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            categoryBox.Items.AddRange(new object[] { "Salary dispute", "Report", "Other" });
            categoryBox.SelectedItem = "Other";

            var (subjectField, subjectBox) = InputField.Create("OTHER SUBJECT", false, new Size(380, 40), new Point(24, 154));
            txtSubject = subjectBox;
            subjectField.Visible = true;

            var urgencyLabel = new Label
            {
                Text = "URGENCY", Font = Theme.SmallBold, ForeColor = Theme.TextGray,
                AutoSize = true, Location = new Point(24, 232), BackColor = Color.White
            };
            urgencyBox = new ComboBox
            {
                Location = new Point(24, 252), Width = 380,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            urgencyBox.Items.AddRange(new object[] { "Low", "Medium", "High" });
            urgencyBox.SelectedItem = "Medium";

            var messageLabel = new Label
            {
                Text = "MESSAGE",
                Font = Theme.SmallBold,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(24, 292),
                BackColor = Color.White
            };
            var messageBox = new RoundedPanel
            {
                Location = new Point(24, 314), Size = new Size(380, 86),
                CornerRadius = 10, SurroundColor = Color.White
            };
            txtMessage = new TextBox
            {
                Multiline = true, BorderStyle = BorderStyle.None, Font = Theme.Body,
                Location = new Point(12, 10), Size = new Size(356, 64),
                ScrollBars = ScrollBars.Vertical
            };
            messageBox.Controls.Add(txtMessage);

            var btnSubmit = new ModernButton
            {
                Text = "Submit", Location = new Point(24, 410), Size = new Size(180, 40),
                SurroundColor = Color.White
            };
            btnSubmit.Click += (s, e) =>
            {
                if ((Category == "Other" && txtSubject.Text.Trim().Length == 0) || txtMessage.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Please enter a subject for Other tickets and provide a message.", "Missing Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new ModernButton
            {
                Text = "Cancel", Location = new Point(224, 410), Size = new Size(180, 40),
                SurroundColor = Color.White, FillColor = Color.FromArgb(229, 231, 235)
            };
            btnCancel.ForeColor = Theme.TextDark;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            void UpdateCategoryLayout()
            {
                bool needsSubject = Category == "Other";
                subjectField.Visible = needsSubject;
                int shift = needsSubject ? 0 : -78;
                urgencyLabel.Top = 232 + shift;
                urgencyBox.Top = 252 + shift;
                messageLabel.Top = 292 + shift;
                messageBox.Top = 314 + shift;
                btnSubmit.Top = 410 + shift;
                btnCancel.Top = 410 + shift;
                ClientSize = new Size(440, needsSubject ? 480 : 402);
            }
            categoryBox.SelectedIndexChanged += (s, e) => UpdateCategoryLayout();
            UpdateCategoryLayout();

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(subjectField);
            Controls.Add(categoryLabel);
            Controls.Add(categoryBox);
            Controls.Add(urgencyLabel);
            Controls.Add(urgencyBox);
            Controls.Add(messageLabel);
            Controls.Add(messageBox);
            Controls.Add(btnSubmit);
            Controls.Add(btnCancel);
        }
    }
}
