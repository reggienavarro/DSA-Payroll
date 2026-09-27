using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    // Opened from EmployeeDashboardForm's "File a Salary Dispute" button.
    public class TicketForm : Form
    {
        private TextBox txtSubject = null!;
        private TextBox txtMessage = null!;

        public string Subject => txtSubject.Text.Trim();
        public string Message => txtMessage.Text.Trim();

        public TicketForm()
        {
            Text = "File a Salary Dispute";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(440, 420);
            BackColor = Color.White;
            Font = Theme.Body;

            var title = new Label
            {
                Text = "File a Salary Dispute",
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

            var (subjectField, subjectBox) = InputField.Create("SUBJECT", false, new Size(380, 40), new Point(24, 92));
            txtSubject = subjectBox;

            var messageLabel = new Label
            {
                Text = "MESSAGE",
                Font = Theme.SmallBold,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(24, 158),
                BackColor = Color.White
            };
            var messageBox = new RoundedPanel
            {
                Location = new Point(24, 180), Size = new Size(380, 140),
                CornerRadius = 10, SurroundColor = Color.White
            };
            txtMessage = new TextBox
            {
                Multiline = true, BorderStyle = BorderStyle.None, Font = Theme.Body,
                Location = new Point(12, 10), Size = new Size(356, 118),
                ScrollBars = ScrollBars.Vertical
            };
            messageBox.Controls.Add(txtMessage);

            var btnSubmit = new ModernButton
            {
                Text = "Submit", Location = new Point(24, 336), Size = new Size(180, 40),
                SurroundColor = Color.White
            };
            btnSubmit.Click += (s, e) =>
            {
                if (txtSubject.Text.Trim().Length == 0 || txtMessage.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Please fill in both the subject and the message.", "Missing Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new ModernButton
            {
                Text = "Cancel", Location = new Point(224, 336), Size = new Size(180, 40),
                SurroundColor = Color.White, FillColor = Color.FromArgb(229, 231, 235)
            };
            btnCancel.ForeColor = Theme.TextDark;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(subjectField);
            Controls.Add(messageLabel);
            Controls.Add(messageBox);
            Controls.Add(btnSubmit);
            Controls.Add(btnCancel);
        }
    }
}