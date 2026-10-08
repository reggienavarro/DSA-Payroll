using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using System.Windows.Forms;

namespace PAYROLL
{
    public sealed class PayslipPreviewForm : Form
    {
        private readonly Payslip payslip;
        private readonly PrintDocument printDocument = new();
        private readonly RichTextBox preview = new();

        public PayslipPreviewForm(Payslip payslip)
        {
            this.payslip = payslip;
            Text = $"Payslip - {payslip.EmployeeName}";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 760);
            MinimizeBox = false;

            var print = new Button { Text = "Print / Save PDF", Dock = DockStyle.Bottom, Height = 42 };
            print.Click += Print_Click;
            preview.Dock = DockStyle.Fill;
            preview.ReadOnly = true;
            preview.Font = new Font("Consolas", 10f);
            preview.Text = BuildText();
            Controls.Add(preview);
            Controls.Add(print);

            printDocument.BeginPrint += (s, e) => { };
            printDocument.PrintPage += PrintPage;
        }

        private string BuildText()
        {
            var b = new StringBuilder();
            b.AppendLine("PAYROLL PAYSLIP");
            b.AppendLine(new string('=', 68));
            b.AppendLine($"Employee:   {payslip.EmployeeName}");
            b.AppendLine($"Department: {payslip.DepartmentName}");
            b.AppendLine($"Cutoff:     {payslip.CutoffStart:MMM d, yyyy} - {payslip.CutoffEnd:MMM d, yyyy}");
            b.AppendLine($"Generated:  {payslip.CreatedAt:MMM d, yyyy h:mm tt}");
            b.AppendLine(new string('-', 68));
            Add(b, "Basic Pay", payslip.BasicPay);
            Add(b, "Night Differential", payslip.NdPayPrem);
            Add(b, "Rice Allowance", payslip.RiceAllowance);
            Add(b, "Daily Meal", payslip.DailyMeal);
            Add(b, "Uniform", payslip.Uniform);
            Add(b, "Laundry", payslip.Laundry);
            foreach (var benefit in payslip.Benefits)
                Add(b, benefit.BenefitName, benefit.Amount);
            Add(b, "Overtime", payslip.TotalOtPay);
            Add(b, "Regular Holiday", payslip.RegHolPayPrem);
            Add(b, "Special Holiday", payslip.SpHolPayPrem);
            Add(b, "Leave With Pay", payslip.LeaveWithPay);
            Add(b, "Adjustment", payslip.Adjustment);
            Add(b, "TOTAL EARNINGS", payslip.Total);
            b.AppendLine();
            Add(b, "Absences", payslip.Absences);
            Add(b, "Late / Undertime / Overbreak", payslip.LateUtOb);
            Add(b, "SSS", payslip.SssContribution);
            Add(b, "PhilHealth", payslip.PhilHealthContribution);
            Add(b, "HMDF", payslip.HmdfContribution);
            Add(b, "Loans", payslip.Loans);
            Add(b, "TOTAL DEDUCTIONS", payslip.TotalDeductions);
            Add(b, "NET PAY", payslip.NetPay);
            b.AppendLine();
            Add(b, "Attendance Bonus", payslip.AttendanceBonus);
            Add(b, "Tenure Bonus", payslip.TenureBonus);
            // Keep legacy non-zero entries visible on previously saved payslips,
            // but don't show these retired bonus fields on newly generated slips.
            if (payslip.Oic != 0m) Add(b, "OIC", payslip.Oic);
            if (payslip.Account != 0m) Add(b, "Account", payslip.Account);
            Add(b, "Incentives", payslip.Incentives);
            Add(b, "Internal Commission", payslip.InternalCommission);
            Add(b, "TOTAL AMOUNT RECEIVABLE", payslip.TotalAmountReceivable);
            return b.ToString();
        }

        private static void Add(StringBuilder b, string label, decimal amount) =>
            b.AppendLine($"{label,-35} {amount,15:N2}");

        private void Print_Click(object? sender, EventArgs e)
        {
            using var dialog = new PrintDialog { Document = printDocument, UseEXDialog = true };
            MessageBox.Show("Choose a printer, or choose 'Microsoft Print to PDF' to save this payslip as a PDF.",
                "Print Payslip", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (dialog.ShowDialog(this) == DialogResult.OK)
                printDocument.Print();
        }

        private void PrintPage(object? sender, PrintPageEventArgs e)
        {
            using var font = new Font("Consolas", 10f);
            e.Graphics.DrawString(preview.Text, font, Brushes.Black, e.MarginBounds.Left, e.MarginBounds.Top);
            e.HasMorePages = false;
        }
    }
}
