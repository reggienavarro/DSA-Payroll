using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL
{
    public sealed class LeaveCreditsForm : Form
    {
        private readonly string administrator;
        private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly NumericUpDown year = new() { Minimum = 2000, Maximum = 2100, Value = DateTime.Today.Year, Width = 95 };
        private readonly NumericUpDown entitlement = new() { Minimum = 0, Maximum = 366, DecimalPlaces = 2, Increment = 0.5m, Width = 120 };
        private readonly Label selectedEmployee = new() { AutoSize = true, ForeColor = Theme.TextDark };

        public LeaveCreditsForm(string administrator)
        {
            this.administrator = administrator;
            Text = "Employee Leave Credits";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(850, 570);
            MinimumSize = new Size(700, 430);
            BackColor = Theme.Bg;

            var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Bg, Padding = new Padding(18) };
            header.Controls.Add(new Label { Text = "Annual paid-leave credits", Font = Theme.H2, ForeColor = Theme.TextDark, AutoSize = true, Location = new Point(18, 10) });
            header.Controls.Add(new Label { Text = "Set each employee’s allowance according to the beneficiary’s policy. Pending requests do not use credits; approval does.", Font = Theme.Small, ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(18, 48) });

            var yearBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(18, 5, 0, 0), BackColor = Theme.Bg };
            yearBar.Controls.Add(new Label { Text = "Leave year:", AutoSize = true, Margin = new Padding(0, 7, 6, 0), ForeColor = Theme.TextDark });
            yearBar.Controls.Add(year);
            year.ValueChanged += (_, _) => RefreshBalances();

            GridStyle.Apply(grid);
            grid.SelectionChanged += (_, _) => LoadSelectedEntitlement();

            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(14, 10, 14, 8), BackColor = Theme.Bg, WrapContents = false };
            footer.Controls.Add(selectedEmployee);
            selectedEmployee.Margin = new Padding(0, 10, 18, 0);
            footer.Controls.Add(new Label { Text = "Annual allowance (days):", AutoSize = true, Margin = new Padding(0, 9, 7, 0), ForeColor = Theme.TextDark });
            footer.Controls.Add(entitlement);
            var save = new ModernButton { Text = "Save allowance", Size = new Size(145, 36), Margin = new Padding(12, 0, 0, 0), SurroundColor = Theme.Bg };
            save.Click += (_, _) => SaveAllowance();
            footer.Controls.Add(save);

            Controls.Add(grid);
            Controls.Add(footer);
            Controls.Add(yearBar);
            Controls.Add(header);
            RefreshBalances();
        }

        private void RefreshBalances()
        {
            try
            {
                var table = new DataTable();
                table.Columns.Add("EmployeeID", typeof(int));
                table.Columns.Add("Employee");
                table.Columns.Add("Annual allowance", typeof(decimal));
                table.Columns.Add("Used", typeof(decimal));
                table.Columns.Add("Available", typeof(decimal));
                foreach (var balance in LeaveService.ListCreditBalances((int)year.Value))
                    table.Rows.Add(balance.EmployeeId, balance.EmployeeName, balance.EntitledDays, balance.UsedDays, balance.AvailableDays);
                grid.DataSource = table;
                if (grid.Columns["EmployeeID"] is DataGridViewColumn id) id.Visible = false;
                grid.Columns["Annual allowance"]!.DefaultCellStyle.Format = "0.##";
                grid.Columns["Used"]!.DefaultCellStyle.Format = "0.##";
                grid.Columns["Available"]!.DefaultCellStyle.Format = "0.##";
                LoadSelectedEntitlement();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load leave credits: " + ex.Message, "Leave Credits", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadSelectedEntitlement()
        {
            if (grid.CurrentRow?.Cells["EmployeeID"].Value is int employeeId)
            {
                selectedEmployee.Text = grid.CurrentRow.Cells["Employee"].Value?.ToString() ?? "Employee";
                entitlement.Value = Math.Clamp(Convert.ToDecimal(grid.CurrentRow.Cells["Annual allowance"].Value), entitlement.Minimum, entitlement.Maximum);
            }
            else selectedEmployee.Text = "Select an employee";
        }

        private void SaveAllowance()
        {
            if (grid.CurrentRow?.Cells["EmployeeID"].Value is not int employeeId)
            {
                MessageBox.Show(this, "Select an employee first.", "Leave Credits", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                LeaveService.SetAnnualCredits(employeeId, (int)year.Value, entitlement.Value, administrator);
                RefreshBalances();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Leave Credits", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
