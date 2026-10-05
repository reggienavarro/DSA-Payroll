using System;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    public class LeaveRequestForm : Form
    {
        private readonly int employeeId;
        private readonly DateTimePicker startDate = new();
        private readonly DateTimePicker endDate = new();
        private readonly TextBox reason = new();
        private readonly Label creditSummary = new();

        public LeaveRequestForm(int employeeId)
        {
            this.employeeId = employeeId;
            Text = "Apply for Leave";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 310);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            var startLabel = new Label
            {
                Text = "START DATE", Location = new Point(20, 20), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            startDate.Location = new Point(20, 44);
            startDate.Width = 190;
            startDate.MinDate = DateTime.Today;
            startDate.ValueChanged += (s, e) =>
            {
                endDate.MinDate = DateTime.Today;
                endDate.MaxDate = new DateTime(startDate.Value.Year, 12, 31);
                endDate.MinDate = startDate.Value.Date;
                RefreshCreditSummary();
            };

            var endLabel = new Label
            {
                Text = "END DATE", Location = new Point(230, 20), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            endDate.Location = new Point(230, 44);
            endDate.Width = 190;
            endDate.MinDate = DateTime.Today;
            endDate.MaxDate = new DateTime(DateTime.Today.Year, 12, 31);
            endDate.ValueChanged += (s, e) => RefreshCreditSummary();

            creditSummary.Location = new Point(20, 78);
            creditSummary.AutoSize = true;
            creditSummary.Font = Theme.Small;
            creditSummary.ForeColor = Theme.TextGray;
            creditSummary.BackColor = Theme.Bg;

            var reasonLabel = new Label
            {
                Text = "REASON", Location = new Point(20, 108), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            reason.Location = new Point(20, 132);
            reason.Size = new Size(400, 90);
            reason.Multiline = true;
            reason.MaxLength = 500;
            reason.ScrollBars = ScrollBars.Vertical;

            var cancel = new ModernButton
            {
                Text = "Cancel", Location = new Point(210, 248), Size = new Size(100, 40),
                SurroundColor = Theme.Bg, FillColor = Color.FromArgb(229, 231, 235)
            };
            cancel.ForeColor = Theme.TextDark;
            cancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            var submit = new ModernButton
            {
                Text = "Submit Request", Location = new Point(320, 248), Size = new Size(100, 40),
                SurroundColor = Theme.Bg
            };
            submit.Click += Submit_Click;

            Controls.Add(startLabel);
            Controls.Add(startDate);
            Controls.Add(endLabel);
            Controls.Add(endDate);
            Controls.Add(creditSummary);
            Controls.Add(reasonLabel);
            Controls.Add(reason);
            Controls.Add(cancel);
            Controls.Add(submit);
            RefreshCreditSummary();
        }

        private void RefreshCreditSummary()
        {
            try
            {
                var balance = LeaveService.GetCreditBalance(employeeId, startDate.Value.Year);
                decimal requested = LeaveService.GetRequestedWorkdays(startDate.Value, endDate.Value);
                creditSummary.Text = $"{startDate.Value.Year} paid leave credits: {balance.AvailableDays:0.##} available of {balance.EntitledDays:0.##}; this request uses {requested:0.##} workday(s).";
                creditSummary.ForeColor = requested > balance.AvailableDays ? Color.Firebrick : Theme.TextGray;
            }
            catch
            {
                creditSummary.Text = "Leave-credit balance could not be loaded. You may still submit for management review.";
                creditSummary.ForeColor = Color.Firebrick;
            }
        }

        private void Submit_Click(object? sender, EventArgs e)
        {
            try
            {
                LeaveService.CreateRequest(employeeId, startDate.Value, endDate.Value, reason.Text);
                MessageBox.Show("Leave request submitted for management review.", "Leave Request",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Leave Request", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
