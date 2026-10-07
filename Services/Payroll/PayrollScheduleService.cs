using System;
using System.Collections.Generic;
using MySqlConnector;

namespace PAYROLL
{
    public sealed class PayrollReleasePolicy
    {
        public int PeriodDays { get; init; }
        public DateTime EffectiveFrom { get; init; }
    }

    public sealed class PayrollReleaseResult
    {
        public int PeriodsChecked { get; init; }
        public int PayslipsCreated { get; set; }
        public List<string> Errors { get; } = new();
    }

    internal sealed class PayrollEmployee
    {
        public int EmployeeId;
        public string Name = "";
        public decimal HourlyRate;
    }

    internal sealed class PayrollPeriod
    {
        public DateTime Start;
        public DateTime End;
    }

    /// <summary>
    /// Stores the company's release cadence and creates due payslips once per employee/cutoff.
    /// The desktop app invokes this at management login and periodically while it is open.
    /// </summary>
    public static class PayrollScheduleService
    {
        public static PayrollReleasePolicy GetPolicy()
        {
            EnsureSchema();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"SELECT PeriodDays, EffectiveFrom
                FROM CompanyPayrollPolicy WHERE PolicyID=1", con);
            con.Open();
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return new PayrollReleasePolicy { PeriodDays = 15, EffectiveFrom = DateTime.Today };
            return new PayrollReleasePolicy
            {
                PeriodDays = Convert.ToInt32(reader["PeriodDays"]),
                EffectiveFrom = Convert.ToDateTime(reader["EffectiveFrom"]).Date
            };
        }

