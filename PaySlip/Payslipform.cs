using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MySqlConnector;
using PAYROLL.UI;

namespace PAYROLL
{
    // Generates one itemized payslip. Each section (Earnings/Deductions/
    // Bonuses) is grouped into its own white card, matching the rest of the
    // app's visual language — the earlier version floated plain gray
    // textboxes directly on the background, which is why it looked empty.
    public class PayslipForm : Form
    {
        private readonly Dictionary<int, decimal> employeeRates = new(); // EmployeeID -> HourlyRate
        private readonly Dictionary<string, TextBox> fields = new();
        private readonly int? preselectEmployeeId;

        private ComboBox employeeCombo = null!;
        private DateTimePicker cutoffStart = null!, cutoffEnd = null!;

        private Label totalValue = null!, totalDeductionsValue = null!, netPayValue = null!,
            totalBonusValue = null!, totalReceivableValue = null!;

        public PayslipForm(int? preselectEmployeeId = null)
        {
            this.preselectEmployeeId = preselectEmployeeId;
            Text = "Generate Payslip";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(640, 560);
            Size = new Size(680, 800);
            BackColor = Theme.Bg;
            Font = Theme.Body;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
            var content = new Panel { Location = new Point(0, 0), Width = 600, BackColor = Theme.Bg };

            // Read from the Company module's configurable defaults. Falls
            // back to the original hardcoded values if that table isn't set
            // up yet, so this never hard-crashes an older database.
            PayrollDefaultsConfig defaults;
            try { defaults = CompanyService.GetPayrollDefaults(); }
            catch
            {
                defaults = new PayrollDefaultsConfig
                {
                    RiceAllowance = PayrollDefaults.RiceAllowance,
                    DailyMeal = PayrollDefaults.DailyMeal,
                    Uniform = PayrollDefaults.Uniform,
                    Laundry = PayrollDefaults.Laundry,
                    Incentives = PayrollDefaults.Incentives,
                    DefaultHourlyRate = PayrollDefaults.DefaultHourlyRate,
                };
            }

            int y = 20;
            BuildHeaderCard(content, ref y, defaults.DefaultHourlyRate);

            BuildSection(content, ref y, "EARNINGS",
                new[]
                {
                    ("riceAllowance", "Rice Allowance", defaults.RiceAllowance),
                    ("dailyMeal", "Daily Meal", defaults.DailyMeal),
                    ("uniform", "Uniform", defaults.Uniform),
                    ("laundry", "Laundry", defaults.Laundry),
                    ("totalOtPay", "Total OT Pay", 0m),
                    ("regHolPayPrem", "Regular Holidays", 0m),
                    ("spHolPayPrem", "Special Holidays", 0m),
                    ("leaveWithPay", "Leave With Pay", 0m),
                    ("adjustment", "Adjustment", 0m),
                }, out var totalRow, "TOTAL");
            totalValue = totalRow;

            BuildSection(content, ref y, "DEDUCTIONS",
                new[]
                {
                    ("lateUtOb", "Overbreak (> 1 hour)", 0m),
                    ("sss", "SSS Contribution", 0m),
                    ("philHealth", "PhilHealth Contribution", 0m),
                    ("hmdf", "HMDF Contribution", 0m),
                }, out var totalDeductionsRow, "TOTAL DEDUCTIONS");
            totalDeductionsValue = totalDeductionsRow;

            netPayValue = AddHighlightRow(content, ref y, "NET PAY");

            BuildSection(content, ref y, "BONUSES",
                new[]
                {
                    ("attendanceBonus", "Attendance Bonus", 0m),
                    ("tenureBonus", "Tenure Bonus", 0m),
                    ("oic", "OIC", 0m),
                    ("account", "Account", 0m),
                    ("incentives", "Incentives", defaults.Incentives),
                    ("internalCommission", "Internal Commission", 0m),
                }, out var totalBonusRow, "TOTAL BONUS");
            totalBonusValue = totalBonusRow;

            totalReceivableValue = AddHighlightRow(content, ref y, "TOTAL AMOUNT RECEIVABLE", big: true);

            foreach (string key in new[]
                { "noOfDays", "hourlyRate", "totalOtPay", "regHolPayPrem", "spHolPayPrem", "leaveWithPay", "lateUtOb", "sss", "philHealth", "hmdf" })
                fields[key].ReadOnly = true;

            var btnSave = new ModernButton
            {
                Text = "Save Payslip", Location = new Point(20, y + 6), Size = new Size(560, 44),
                SurroundColor = Theme.Bg
            };
            btnSave.Click += BtnSave_Click;
            content.Controls.Add(btnSave);
            y += 70;

            content.Height = y;
            scroll.Controls.Add(content);
            Controls.Add(scroll);

            LoadEmployees();
            RecomputeAll();
        }

