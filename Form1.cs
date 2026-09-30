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
                    string query = "SELECT * FROM Employees";

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
            try
            {
                decimal basicSalary = decimal.Parse(txtBasicSalary.Text);
                decimal hoursWorked = decimal.Parse(txtHoursWorked.Text);
                decimal overtimeHours = decimal.Parse(txtOvertimeHours.Text);

                // Standard monthly working hours
                decimal hourlyRate = basicSalary / 160m;

                // Regular pay
                decimal regularPay = hourlyRate * hoursWorked;

                // Overtime pay = hourly rate × overtime hours × 1.25
                decimal overtimePay = hourlyRate * overtimeHours * 1.25m;

                // Gross pay
                decimal grossPay = regularPay + overtimePay;

                // Government-mandated deductions (SSS, PhilHealth, Pag-IBIG,
                // withholding tax) computed from basic salary — no longer
                // manually typed in, so it can't drift out of sync with the
                // employee's actual salary bracket.
                var d = DeductionsCalculator.Compute(basicSalary);
                decimal deductions = d.total;

                // Net pay
                decimal netPay = grossPay - deductions;

                // Display results
                txtRegularPay.Text = regularPay.ToString("N2");
                txtOvertimePay.Text = overtimePay.ToString("N2");
                txtGrossPay.Text = grossPay.ToString("N2");
                txtDeductions.Text = deductions.ToString("N2");
                txtNetPay.Text = netPay.ToString("N2");
            }
            catch
            {
                MessageBox.Show(
                    "Please enter valid numbers for salary, hours worked, and overtime hours.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {

            try
            {
                int employeeID = int.Parse(txtEmployeeID.Text);
                string employeeName = txtEmployeeName.Text;
                string position = txtPosition.Text;

                decimal basicSalary = decimal.Parse(txtBasicSalary.Text);
                decimal hoursWorked = decimal.Parse(txtHoursWorked.Text);
                decimal overtimeHours = decimal.Parse(txtOvertimeHours.Text);
                decimal deductions = decimal.Parse(txtDeductions.Text);

                decimal overtimePay = decimal.Parse(txtOvertimePay.Text);
                decimal grossPay = decimal.Parse(txtGrossPay.Text);
                decimal netPay = decimal.Parse(txtNetPay.Text);

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    string query = @"
                INSERT INTO Employees
                (
                    EmployeeID,
                    EmployeeName,
                    Position,
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
                    @BasicSalary,
                    @HoursWorked,
                    @OvertimeHours,
                    @Deductions,
                    @OvertimePay,
                    @GrossPay,
                    @NetPay
                )";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeID", employeeID);
                        command.Parameters.AddWithValue("@EmployeeName", employeeName);
                        command.Parameters.AddWithValue("@Position", position);
                        command.Parameters.AddWithValue("@BasicSalary", basicSalary);
                        command.Parameters.AddWithValue("@HoursWorked", hoursWorked);
                        command.Parameters.AddWithValue("@OvertimeHours", overtimeHours);
                        command.Parameters.AddWithValue("@Deductions", deductions);
                        command.Parameters.AddWithValue("@OvertimePay", overtimePay);
                        command.Parameters.AddWithValue("@GrossPay", grossPay);
                        command.Parameters.AddWithValue("@NetPay", netPay);

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
                int employeeID = int.Parse(txtEmployeeID.Text);
                string employeeName = txtEmployeeName.Text;
                string position = txtPosition.Text;

                decimal basicSalary = decimal.Parse(txtBasicSalary.Text);
                decimal hoursWorked = decimal.Parse(txtHoursWorked.Text);
                decimal overtimeHours = decimal.Parse(txtOvertimeHours.Text);
                decimal deductions = decimal.Parse(txtDeductions.Text);

                decimal overtimePay = decimal.Parse(txtOvertimePay.Text);
                decimal grossPay = decimal.Parse(txtGrossPay.Text);
                decimal netPay = decimal.Parse(txtNetPay.Text);

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    string query = @"
                UPDATE Employees
                SET
                    EmployeeName = @EmployeeName,
                    Position = @Position,
                    BasicSalary = @BasicSalary,
                    HoursWorked = @HoursWorked,
                    OvertimeHours = @OvertimeHours,
                    Deductions = @Deductions,
                    OvertimePay = @OvertimePay,
                    GrossPay = @GrossPay,
                    NetPay = @NetPay
                WHERE EmployeeID = @EmployeeID";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeID", employeeID);
                        command.Parameters.AddWithValue("@EmployeeName", employeeName);
                        command.Parameters.AddWithValue("@Position", position);
                        command.Parameters.AddWithValue("@BasicSalary", basicSalary);
                        command.Parameters.AddWithValue("@HoursWorked", hoursWorked);
                        command.Parameters.AddWithValue("@OvertimeHours", overtimeHours);
                        command.Parameters.AddWithValue("@Deductions", deductions);
                        command.Parameters.AddWithValue("@OvertimePay", overtimePay);
                        command.Parameters.AddWithValue("@GrossPay", grossPay);
                        command.Parameters.AddWithValue("@NetPay", netPay);

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

            txtEmployeeID.Text = row.Cells[0].Value?.ToString();
            txtEmployeeName.Text = row.Cells[1].Value?.ToString();
            txtPosition.Text = row.Cells[2].Value?.ToString();
            txtBasicSalary.Text = row.Cells[3].Value?.ToString();
            txtHoursWorked.Text = row.Cells[4].Value?.ToString();
            txtOvertimeHours.Text = row.Cells[5].Value?.ToString();
            txtDeductions.Text = row.Cells[6].Value?.ToString();
            txtOvertimePay.Text = row.Cells[7].Value?.ToString();
            txtGrossPay.Text = row.Cells[8].Value?.ToString();
            txtNetPay.Text = row.Cells[9].Value?.ToString();

            decimal basicSalary = decimal.Parse(txtBasicSalary.Text);
            decimal hoursWorked = decimal.Parse(txtHoursWorked.Text);

            decimal regularPay = (basicSalary / 160m) * hoursWorked;

            txtRegularPay.Text = regularPay.ToString("N2");
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
                        "Are you sure you want to delete this employee?",
                        "Confirm Delete",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);


                if (result != DialogResult.Yes)
                    return;


                using (MySqlConnection connection =
                    new MySqlConnection(connectionString))
                {
                    string query =
                        "DELETE FROM Employees WHERE EmployeeID = @EmployeeID";


                    using (MySqlCommand command =
                        new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue(
                            "@EmployeeID", employeeID);


                        connection.Open();


                        int rowsAffected =
                            command.ExecuteNonQuery();


                        if (rowsAffected == 0)
                        {
                            MessageBox.Show(
                                "Employee not found.",
                                "Delete",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);

                            return;
                        }
                    }
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
        private void ClearFields()
        {
            txtEmployeeID.Clear();
            txtEmployeeName.Clear();
            txtPosition.Clear();
            txtBasicSalary.Clear();
            txtHoursWorked.Clear();
            txtOvertimeHours.Clear();
            txtDeductions.Clear();
            txtRegularPay.Clear();
            txtOvertimePay.Clear();
            txtGrossPay.Clear();
            txtNetPay.Clear();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            ClearFields();
        }
    }
}