        public static void SavePolicy(int periodDays)
        {
            if (periodDays is not (7 or 15 or 30))
                throw new ArgumentOutOfRangeException(nameof(periodDays), "Choose 7, 15, or 30 days.");

            EnsureSchema();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            using var cmd = new MySqlCommand(@"UPDATE CompanyPayrollPolicy
                SET EffectiveFrom=IF(PeriodDays=@PeriodDays, EffectiveFrom, CURRENT_DATE()),
                    PeriodDays=@PeriodDays, UpdatedAt=CURRENT_TIMESTAMP
                WHERE PolicyID=1", con);
            cmd.Parameters.AddWithValue("@PeriodDays", periodDays);
            cmd.ExecuteNonQuery();
        }

        public static PayrollReleaseResult ReleaseDuePayslips(DateTime? asOfDate = null)
        {
            EnsureSchema();
            var policy = GetPolicy();
            DateTime today = (asOfDate ?? DateTime.Today).Date;
            List<PayrollPeriod> periods = GetCompletedPeriods(policy, today);
            var result = new PayrollReleaseResult { PeriodsChecked = periods.Count };
            if (periods.Count == 0) return result;

            CompanyService.EnsureDepartmentSchema();
            CompanyService.EnsureBenefitsSchema();
            List<PayrollEmployee> employees = LoadActiveEmployees();
            List<CompanyBenefit> benefits = CompanyService.ListBenefits();

            foreach (var period in periods)
            foreach (var employee in employees)
            {
                try
                {
                    if (IsHandled(employee.EmployeeId, period)) continue;

                    int? existingPayslipId = FindPayslip(employee.EmployeeId, period);
                    if (existingPayslipId.HasValue)
                    {
                        MarkHandled(employee.EmployeeId, period, existingPayslipId.Value, "AlreadyExists");
                        continue;
                    }

                    var summary = AttendanceService.GetSummary(employee.EmployeeId, period.Start, period.End);
                    decimal paidLeaveDays = LeaveService.GetPaidWorkdays(employee.EmployeeId, period.Start, period.End);
                    decimal daysWorked = summary.RegularHours / 8m;
                    if (daysWorked <= 0 && paidLeaveDays <= 0) continue;

                    var payslip = BuildPayslip(employee, period, summary, paidLeaveDays, benefits, policy.PeriodDays);
                    int createdId;
                    using (var con = new MySqlConnection(AppConfig.ConnectionString))
                    {
                        con.Open();
                        using var transaction = con.BeginTransaction();
                        using (var claim = new MySqlCommand(@"INSERT IGNORE INTO PayrollAutoReleaseItems
                            (EmployeeID, CutoffStart, CutoffEnd, Status)
                            VALUES (@EmployeeID, @CutoffStart, @CutoffEnd, 'Processing')", con, transaction))
                        {
                            claim.Parameters.AddWithValue("@EmployeeID", employee.EmployeeId);
                            claim.Parameters.AddWithValue("@CutoffStart", period.Start);
                            claim.Parameters.AddWithValue("@CutoffEnd", period.End);
                            if (claim.ExecuteNonQuery() == 0)
                            {
                                transaction.Commit();
                                continue;
                            }
                        }

                        existingPayslipId = FindPayslip(con, transaction, employee.EmployeeId, period);
                        if (existingPayslipId.HasValue)
                        {
                            UpdateReleaseItem(con, transaction, employee.EmployeeId, period,
                                existingPayslipId.Value, "AlreadyExists");
                            transaction.Commit();
                            continue;
                        }

                        createdId = PayslipService.CreateInTransaction(con, transaction, payslip);
                        UpdateReleaseItem(con, transaction, employee.EmployeeId, period, createdId, "Released");
                        transaction.Commit();
                    }

                    result.PayslipsCreated++;
                    string sourceKey = $"Payslip:{period.Start:yyyyMMdd}:{period.End:yyyyMMdd}";
                    try
                    {
                        EmployeeNotificationService.Add(employee.EmployeeId, "PayslipReleased", sourceKey,
                            "New payslip available",
                            $"Your payslip for {period.Start:MMM d}–{period.End:MMM d, yyyy} is ready in My Payslips.");
                    }
                    catch { /* A notification outage should not undo an already released payslip. */ }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{employee.Name} ({period.Start:yyyy-MM-dd} to {period.End:yyyy-MM-dd}): {ex.Message}");
                }
            }
            return result;
        }

        private static Payslip BuildPayslip(PayrollEmployee employee, PayrollPeriod period,
            AttendanceSummary summary, decimal paidLeaveDays, List<CompanyBenefit> benefits, int periodDays)
        {
            decimal hourlyRate = employee.HourlyRate;
            decimal dailyRate = hourlyRate * 8m;
            decimal monthlySalary = DeductionsCalculator.MonthlySalaryFromHourlyRate(hourlyRate);
            var contributions = DeductionsCalculator.Compute(monthlySalary);
            decimal contributionShare = periodDays switch
            {
                7 => 12m / 52m,
                15 => 0.5m,
                _ => 1m
            };

            var payslip = new Payslip
            {
                EmployeeId = employee.EmployeeId,
                EmployeeName = employee.Name,
                CutoffStart = period.Start,
                CutoffEnd = period.End,
                NoOfDays = summary.RegularHours / 8m,
                HourlyRate = hourlyRate,
                BasicPay = hourlyRate * summary.RegularHours,
                TotalOtPay = summary.ApprovedOvertimeHours * hourlyRate * 1.25m,
                RegHolPayPrem = summary.RegularHolidayDays * dailyRate,
                SpHolPayPrem = summary.SpecialHolidayDays * dailyRate * 0.3m,
                LeaveWithPay = paidLeaveDays * dailyRate,
                LateUtOb = summary.OverbreakMinutes * hourlyRate / 60m,
                SssContribution = Math.Round(contributions.sss * contributionShare, 2),
                PhilHealthContribution = Math.Round(contributions.philHealth * contributionShare, 2),
                HmdfContribution = Math.Round(contributions.pagIbig * contributionShare, 2),
                Absences = 0m,
                Loans = 0m
            };
            foreach (var benefit in benefits)
                payslip.Benefits.Add(new PayslipBenefitItem
                {
                    BenefitName = benefit.BenefitName,
                    Amount = benefit.DefaultAmount
                });
            return payslip;
        }

        private static List<PayrollEmployee> LoadActiveEmployees()
        {
            var employees = new List<PayrollEmployee>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"SELECT EmployeeID, EmployeeName, HourlyRate
                FROM Employees WHERE IsActive=1 ORDER BY EmployeeID", con);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) employees.Add(new PayrollEmployee
            {
                EmployeeId = Convert.ToInt32(reader["EmployeeID"]),
                Name = reader["EmployeeName"].ToString() ?? "Employee",
                HourlyRate = Convert.ToDecimal(reader["HourlyRate"])
            });
            return employees;
        }

        private static List<PayrollPeriod> GetCompletedPeriods(PayrollReleasePolicy policy, DateTime today)
        {
            var periods = new List<PayrollPeriod>();
            DateTime effective = policy.EffectiveFrom.Date;
            PayrollPeriod current = FirstPeriodOnOrAfter(policy.PeriodDays, effective);
            while (current.End < today)
            {
                periods.Add(current);
                current = NextPeriod(policy.PeriodDays, current);
            }
            return periods;
        }

        private static PayrollPeriod FirstPeriodOnOrAfter(int periodDays, DateTime effective)
        {
            PayrollPeriod period;
            if (periodDays == 7)
            {
                int daysSinceMonday = ((int)effective.DayOfWeek + 6) % 7;
                DateTime monday = effective.AddDays(-daysSinceMonday);
                if (monday < effective) monday = monday.AddDays(7);
                period = new PayrollPeriod { Start = monday, End = monday.AddDays(6) };
            }
            else if (periodDays == 15)
            {
                DateTime first = new(effective.Year, effective.Month, effective.Day <= 15 ? 1 : 16);
                int lastDay = effective.Day <= 15 ? 15 : DateTime.DaysInMonth(effective.Year, effective.Month);
                period = new PayrollPeriod { Start = first, End = new DateTime(effective.Year, effective.Month, lastDay) };
                if (period.Start < effective) period = NextPeriod(15, period);
            }
            else
            {
                DateTime first = new(effective.Year, effective.Month, 1);
                period = new PayrollPeriod
                {
                    Start = first,
                    End = first.AddMonths(1).AddDays(-1)
                };
                if (period.Start < effective) period = NextPeriod(30, period);
            }
            return period;
        }

        private static PayrollPeriod NextPeriod(int periodDays, PayrollPeriod current)
        {
            DateTime nextStart = current.End.AddDays(1);
            if (periodDays == 7) return new PayrollPeriod { Start = nextStart, End = nextStart.AddDays(6) };
            if (periodDays == 15)
            {
                int lastDay = DateTime.DaysInMonth(nextStart.Year, nextStart.Month);
                DateTime end = nextStart.Day == 1
                    ? new DateTime(nextStart.Year, nextStart.Month, 15)
                    : new DateTime(nextStart.Year, nextStart.Month, lastDay);
                return new PayrollPeriod { Start = nextStart, End = end };
            }
            DateTime monthStart = new(nextStart.Year, nextStart.Month, 1);
            return new PayrollPeriod { Start = monthStart, End = monthStart.AddMonths(1).AddDays(-1) };
        }

        private static bool IsHandled(int employeeId, PayrollPeriod period)
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"SELECT COUNT(*) FROM PayrollAutoReleaseItems
                WHERE EmployeeID=@EmployeeID AND CutoffStart=@Start AND CutoffEnd=@End", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Start", period.Start);
            cmd.Parameters.AddWithValue("@End", period.End);
            con.Open();
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static int? FindPayslip(int employeeId, PayrollPeriod period)
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            return FindPayslip(con, null, employeeId, period);
        }

