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
            startDate.ValueChanged += (s, e) => endDate.MinDate = startDate.Value.Date;

            var endLabel = new Label
            {
                Text = "END DATE", Location = new Point(230, 20), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            endDate.Location = new Point(230, 44);
            endDate.Width = 190;
            endDate.MinDate = DateTime.Today;

            var reasonLabel = new Label
            {
                Text = "REASON", Location = new Point(20, 88), AutoSize = true,
                Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
            };
            reason.Location = new Point(20, 112);
            reason.Size = new Size(400, 110);
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
            Controls.Add(reasonLabel);
            Controls.Add(reason);
            Controls.Add(cancel);
            Controls.Add(submit);
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