        // ------------------------------------------------------------------
        // HEADER CARD — employee, cutoff dates, no. of days, hourly rate
        // ------------------------------------------------------------------
        private void BuildHeaderCard(Panel content, ref int y, decimal defaultHourlyRate)
        {
            var card = new RoundedPanel { Location = new Point(20, y), Size = new Size(560, 230), SurroundColor = Theme.Bg };

            var empLabel = new Label { Text = "EMPLOYEE", Font = Theme.SmallBold, ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(16, 14), BackColor = Color.White };
            employeeCombo = new ComboBox { Location = new Point(16, 34), Width = 528, DropDownStyle = ComboBoxStyle.DropDownList };
            employeeCombo.SelectedIndexChanged += (s, e) => OnEmployeeChanged();

            var cutoffLabel = new Label { Text = "CUTOFF PERIOD", Font = Theme.SmallBold, ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(16, 70), BackColor = Color.White };
            cutoffStart = new DateTimePicker { Location = new Point(16, 90), Width = 256, Format = DateTimePickerFormat.Short };
            cutoffEnd = new DateTimePicker { Location = new Point(288, 90), Width = 256, Format = DateTimePickerFormat.Short };
            DateTime today = DateTime.Today;
            int lastDay = DateTime.DaysInMonth(today.Year, today.Month);
            cutoffStart.Value = new DateTime(today.Year, today.Month, today.Day <= 15 ? 1 : 16);
            cutoffEnd.Value = new DateTime(today.Year, today.Month, today.Day <= 15 ? 15 : lastDay);
            cutoffStart.ValueChanged += (s, e) => RefreshAttendanceDefaults();
            cutoffEnd.ValueChanged += (s, e) => RefreshAttendanceDefaults();

            var daysLabel = new Label { Text = "NO. OF DAYS", Font = Theme.SmallBold, ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(16, 126), BackColor = Color.White };
            var daysBox = new TextBox { Location = new Point(16, 146), Width = 256, Text = "0", ReadOnly = true };
            daysBox.TextChanged += (s, e) => RecomputeBasicPay();
            fields["noOfDays"] = daysBox;

            var rateLabel = new Label { Text = "HOURLY RATE", Font = Theme.SmallBold, ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(288, 126), BackColor = Color.White };
            var rateBox = new TextBox { Location = new Point(288, 146), Width = 256, Text = defaultHourlyRate.ToString("0.00"), ReadOnly = true };
            rateBox.TextChanged += (s, e) => RecomputeBasicPay();
            fields["hourlyRate"] = rateBox;

            var basicPayLabel = new Label { Text = "BASIC PAY  (Hourly Rate × 8 Hours × Recorded Workdays)", Font = Theme.SmallBold, ForeColor = Theme.Accent, AutoSize = true, Location = new Point(16, 182), BackColor = Color.White };
            var basicPayBox = new TextBox
            {
                Location = new Point(16, 202), Width = 528, ReadOnly = true, Text = "0.00", TextAlign = HorizontalAlignment.Right,
                Font = Theme.BodyBold, BackColor = Theme.AccentSoft, BorderStyle = BorderStyle.FixedSingle
            };
            fields["basicPay"] = basicPayBox;

            card.Controls.Add(empLabel);
            card.Controls.Add(employeeCombo);
            card.Controls.Add(cutoffLabel);
            card.Controls.Add(cutoffStart);
            card.Controls.Add(cutoffEnd);
            card.Controls.Add(daysLabel);
            card.Controls.Add(daysBox);
            card.Controls.Add(rateLabel);
            card.Controls.Add(rateBox);
            card.Controls.Add(basicPayLabel);
            card.Controls.Add(basicPayBox);

            content.Controls.Add(card);
            y += 250;
        }