        private static int? FindPayslip(MySqlConnection con, MySqlTransaction? transaction, int employeeId, PayrollPeriod period)
        {
            using var cmd = new MySqlCommand(@"SELECT PayslipID FROM Payslips
                WHERE EmployeeID=@EmployeeID AND CutoffStart=@Start AND CutoffEnd=@End
                ORDER BY PayslipID LIMIT 1", con, transaction);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Start", period.Start);
            cmd.Parameters.AddWithValue("@End", period.End);
            object? value = cmd.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToInt32(value);
        }

        private static void MarkHandled(int employeeId, PayrollPeriod period, int payslipId, string status)
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            using var transaction = con.BeginTransaction();
            using var cmd = new MySqlCommand(@"INSERT IGNORE INTO PayrollAutoReleaseItems
                (EmployeeID, CutoffStart, CutoffEnd, PayslipID, Status)
                VALUES (@EmployeeID, @Start, @End, @PayslipID, @Status)", con, transaction);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Start", period.Start);
            cmd.Parameters.AddWithValue("@End", period.End);
            cmd.Parameters.AddWithValue("@PayslipID", payslipId);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }

        private static void UpdateReleaseItem(MySqlConnection con, MySqlTransaction transaction,
            int employeeId, PayrollPeriod period, int payslipId, string status)
        {
            using var cmd = new MySqlCommand(@"UPDATE PayrollAutoReleaseItems
                SET PayslipID=@PayslipID, Status=@Status
                WHERE EmployeeID=@EmployeeID AND CutoffStart=@Start AND CutoffEnd=@End", con, transaction);
            cmd.Parameters.AddWithValue("@PayslipID", payslipId);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Start", period.Start);
            cmd.Parameters.AddWithValue("@End", period.End);
            cmd.ExecuteNonQuery();
        }

        private static void EnsureSchema()
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            using (var policy = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS CompanyPayrollPolicy (
                PolicyID TINYINT NOT NULL PRIMARY KEY,
                PeriodDays TINYINT NOT NULL DEFAULT 15,
                EffectiveFrom DATE NOT NULL,
                UpdatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            )", con)) policy.ExecuteNonQuery();
            using (var seed = new MySqlCommand(@"INSERT IGNORE INTO CompanyPayrollPolicy
                (PolicyID, PeriodDays, EffectiveFrom) VALUES (1, 15, CURRENT_DATE())", con)) seed.ExecuteNonQuery();
            using (var items = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS PayrollAutoReleaseItems (
                ReleaseItemID BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                EmployeeID INT NOT NULL,
                CutoffStart DATE NOT NULL,
                CutoffEnd DATE NOT NULL,
                PayslipID INT NULL,
                Status VARCHAR(24) NOT NULL,
                CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY UX_PayrollAutoReleaseItem (EmployeeID, CutoffStart, CutoffEnd)
            )", con)) items.ExecuteNonQuery();
        }
    }
}
