using System;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    // Opened when an admin double-clicks a ticket row in DashboardForm.
    public class TicketResponseForm : Form
    {
        private TextBox txtResponse = null!;
        public string Response => txtResponse.Text.Trim();

        public TicketResponseForm(TicketRow ticket, bool readOnly = false)
        {
            Text = readOnly ? "Ticket Details" : "Respond to Ticket";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(460, 460);
            BackColor = Color.White;
            Font = Theme.Body;

            var title = new Label
            {
                Text = ticket.Subject,
                Font = new Font("Segoe UI Semibold", 15f),
                ForeColor = Theme.TextDark,
                AutoSize = false,
                Size = new Size(400, 40),
                Location = new Point(24, 20),
                BackColor = Color.White
            };
            var meta = new Label
            {
                Text = $"From {ticket.EmployeeName}  ·  {ticket.DepartmentName}  ·  {ticket.CreatedAt:MMM d, yyyy}  ·  {ticket.Urgency} urgency  ·  {ticket.Status}",
                Font = Theme.Small,
                ForeColor = Theme.TextGray,
                AutoSize = false,
                Size = new Size(400, 22),
                AutoEllipsis = true,
                Location = new Point(24, 58),
                BackColor = Color.White
            };

            var messageLabel = new Label
            {
                Text = "EMPLOYEE'S MESSAGE",
                Font = Theme.SmallBold,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(24, 92),
                BackColor = Color.White
            };
            var messageBox = new RoundedPanel
            {
                Location = new Point(24, 114), Size = new Size(400, 90),
                CornerRadius = 10, SurroundColor = Color.White
            };
            var messageText = new TextBox
            {
                Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
                Font = Theme.Body, Location = new Point(12, 10), Size = new Size(376, 70),
                ScrollBars = ScrollBars.Vertical, Text = ticket.Message
            };
            messageBox.Controls.Add(messageText);

            var responseLabel = new Label
            {
                Text = readOnly ? "MANAGEMENT RESPONSE" : "YOUR RESPONSE",
                Font = Theme.SmallBold,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(24, 218),
                BackColor = Color.White
            };
            var responsePanel = new RoundedPanel
            {
                Location = new Point(24, 240), Size = new Size(400, 110),
                CornerRadius = 10, SurroundColor = Color.White
            };
            txtResponse = new TextBox
            {
                Multiline = true, BorderStyle = BorderStyle.None, Font = Theme.Body,
                Location = new Point(12, 10), Size = new Size(376, 90),
                ScrollBars = ScrollBars.Vertical,
                Text = ticket.AdminResponse ?? "",
                ReadOnly = readOnly
            };
            responsePanel.Controls.Add(txtResponse);

            var btnSubmit = new ModernButton
            {
                Text = readOnly ? "Close" : "Send Response & Resolve", Location = new Point(24, 370),
                Size = new Size(400, 42), SurroundColor = Color.White
            };
            btnSubmit.Click += (s, e) =>
            {
                if (readOnly)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }
                if (txtResponse.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Write a response before resolving this ticket.", "Missing Response",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(title);
            Controls.Add(meta);
            Controls.Add(messageLabel);
            Controls.Add(messageBox);
            Controls.Add(responseLabel);
            Controls.Add(responsePanel);
            Controls.Add(btnSubmit);
        }
    }
}
