using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using MySqlConnector;
namespace PAYROLL
{
    public partial class Form1 : Form
    {
       string connectionString = AppConfig.ConnectionString;

        public Form1()
        {
            InitializeComponent();
            txtDeductions.ReadOnly = true; // now auto-computed by DeductionsCalculator, not typed in
            txtBasicSalary.Text = GetDefaultHourlyRate().ToString("0.00");
            BuildPayslipSection();
            LoadEmployees();
        }

        // ------------------------------------------------------------------
        // PAYROLL — replaces the old flat Regular/Overtime/Gross/Net
        // calculator. Payroll is now itemized per cutoff period via the
        // Payslips table; this panel just opens that generator for whichever
        // employee is currently selected/loaded on this screen.
        // ------------------------------------------------------------------
        private void BuildPayslipSection()
        {
            groupBox1.Visible = false; // the old calculator (btnCalculate, txtRegularPay, etc.) is retired

            var panel = new GroupBox
            {
                Text = "PAYROLL",
                Location = groupBox1.Location,
                Size = groupBox1.Size
            };

            var info = new Label
            {
                Text = "Select an employee below (or enter their Employee ID above), then generate their " +
                       "itemized payslip: basic pay, allowances, government deductions, and bonuses.",
                Location = new Point(20, 30),
                Size = new Size(panel.Width - 40, 90),
                AutoSize = false
            };

            var btnGenerate = new PAYROLL.UI.ModernButton
            {
                Text = "Generate Payslip",
                ShowPlus = true,
                Location = new Point(20, 140),
                Size = new Size(panel.Width - 40, 42),
                SurroundColor = Color.White
            };
            btnGenerate.Click += BtnGeneratePayslip_Click;

            var note = new Label
            {
                Text = "Saving a payslip also updates this employee's Basic Salary, Gross Pay, " +
                       "Deductions, and Net Pay below, so the Overview dashboard stays current.",
                Font = new Font(Font, FontStyle.Italic),
                ForeColor = SystemColors.GrayText,
                Location = new Point(20, 195),
                Size = new Size(panel.Width - 40, 60),
                AutoSize = false
            };

            panel.Controls.Add(info);
            panel.Controls.Add(btnGenerate);
            panel.Controls.Add(note);
            Controls.Add(panel);
        }