        private void LoadEmployees()
        {
            try
            {
                using var con = new MySqlConnection(AppConfig.ConnectionString);
                using var cmd = new MySqlCommand("SELECT EmployeeID, EmployeeName, HourlyRate FROM Employees ORDER BY EmployeeName", con);
                con.Open();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader["EmployeeID"]);
                    string name = reader["EmployeeName"].ToString() ?? "";
                    decimal rate = Convert.ToDecimal(reader["HourlyRate"]);
                    employeeRates[id] = rate;
                    employeeCombo.Items.Add(new EmployeeItem(id, name));
                }
                if (preselectEmployeeId.HasValue)
                {
                    foreach (EmployeeItem candidate in employeeCombo.Items)
                    {
                        if (candidate.Id == preselectEmployeeId.Value)
                        {
                            employeeCombo.SelectedItem = candidate;
                            break;
                        }
                    }
                }
                if (employeeCombo.SelectedIndex < 0 && employeeCombo.Items.Count > 0) employeeCombo.SelectedIndex = 0;

                if (employeeCombo.Items.Count == 0)
                    MessageBox.Show(
                        "No employees found. Add one in Employee Management first, then generate a payslip for them.",
                        "No Employees", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load employees: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnEmployeeChanged()
        {
            if (employeeCombo.SelectedItem is EmployeeItem item && employeeRates.TryGetValue(item.Id, out var rate))
            {
                fields["hourlyRate"].Text = rate.ToString("0.00");
                RefreshAttendanceDefaults();
            }
        }

        private void RefreshAttendanceDefaults()
        {
            if (employeeCombo?.SelectedItem is not EmployeeItem item || fields.Count == 0) return;
            if (cutoffEnd.Value.Date < cutoffStart.Value.Date) return;

            try
            {
                var summary = AttendanceService.GetSummary(item.Id, cutoffStart.Value, cutoffEnd.Value);
                decimal hourlyRate = ParseField("hourlyRate");
                decimal dailyRate = hourlyRate * 8m;
                decimal paidLeaveDays = LeaveService.GetPaidWorkdays(item.Id, cutoffStart.Value, cutoffEnd.Value);
                fields["noOfDays"].Text = (summary.RegularHours / 8m).ToString("0.##");
                fields["regHolPayPrem"].Text = (summary.RegularHolidayDays * dailyRate).ToString("0.00");
                fields["spHolPayPrem"].Text = (summary.SpecialHolidayDays * dailyRate * 0.3m).ToString("0.00");
                fields["totalOtPay"].Text = (summary.ApprovedOvertimeHours * hourlyRate * 1.25m).ToString("0.00");
                fields["leaveWithPay"].Text = (paidLeaveDays * dailyRate).ToString("0.00");
                fields["lateUtOb"].Text = (summary.OverbreakMinutes * hourlyRate / 60m).ToString("0.00");

                decimal monthlySalary = DeductionsCalculator.MonthlySalaryFromHourlyRate(hourlyRate);
                var contributions = DeductionsCalculator.Compute(monthlySalary);
                fields["sss"].Text = (contributions.sss / 2m).ToString("0.00");
                fields["philHealth"].Text = (contributions.philHealth / 2m).ToString("0.00");
                fields["hmdf"].Text = (contributions.pagIbig / 2m).ToString("0.00");
                RecomputeBasicPay();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load attendance for this cutoff: " + ex.Message,
                    "Attendance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ------------------------------------------------------------------
        // SECTION BUILDER — a titled white card holding every row for one
        // section, followed by its own bold TOTAL bar.
        // ------------------------------------------------------------------
        private void BuildSection(Panel content, ref int y, string sectionTitle,
            (string key, string label, decimal defaultValue)[] rows, out Label totalRow,
            string totalLabel)
        {
            var header = new Label
            {
                Text = sectionTitle, Font = Theme.BodyBold, ForeColor = Theme.Accent,
                AutoSize = true, Location = new Point(20, y), BackColor = Theme.Bg
            };
            content.Controls.Add(header);
            y += 26;

            int cardHeight = rows.Length * 34 + 16;
            var card = new RoundedPanel { Location = new Point(20, y), Size = new Size(560, cardHeight), SurroundColor = Theme.Bg };

            int ry = 10;
            foreach (var row in rows)
            {
                var lbl = new Label { Text = row.label, Font = Theme.Body, ForeColor = Theme.TextDark, AutoSize = true, Location = new Point(16, ry + 4), BackColor = Color.White };
                var pesoLbl = new Label { Text = "₱", Font = Theme.Body, ForeColor = Theme.TextGray, AutoSize = true, Location = new Point(420, ry + 4), BackColor = Color.White };
                var box = new TextBox
                {
                    Location = new Point(440, ry), Width = 104, TextAlign = HorizontalAlignment.Right,
                    Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle,
                    Text = row.defaultValue.ToString("0.00")
                };
                box.TextChanged += (s, e) => RecomputeAll();
                fields[row.key] = box;

                card.Controls.Add(lbl);
                card.Controls.Add(pesoLbl);
                card.Controls.Add(box);
                ry += 34;
            }
            content.Controls.Add(card);
            y += cardHeight + 10;

            totalRow = AddHighlightRow(content, ref y, totalLabel);
        }

        private Label AddHighlightRow(Panel content, ref int y, string label, bool big = false)
        {
            var bg = new Panel { Location = new Point(20, y), Size = new Size(560, big ? 48 : 36), BackColor = Theme.Navy };
            var lbl = new Label
            {
                Text = label, Font = big ? new Font("Segoe UI Semibold", 12f) : Theme.BodyBold,
                ForeColor = Color.White, AutoSize = true, Location = new Point(16, big ? 14 : 9), BackColor = Theme.Navy
            };
            var val = new Label
            {
                Text = "₱0.00", Font = big ? new Font("Segoe UI Semibold", 12f) : Theme.BodyBold,
                ForeColor = Color.White, AutoSize = true, BackColor = Theme.Navy
            };
            bg.Controls.Add(lbl);
            bg.Controls.Add(val);
            bg.Resize += (s, e) => val.Location = new Point(bg.Width - val.Width - 16, big ? 14 : 9);
            content.Controls.Add(bg);
            y += big ? 60 : 46;
            return val;
        }

        // ------------------------------------------------------------------
        // LIVE CALCULATIONS
        // ------------------------------------------------------------------
        private decimal ParseField(string key) => decimal.TryParse(fields[key].Text, out var v) ? v : 0m;

        private void RecomputeBasicPay()
        {
            decimal rate = ParseField("hourlyRate");
            decimal days = ParseField("noOfDays");
            fields["basicPay"].Text = (rate * 8m * days).ToString("0.00");
            RecomputeAll();
        }

        private void RecomputeAll()
        {
            if (totalValue == null) return; // still under construction

            decimal total = ParseField("basicPay") + ParseField("riceAllowance") +
                ParseField("dailyMeal") + ParseField("uniform") + ParseField("laundry") + ParseField("totalOtPay") +
                ParseField("regHolPayPrem") + ParseField("spHolPayPrem") + ParseField("leaveWithPay") + ParseField("adjustment");
            totalValue.Text = Theme.Money(total);

            decimal totalDeductions = ParseField("lateUtOb") + ParseField("sss") +
                ParseField("philHealth") + ParseField("hmdf");
            totalDeductionsValue.Text = Theme.Money(totalDeductions);

            decimal netPay = total - totalDeductions;
            netPayValue.Text = Theme.Money(netPay);

            decimal totalBonus = ParseField("attendanceBonus") + ParseField("tenureBonus") + ParseField("oic") +
                ParseField("account") + ParseField("incentives") + ParseField("internalCommission");
            totalBonusValue.Text = Theme.Money(totalBonus);

            totalReceivableValue.Text = Theme.Money(netPay + totalBonus);

            foreach (var l in new[] { totalValue, totalDeductionsValue, netPayValue, totalBonusValue, totalReceivableValue })
                if (l.Parent != null) l.Location = new Point(l.Parent.Width - l.Width - 16, l.Location.Y);
        }

        // ------------------------------------------------------------------
        // SAVE
        // ------------------------------------------------------------------
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (employeeCombo.SelectedItem is not EmployeeItem item)
            {
                MessageBox.Show("Select an employee first.", "Missing Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (ParseField("noOfDays") <= 0 && ParseField("leaveWithPay") <= 0)
            {
                MessageBox.Show("No worked hours or approved paid leave were found for this cutoff.",
                    "Missing Payroll Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!IsSemiMonthlyCutoff(cutoffStart.Value.Date, cutoffEnd.Value.Date))
            {
                MessageBox.Show("Use a first-half cutoff (1-15) or second-half cutoff (16-last day) within one month.",
                    "Invalid Cutoff", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var p = new Payslip
            {
                EmployeeId = item.Id,
                CutoffStart = cutoffStart.Value.Date,
                CutoffEnd = cutoffEnd.Value.Date,
                NoOfDays = ParseField("noOfDays"),
                HourlyRate = ParseField("hourlyRate"),
                BasicPay = ParseField("basicPay"),
                RiceAllowance = ParseField("riceAllowance"),
                DailyMeal = ParseField("dailyMeal"),
                Uniform = ParseField("uniform"),
                Laundry = ParseField("laundry"),
                TotalOtPay = ParseField("totalOtPay"),
                RegHolPayPrem = ParseField("regHolPayPrem"),
                SpHolPayPrem = ParseField("spHolPayPrem"),
                LeaveWithPay = ParseField("leaveWithPay"),
                Adjustment = ParseField("adjustment"),
                Absences = 0m,
                LateUtOb = ParseField("lateUtOb"),
                SssContribution = ParseField("sss"),
                PhilHealthContribution = ParseField("philHealth"),
                HmdfContribution = ParseField("hmdf"),
                Loans = 0m,
                AttendanceBonus = ParseField("attendanceBonus"),
                TenureBonus = ParseField("tenureBonus"),
                Oic = ParseField("oic"),
                Account = ParseField("account"),
                Incentives = ParseField("incentives"),
                InternalCommission = ParseField("internalCommission"),
            };

            try
            {
                PayslipService.Create(p);
                MessageBox.Show("Payslip saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save payslip: " + ex.Message, "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsSemiMonthlyCutoff(DateTime start, DateTime end)
        {
            if (start.Year != end.Year || start.Month != end.Month) return false;
            int lastDay = DateTime.DaysInMonth(end.Year, end.Month);
            return start.Day == 1 && end.Day == 15 || start.Day == 16 && end.Day == lastDay;
        }

        private class EmployeeItem
        {
            public int Id;
            public string Name;
            public EmployeeItem(int id, string name) { Id = id; Name = name; }
            public override string ToString() => $"{Id} — {Name}";
        }
    }
}