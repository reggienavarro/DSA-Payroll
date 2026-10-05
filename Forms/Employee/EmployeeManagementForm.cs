using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using MySqlConnector;
using PAYROLL.UI;

namespace PAYROLL
{
    public partial class EmployeeManagementForm : Form
    {
        private readonly string connectionString = AppConfig.ConnectionString;
        private ComboBox departmentCombo = null!;
        private DateTimePicker hireDatePicker = null!;
        private CheckBox activeCheck = null!;
        private TextBox searchBox = null!;
        private DataView employeeView = null!;
        private int? selectedEmployeeId;

        public EmployeeManagementForm()
        {
            InitializeComponent();
            CompanyService.EnsureDepartmentSchema();
            BuildEmployeeManagementUi();
            LoadDepartments();
            LoadEmployees();
        }

        private void BuildEmployeeManagementUi()
        {
            SuspendLayout();
            Controls.Clear();
            BackColor = Theme.Bg;
            Padding = new Padding(18);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Bg,
                Margin = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));

            var formCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20),
                Margin = new Padding(0, 0, 16, 0)
            };
            var formFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.White,
                Padding = new Padding(0, 0, 6, 0)
            };
            formCard.Controls.Add(formFlow);

            AddText(formFlow, "Add or edit employee", Theme.H2, Theme.TextDark, 32);
            AddText(formFlow, "Employee information", Theme.Small, Theme.TextGray, 26);
            AddField(formFlow, "Employee ID", txtEmployeeID);
            AddField(formFlow, "Full name", txtEmployeeName);
            AddField(formFlow, "Position", txtPosition);
            AddField(formFlow, "Hourly rate", txtBasicSalary);

            departmentCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 310,
                Height = 32,
                Font = Theme.Body,
                Margin = new Padding(0, 2, 0, 10)
            };
            AddField(formFlow, "Department", departmentCombo);

            hireDatePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                ShowCheckBox = true,
                Width = 310,
                Height = 32,
                Font = Theme.Body,
                Value = DateTime.Today,
                Margin = new Padding(0, 2, 0, 10)
            };
            AddField(formFlow, "Date hired", hireDatePicker);

            activeCheck = new CheckBox
            {
                Text = "Active employee (may register an account)",
                Checked = true,
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.TextDark,
                BackColor = Color.White,
                Margin = new Padding(0, 8, 0, 14)
            };
            formFlow.Controls.Add(activeCheck);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.White,
                Margin = Padding.Empty
            };
            StyleActionButton(btnAdd, "Add employee", Theme.Accent);
            StyleActionButton(btnUpdate, "Save changes", Theme.Navy);
            StyleActionButton(btnClear, "Clear", Color.FromArgb(238, 241, 245));
            btnDelete.Visible = false;
            actions.Controls.Add(btnAdd);
            actions.Controls.Add(btnUpdate);
            actions.Controls.Add(btnClear);
            formFlow.Controls.Add(actions);

            var listCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18) };
            var listLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White,
                Margin = Padding.Empty
            };
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            listCard.Controls.Add(listLayout);

            var listTitle = new Label
            {
                Text = "Employee list",
                Dock = DockStyle.Fill,
                Font = Theme.H2,
                ForeColor = Theme.TextDark,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.White
            };
            listLayout.Controls.Add(listTitle, 0, 0);

            searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = Theme.Body,
                PlaceholderText = "Search by employee name, ID, department, or position...",
                Margin = new Padding(0, 6, 0, 8)
            };
            searchBox.TextChanged += (s, e) => ApplyEmployeeSearch();
            listLayout.Controls.Add(searchBox, 0, 1);

            dgvPayroll.Dock = DockStyle.Fill;
            dgvPayroll.ReadOnly = true;
            dgvPayroll.AutoGenerateColumns = true;
            dgvPayroll.CellClick -= dgvPayroll_CellClick;
            dgvPayroll.CellClick += EmployeeGrid_CellClick;
            dgvPayroll.CellDoubleClick -= dgvPayroll_CellClick;
            GridStyle.Apply(dgvPayroll);
            dgvPayroll.ScrollBars = ScrollBars.Vertical;
            dgvPayroll.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvPayroll.GridColor = Color.FromArgb(220, 224, 231);
            dgvPayroll.RowTemplate.Height = 44;
            dgvPayroll.ColumnHeadersHeight = 42;
            dgvPayroll.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvPayroll.Columns.Clear();
            dgvPayroll.AutoGenerateColumns = false;
            dgvPayroll.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmployeeName", HeaderText = "Employee", Name = "EmployeeName", FillWeight = 125 });
            dgvPayroll.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Position", HeaderText = "Position", Name = "Position", FillWeight = 75 });
            dgvPayroll.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DepartmentName", HeaderText = "Department", Name = "DepartmentName", FillWeight = 85 });
            dgvPayroll.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "HireDate", HeaderText = "Date hired", Name = "HireDate", FillWeight = 75 });
            dgvPayroll.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmploymentStatus", HeaderText = "Status", Name = "EmploymentStatus", FillWeight = 65 });
            dgvPayroll.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AccountStatus", HeaderText = "Account", Name = "AccountStatus", FillWeight = 85 });
            dgvPayroll.Columns.Add(new DataGridViewButtonColumn
            {
                HeaderText = "",
                Name = "Actions",
                Text = "⋯",
                UseColumnTextForButtonValue = true,
                FillWeight = 38,
                FlatStyle = FlatStyle.Flat,
                MinimumWidth = 42
            });
            dgvPayroll.CellPainting += PaintEmployeeStatusCell;
            dgvPayroll.CellPainting += PaintActionCell;
            dgvPayroll.CellContentClick += EmployeeGrid_ActionClick;
            dgvPayroll.DataError += (s, e) => e.ThrowException = false;
            listLayout.Controls.Add(dgvPayroll, 0, 2);

            layout.Controls.Add(formCard, 0, 0);
            layout.Controls.Add(listCard, 1, 0);
            Controls.Add(layout);
            ResumeLayout(true);

            TextBoxStyle(txtEmployeeID);
            TextBoxStyle(txtEmployeeName);
            TextBoxStyle(txtPosition);
            TextBoxStyle(txtBasicSalary);
            txtEmployeeID.ReadOnly = false;
            txtBasicSalary.ReadOnly = false;
        }

        private static void AddText(Control parent, string text, Font font, Color color, int height)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                AutoSize = false,
                Width = 330,
                Height = height,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.White,
                Margin = new Padding(0)
            });
        }

        private static void AddField(FlowLayoutPanel parent, string caption, Control input)
        {
            var group = new Panel
            {
                Width = 330,
                Height = 68,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 2)
            };
            group.Controls.Add(new Label
            {
                Text = caption,
                Location = new Point(0, 0),
                Size = new Size(320, 22),
                Font = Theme.SmallBold,
                ForeColor = Theme.TextGray,
                BackColor = Color.White
            });
            input.Location = new Point(0, 25);
            input.Width = 310;
            input.Height = 32;
            group.Controls.Add(input);
            parent.Controls.Add(group);
        }

        private static void TextBoxStyle(TextBox box)
        {
            box.Font = Theme.Body;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Margin = new Padding(0, 2, 0, 10);
        }

        private static void StyleActionButton(Button button, string text, Color color)
        {
            button.Text = text;
            button.Size = new Size(132, 38);
            button.Margin = new Padding(0, 0, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = color == Color.FromArgb(238, 241, 245) ? Theme.TextDark : Color.White;
            button.Font = Theme.SmallBold;
        }

        private void LoadDepartments()
        {
            var table = new DataTable();
            using var con = new MySqlConnection(connectionString);
            using var da = new MySqlDataAdapter("SELECT DepartmentID, DepartmentName FROM Departments ORDER BY DepartmentName", con);
            da.Fill(table);
            var unassigned = table.NewRow();
            unassigned["DepartmentID"] = DBNull.Value;
            unassigned["DepartmentName"] = "Unassigned";
            table.Rows.InsertAt(unassigned, 0);
            departmentCombo.DisplayMember = "DepartmentName";
            departmentCombo.ValueMember = "DepartmentID";
            departmentCombo.DataSource = table;
            departmentCombo.SelectedIndex = 0;
        }

        private void LoadEmployees()
        {
            try
            {
                using var connection = new MySqlConnection(connectionString);
                using var adapter = new MySqlDataAdapter(@"
                    SELECT e.EmployeeID, e.EmployeeName, e.Position, e.HourlyRate, e.DepartmentID,
                           COALESCE(d.DepartmentName, 'Unassigned') AS DepartmentName,
                           e.HireDate, e.IsActive,
                           CASE WHEN e.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS EmploymentStatus,
                           CASE WHEN EXISTS (SELECT 1 FROM Accounts a WHERE a.EmployeeID = e.EmployeeID)
                                THEN 'Registered' ELSE 'No account yet' END AS AccountStatus
                    FROM Employees e
                    LEFT JOIN Departments d ON d.DepartmentID = e.DepartmentID
                    ORDER BY e.EmployeeName", connection);
                var table = new DataTable();
                adapter.Fill(table);
                table.Columns.Add("HireDateDisplay", typeof(string));
                foreach (DataRow row in table.Rows)
                    row["HireDateDisplay"] = row["HireDate"] is DBNull ? "—" : Convert.ToDateTime(row["HireDate"]).ToString("MMM d, yyyy");
                dgvPayroll.DataSource = null;
                employeeView = table.DefaultView;
                dgvPayroll.DataSource = employeeView;
                if (dgvPayroll.Columns["HireDate"] is DataGridViewColumn hireDateColumn)
                    hireDateColumn.DataPropertyName = "HireDateDisplay";
                ApplyEmployeeSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load employees: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyEmployeeSearch()
        {
            if (employeeView == null) return;
            string value = searchBox.Text.Trim().Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
            employeeView.RowFilter = value.Length == 0 ? string.Empty :
                $"Convert(EmployeeID, 'System.String') LIKE '%{value}%' OR EmployeeName LIKE '%{value}%' OR " +
                $"Position LIKE '%{value}%' OR DepartmentName LIKE '%{value}%' OR EmploymentStatus LIKE '%{value}%' OR AccountStatus LIKE '%{value}%'";
        }

        private void EmployeeGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvPayroll.Rows[e.RowIndex].DataBoundItem is not DataRowView row) return;
            if (dgvPayroll.Columns[e.ColumnIndex].Name == "Actions") return;
            PopulateEmployeeForm(row);
        }

        private void EmployeeGrid_ActionClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvPayroll.Columns[e.ColumnIndex].Name != "Actions") return;
            if (dgvPayroll.Rows[e.RowIndex].DataBoundItem is not DataRowView row) return;
            int employeeId = Convert.ToInt32(row["EmployeeID"]);
            var menu = new ContextMenuStrip();
            menu.Items.Add("Generate payslip", null, (s, args) => GeneratePayslip(employeeId));
            menu.Items.Add("Delete employee", null, (s, args) => DeleteEmployee(employeeId));
            var cell = dgvPayroll.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            menu.Show(dgvPayroll, new Point(cell.Right, cell.Bottom));
        }

        private static void PaintActionCell(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0 ||
                grid.Columns[e.ColumnIndex].Name != "Actions") return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground |
                DataGridViewPaintParts.ContentBackground);

            var button = new Rectangle(
                e.CellBounds.X + (e.CellBounds.Width - 30) / 2,
                e.CellBounds.Y + (e.CellBounds.Height - 26) / 2,
                30, 26);
            using var path = Gfx.Round(button, 5);
            using var fill = new SolidBrush(Color.FromArgb(248, 250, 252));
            using var border = new Pen(Color.FromArgb(214, 219, 227));
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
            using (var rowLine = new Pen(Color.FromArgb(220, 224, 231)))
                e.Graphics.DrawLine(rowLine, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                    e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
            TextRenderer.DrawText(e.Graphics, "•••", Theme.SmallBold, button, Theme.TextGray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            e.Handled = true;
        }

        private static void PaintEmployeeStatusCell(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string column = grid.Columns[e.ColumnIndex].Name;
            if (column != "EmploymentStatus") return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground |
                DataGridViewPaintParts.ContentBackground);
            string text = e.FormattedValue?.ToString() ?? string.Empty;
            if (text.Length > 0)
            {
                var (background, foreground) = GridStyle.BadgeColors(text);
                var size = TextRenderer.MeasureText(text, Theme.SmallBold);
                var badge = new Rectangle(e.CellBounds.X + 10,
                    e.CellBounds.Y + (e.CellBounds.Height - 20) / 2,
                    Math.Max(size.Width + 18, 46), 20);
                using var path = Gfx.Round(badge, 9);
                using var fill = new SolidBrush(background);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillPath(fill, path);
                TextRenderer.DrawText(e.Graphics, text, Theme.SmallBold, badge, foreground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            using var rowLine = new Pen(Color.FromArgb(220, 224, 231));
            e.Graphics.DrawLine(rowLine, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
            e.Handled = true;
        }

        private void PopulateEmployeeForm(DataRowView row)
        {
            selectedEmployeeId = Convert.ToInt32(row["EmployeeID"]);
            txtEmployeeID.Text = row["EmployeeID"].ToString();
            txtEmployeeName.Text = row["EmployeeName"].ToString();
            txtPosition.Text = row["Position"].ToString();
            txtBasicSalary.Text = row["HourlyRate"].ToString();
            hireDatePicker.Value = row["HireDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(row["HireDate"]);
            hireDatePicker.Checked = row["HireDate"] is not DBNull;
            activeCheck.Checked = Convert.ToBoolean(row["IsActive"]);
            if (row["DepartmentID"] is DBNull)
                departmentCombo.SelectedIndex = 0;
            else
                departmentCombo.SelectedValue = row["DepartmentID"];
            btnAdd.Enabled = false;
            btnUpdate.Enabled = true;
        }

        private void btnAdd_Click(object? sender, EventArgs e)
        {
            if (!TryGetEmployeeProfile(out int employeeId, out string employeeName, out string position, out decimal hourlyRate))
            {
                MessageBox.Show("Enter a numeric employee ID, employee name, position, and positive hourly rate.",
                    "Check employee information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using var connection = new MySqlConnection(connectionString);
                using var command = new MySqlCommand(@"
                    INSERT INTO Employees
                    (EmployeeID, EmployeeName, Position, DepartmentID, HireDate, IsActive, HourlyRate,
                     BasicSalary, HoursWorked, OvertimeHours, Deductions, OvertimePay, GrossPay, NetPay)
                    VALUES (@ID, @Name, @Position, @Department, @HireDate, @IsActive, @Rate, 0, 0, 0, 0, 0, 0, 0)", connection);
                AddEmployeeParameters(command, employeeId, employeeName, position, hourlyRate);
                connection.Open();
                command.ExecuteNonQuery();
                LoadEmployees();
                ClearFields();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Number == 1062 ? "That Employee ID already exists." : "Could not add employee: " + ex.Message,
                    "Employee", MessageBoxButtons.OK, ex.Number == 1062 ? MessageBoxIcon.Warning : MessageBoxIcon.Error);
            }
        }

        private void btnUpdate_Click(object? sender, EventArgs e)
        {
            if (!selectedEmployeeId.HasValue)
            {
                MessageBox.Show("Select an employee from the list first.", "No employee selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!TryGetEmployeeProfile(out int employeeId, out string employeeName, out string position, out decimal hourlyRate) || employeeId != selectedEmployeeId.Value)
            {
                MessageBox.Show("Employee ID cannot be changed. Enter a name, position, and positive hourly rate.",
                    "Check employee information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using var connection = new MySqlConnection(connectionString);
                using var command = new MySqlCommand(@"
                    UPDATE Employees SET EmployeeName=@Name, Position=@Position, DepartmentID=@Department,
                        HireDate=@HireDate, IsActive=@IsActive, HourlyRate=@Rate
                    WHERE EmployeeID=@ID", connection);
                AddEmployeeParameters(command, employeeId, employeeName, position, hourlyRate);
                connection.Open();
                command.ExecuteNonQuery();
                LoadEmployees();
                ClearFields();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Could not update employee: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddEmployeeParameters(MySqlCommand command, int employeeId, string employeeName, string position, decimal hourlyRate)
        {
            command.Parameters.AddWithValue("@ID", employeeId);
            command.Parameters.AddWithValue("@Name", employeeName);
            command.Parameters.AddWithValue("@Position", position);
            command.Parameters.AddWithValue("@Department", departmentCombo.SelectedValue is int departmentId ? departmentId : DBNull.Value);
            command.Parameters.AddWithValue("@HireDate", hireDatePicker.Checked ? hireDatePicker.Value.Date : DBNull.Value);
            command.Parameters.AddWithValue("@IsActive", activeCheck.Checked);
            command.Parameters.AddWithValue("@Rate", hourlyRate);
        }

        private bool TryGetEmployeeProfile(out int employeeId, out string employeeName, out string position, out decimal hourlyRate)
        {
            hourlyRate = 0m;
            employeeName = txtEmployeeName.Text.Trim();
            position = txtPosition.Text.Trim();
            return int.TryParse(txtEmployeeID.Text, out employeeId) && employeeName.Length > 0 && position.Length > 0 &&
                decimal.TryParse(txtBasicSalary.Text, out hourlyRate) && hourlyRate > 0;
        }

        private void GeneratePayslip(int employeeId)
        {
            using var form = new PayslipForm(employeeId);
            if (form.ShowDialog(this) == DialogResult.OK) LoadEmployees();
        }

        private void DeleteEmployee(int employeeId)
        {
            var confirm = MessageBox.Show(
                "Delete this employee? Their account, payslips, tickets, attendance, and leave records will also be permanently deleted.",
                "Confirm employee deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            try
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                using var transaction = connection.BeginTransaction();
                try
                {
                    foreach (string table in new[] { "Attendance", "LeaveRequests", "EmployeeLeaveBalances", "SalaryTickets", "Payslips", "Accounts" })
                    {
                        try
                        {
                            using var deleteChild = new MySqlCommand($"DELETE FROM {table} WHERE EmployeeID=@ID", connection, transaction);
                            deleteChild.Parameters.AddWithValue("@ID", employeeId);
                            deleteChild.ExecuteNonQuery();
                        }
                        catch (MySqlException ex) when (ex.Number == 1146) { }
                    }
                    using (var deleteEmployee = new MySqlCommand("DELETE FROM Employees WHERE EmployeeID=@ID", connection, transaction))
                    {
                        deleteEmployee.Parameters.AddWithValue("@ID", employeeId);
                        deleteEmployee.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
                if (selectedEmployeeId == employeeId) ClearFields();
                LoadEmployees();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not delete employee: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClear_Click(object? sender, EventArgs e) => ClearFields();

        private void ClearFields()
        {
            selectedEmployeeId = null;
            txtEmployeeID.Clear();
            txtEmployeeName.Clear();
            txtPosition.Clear();
            txtBasicSalary.Text = GetDefaultHourlyRate().ToString("0.00");
            hireDatePicker.Value = DateTime.Today;
            hireDatePicker.Checked = true;
            activeCheck.Checked = true;
            if (departmentCombo.Items.Count > 0) departmentCombo.SelectedIndex = 0;
            btnAdd.Enabled = true;
            btnUpdate.Enabled = false;
            txtEmployeeID.Focus();
        }

        private static decimal GetDefaultHourlyRate()
        {
            try { return CompanyService.GetPayrollDefaults().DefaultHourlyRate; }
            catch { return PayrollDefaults.DefaultHourlyRate; }
        }

        // Legacy designer event hooks are retained for compatibility with the generated form.
        private void dgvPayroll_CellClick(object? sender, DataGridViewCellEventArgs e) => EmployeeGrid_CellClick(sender, e);
        private void btnDelete_Click(object? sender, EventArgs e)
        {
            if (selectedEmployeeId.HasValue) DeleteEmployee(selectedEmployeeId.Value);
        }
        private void btnCalculate_Click(object? sender, EventArgs e) { }
        private void EmployeePayrollInput_TextChanged(object? sender, EventArgs e) { }
    }
}