        private void BtnGeneratePayslip_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtEmployeeID.Text, out int employeeId))
            {
                MessageBox.Show(
                    "Click an employee row in the list first (or type a valid Employee ID above).",
                    "Select an Employee", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new PayslipForm(employeeId);
            if (form.ShowDialog(this) == DialogResult.OK)
                LoadEmployees(); // refresh so the updated Gross/Net/Deductions show immediately
        }

        private void LoadEmployees()
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    string query = "SELECT EmployeeID, EmployeeName, Position, HourlyRate FROM Employees ORDER BY EmployeeName";

                    MySqlDataAdapter adapter = new MySqlDataAdapter(query, connection);

                    DataTable table = new DataTable();

                    adapter.Fill(table);

                    dgvPayroll.DataSource = table;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            if (!UpdatePayrollFields())
            {
                MessageBox.Show(
                    "Please enter valid numbers for salary, hours worked, and overtime hours.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void EmployeePayrollInput_TextChanged(object? sender, EventArgs e)
        {
            UpdatePayrollFields();
        }

        private bool UpdatePayrollFields()
        {
            if (!decimal.TryParse(txtBasicSalary.Text, out decimal basicSalary))
            {
                txtDeductions.Clear();
                txtRegularPay.Clear();
                txtOvertimePay.Clear();
                txtGrossPay.Clear();
                txtNetPay.Clear();
                return false;
            }

            decimal deductions = DeductionsCalculator.Compute(basicSalary).total;
            txtDeductions.Text = deductions.ToString("N2");

            if (!decimal.TryParse(txtHoursWorked.Text, out decimal hoursWorked) ||
                !decimal.TryParse(txtOvertimeHours.Text, out decimal overtimeHours))
            {
                txtRegularPay.Clear();
                txtOvertimePay.Clear();
                txtGrossPay.Clear();
                txtNetPay.Clear();
                return false;
            }

            decimal hourlyRate = basicSalary / 160m;
            decimal regularPay = hourlyRate * hoursWorked;
            decimal overtimePay = hourlyRate * overtimeHours * 1.25m;
            decimal grossPay = regularPay + overtimePay;
            decimal netPay = grossPay - deductions;

            txtRegularPay.Text = regularPay.ToString("N2");
            txtOvertimePay.Text = overtimePay.ToString("N2");
            txtGrossPay.Text = grossPay.ToString("N2");
            txtNetPay.Text = netPay.ToString("N2");
            return true;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            try
            {
                if (!TryGetEmployeeProfile(out int employeeID, out string employeeName,
                    out string position, out decimal hourlyRate))
                {
                    MessageBox.Show(
                        "Enter a valid employee ID, name, position, and hourly rate.",
                        "Invalid Input",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    string query = @"
                INSERT INTO Employees
                (
                    EmployeeID,
                    EmployeeName,
                    Position,
                    HourlyRate,
                    BasicSalary,
                    HoursWorked,
                    OvertimeHours,
                    Deductions,
                    OvertimePay,
                    GrossPay,
                    NetPay
                )
                VALUES
                (
                    @EmployeeID,
                    @EmployeeName,
                    @Position,
                    @HourlyRate,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0
                )";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeID", employeeID);
                        command.Parameters.AddWithValue("@EmployeeName", employeeName);
                        command.Parameters.AddWithValue("@Position", position);
                        command.Parameters.AddWithValue("@HourlyRate", hourlyRate);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                MessageBox.Show(
                    "Employee added successfully!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadEmployees();
            }
            catch (FormatException)
            {
                MessageBox.Show(
                    "Please enter valid values in all required fields.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            try
            {
                if (!TryGetEmployeeProfile(out int employeeID, out string employeeName,
                    out string position, out decimal hourlyRate))
                {
                    MessageBox.Show(
                        "Enter a valid employee ID, name, position, and hourly rate.",
                        "Invalid Input",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    string query = @"
                UPDATE Employees
                SET
                    EmployeeName = @EmployeeName,
                    Position = @Position,
                    HourlyRate = @HourlyRate
                WHERE EmployeeID = @EmployeeID";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeID", employeeID);
                        command.Parameters.AddWithValue("@EmployeeName", employeeName);
                        command.Parameters.AddWithValue("@Position", position);
                        command.Parameters.AddWithValue("@HourlyRate", hourlyRate);

                        connection.Open();

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            MessageBox.Show(
                                "Employee ID not found.",
                                "Update",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);

                            return;
                        }
                    }
                }

                MessageBox.Show(
                    "Employee updated successfully!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadEmployees();
            }
            catch (FormatException)
            {
                MessageBox.Show(
                    "Please enter valid values.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
     
        private void dgvPayroll_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            DataGridViewRow row = dgvPayroll.Rows[e.RowIndex];

            txtEmployeeID.Text = row.Cells["EmployeeID"].Value?.ToString();
            txtEmployeeName.Text = row.Cells["EmployeeName"].Value?.ToString();
            txtPosition.Text = row.Cells["Position"].Value?.ToString();
            txtBasicSalary.Text = row.Cells["HourlyRate"].Value?.ToString();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            try
            {
                // Make sure an employee is selected
                if (string.IsNullOrWhiteSpace(txtEmployeeID.Text))
                {
                    MessageBox.Show(
                        "Please select an employee first.",
                        "Delete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }


                int employeeID =
                    int.Parse(txtEmployeeID.Text);


                // Confirmation
                DialogResult result =
                    MessageBox.Show(
                        "Delete this employee? This also permanently deletes their login account, payslips, tickets, and attendance records.",
                        "Confirm Delete",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);


                if (result != DialogResult.Yes)
                    return;


                int rowsAffected = DeleteEmployeeWithRelatedRecords(employeeID);

                if (rowsAffected == 0)
                {
                    MessageBox.Show(
                        "Employee not found.",
                        "Delete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }


                MessageBox.Show(
                    "Employee deleted successfully!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);


                // Refresh DataGridView
                LoadEmployees();

                // Clear textboxes
                ClearFields();
            }
            catch (FormatException)
            {
                MessageBox.Show(
                    "Invalid Employee ID.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        // Deletes the employee AND every row that points at them, in one
        // transaction: either everything goes or nothing does. Child tables
        // are deleted first because MySQL's foreign keys refuse to remove a
        // parent row that still has children (that was the "Cannot delete or
        // update a parent row" error).
        private int DeleteEmployeeWithRelatedRecords(int employeeID)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                // Fixed list of table names (never user input). A table that
                // doesn't exist yet (error 1146) simply has nothing to delete.
                foreach (var table in new[] { "Attendance", "LeaveRequests", "SalaryTickets", "Payslips", "Accounts" })
                {
                    try
                    {
                        using var cmd = new MySqlCommand(
                            $"DELETE FROM {table} WHERE EmployeeID = @EmployeeID", connection, transaction);
                        cmd.Parameters.AddWithValue("@EmployeeID", employeeID);
                        cmd.ExecuteNonQuery();
                    }
                    catch (MySqlException ex) when (ex.Number == 1146) { }
                }

                int rows;
                using (var cmd = new MySqlCommand(
                    "DELETE FROM Employees WHERE EmployeeID = @EmployeeID", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@EmployeeID", employeeID);
                    rows = cmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return rows;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private void ClearFields()
        {
            txtEmployeeID.Clear();
            txtEmployeeName.Clear();
            txtPosition.Clear();
            txtBasicSalary.Text = GetDefaultHourlyRate().ToString("0.00");
            txtHoursWorked.Clear();
            txtOvertimeHours.Clear();
            txtDeductions.Clear();
            txtRegularPay.Clear();
            txtOvertimePay.Clear();
            txtGrossPay.Clear();
            txtNetPay.Clear();
        }

        private bool TryGetEmployeeProfile(out int employeeID, out string employeeName,
            out string position, out decimal hourlyRate)
        {
            employeeID = 0;
            hourlyRate = 0;
            employeeName = txtEmployeeName.Text.Trim();
            position = txtPosition.Text.Trim();
            return int.TryParse(txtEmployeeID.Text, out employeeID) &&
                employeeName.Length > 0 && position.Length > 0 &&
                decimal.TryParse(txtBasicSalary.Text, out hourlyRate) && hourlyRate > 0;
        }

        private static decimal GetDefaultHourlyRate()
        {
            try { return CompanyService.GetPayrollDefaults().DefaultHourlyRate; }
            catch { return PayrollDefaults.DefaultHourlyRate; }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            ClearFields();
        }
    }
}