using System;
using System.Collections.Generic;
using MySqlConnector;

namespace PAYROLL
{
    public static class PayslipService
    {
        public static void Create(Payslip p)
        {
            CompanyService.EnsureDepartmentSchema();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"
                INSERT INTO Payslips
                (EmployeeID, CutoffStart, CutoffEnd, NoOfDays, HourlyRate, BasicPay,
                 NdPayPrem, RiceAllowance, DailyMeal, Uniform, Laundry, TotalOtPay,
                 RegHolPayPrem, SpHolPayPrem, LeaveWithPay, Adjustment,
                 Absences, LateUtOb, SssContribution, PhilHealthContribution, HmdfContribution, Loans,
                 AttendanceBonus, TenureBonus, Oic, Account, Incentives, InternalCommission)
                VALUES
                (@EmployeeID, @CutoffStart, @CutoffEnd, @NoOfDays, @HourlyRate, @BasicPay,
                 @NdPayPrem, @RiceAllowance, @DailyMeal, @Uniform, @Laundry, @TotalOtPay,
                 @RegHolPayPrem, @SpHolPayPrem, @LeaveWithPay, @Adjustment,
                 @Absences, @LateUtOb, @SssContribution, @PhilHealthContribution, @HmdfContribution, @Loans,
                 @AttendanceBonus, @TenureBonus, @Oic, @Account, @Incentives, @InternalCommission)", con);

            cmd.Parameters.AddWithValue("@EmployeeID", p.EmployeeId);
            cmd.Parameters.AddWithValue("@CutoffStart", p.CutoffStart);
            cmd.Parameters.AddWithValue("@CutoffEnd", p.CutoffEnd);
            cmd.Parameters.AddWithValue("@NoOfDays", p.NoOfDays);
            cmd.Parameters.AddWithValue("@HourlyRate", p.HourlyRate);
            cmd.Parameters.AddWithValue("@BasicPay", p.BasicPay);
            cmd.Parameters.AddWithValue("@NdPayPrem", p.NdPayPrem);
            cmd.Parameters.AddWithValue("@RiceAllowance", p.RiceAllowance);
            cmd.Parameters.AddWithValue("@DailyMeal", p.DailyMeal);
            cmd.Parameters.AddWithValue("@Uniform", p.Uniform);
            cmd.Parameters.AddWithValue("@Laundry", p.Laundry);
            cmd.Parameters.AddWithValue("@TotalOtPay", p.TotalOtPay);
            cmd.Parameters.AddWithValue("@RegHolPayPrem", p.RegHolPayPrem);
            cmd.Parameters.AddWithValue("@SpHolPayPrem", p.SpHolPayPrem);
            cmd.Parameters.AddWithValue("@LeaveWithPay", p.LeaveWithPay);
            cmd.Parameters.AddWithValue("@Adjustment", p.Adjustment);
            cmd.Parameters.AddWithValue("@Absences", p.Absences);
            cmd.Parameters.AddWithValue("@LateUtOb", p.LateUtOb);
            cmd.Parameters.AddWithValue("@SssContribution", p.SssContribution);
            cmd.Parameters.AddWithValue("@PhilHealthContribution", p.PhilHealthContribution);
            cmd.Parameters.AddWithValue("@HmdfContribution", p.HmdfContribution);
            cmd.Parameters.AddWithValue("@Loans", p.Loans);
            cmd.Parameters.AddWithValue("@AttendanceBonus", p.AttendanceBonus);
            cmd.Parameters.AddWithValue("@TenureBonus", p.TenureBonus);
            cmd.Parameters.AddWithValue("@Oic", p.Oic);
            cmd.Parameters.AddWithValue("@Account", p.Account);
            cmd.Parameters.AddWithValue("@Incentives", p.Incentives);
            cmd.Parameters.AddWithValue("@InternalCommission", p.InternalCommission);

            con.Open();
            cmd.ExecuteNonQuery();

            // Keep the employee's roster row current: hourly rate for next
            // time this form opens, and the pay summary fields so the
            // Overview dashboard (which reads Employees, not Payslips)
            // reflects this payslip without needing any changes itself.
            using var updateEmployee = new MySqlCommand(@"
                UPDATE Employees SET
                    HourlyRate = @Rate,
                    BasicSalary = @BasicPay,
                    GrossPay = @Total,
                    OvertimePay = @TotalOtPay,
                    Deductions = @TotalDeductions,
                    NetPay = @NetPay
                WHERE EmployeeID = @EmployeeID", con);
            updateEmployee.Parameters.AddWithValue("@Rate", p.HourlyRate);
            updateEmployee.Parameters.AddWithValue("@BasicPay", p.BasicPay);
            updateEmployee.Parameters.AddWithValue("@Total", p.Total);
            updateEmployee.Parameters.AddWithValue("@TotalOtPay", p.TotalOtPay);
            updateEmployee.Parameters.AddWithValue("@TotalDeductions", p.TotalDeductions);
            updateEmployee.Parameters.AddWithValue("@NetPay", p.NetPay);
            updateEmployee.Parameters.AddWithValue("@EmployeeID", p.EmployeeId);
            updateEmployee.ExecuteNonQuery();
        }

        public static List<Payslip> LoadAll()
        {
            CompanyService.EnsureDepartmentSchema();
            var list = new List<Payslip>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"
                SELECT ps.*, e.EmployeeName,
                       COALESCE(d.DepartmentName, 'Unassigned') AS DepartmentName
                FROM Payslips ps
                JOIN Employees e ON e.EmployeeID = ps.EmployeeID
                LEFT JOIN Departments d ON d.DepartmentID = e.DepartmentID
                WHERE ps.CreatedAt >= DATE_SUB(NOW(), INTERVAL 30 DAY)
                ORDER BY COALESCE(d.DepartmentName, 'Unassigned'), e.EmployeeName, ps.CutoffEnd DESC", con);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public static List<Payslip> LoadForEmployee(int employeeId)
        {
            var list = new List<Payslip>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"
                SELECT ps.*, e.EmployeeName,
                       COALESCE(d.DepartmentName, 'Unassigned') AS DepartmentName
                FROM Payslips ps
                JOIN Employees e ON e.EmployeeID = ps.EmployeeID
                LEFT JOIN Departments d ON d.DepartmentID = e.DepartmentID
                WHERE ps.EmployeeID = @EmployeeID
                ORDER BY ps.CreatedAt DESC", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public static List<Payslip> LoadRecentForEmployee(int employeeId)
        {
            var list = new List<Payslip>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"
                SELECT ps.*, e.EmployeeName,
                       COALESCE(d.DepartmentName, 'Unassigned') AS DepartmentName
                FROM Payslips ps
                JOIN Employees e ON e.EmployeeID = ps.EmployeeID
                LEFT JOIN Departments d ON d.DepartmentID = e.DepartmentID
                WHERE ps.EmployeeID = @EmployeeID
                  AND ps.CreatedAt >= DATE_SUB(NOW(), INTERVAL 30 DAY)
                ORDER BY ps.CutoffEnd DESC, ps.CreatedAt DESC", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        private static Payslip Map(MySqlDataReader r) => new Payslip
        {
            PayslipId = Convert.ToInt32(r["PayslipID"]),
            EmployeeId = Convert.ToInt32(r["EmployeeID"]),
            EmployeeName = r["EmployeeName"].ToString() ?? "",
            DepartmentName = r["DepartmentName"].ToString() ?? "Unassigned",
            CreatedAt = Convert.ToDateTime(r["CreatedAt"]),
            CutoffStart = Convert.ToDateTime(r["CutoffStart"]),
            CutoffEnd = Convert.ToDateTime(r["CutoffEnd"]),
            NoOfDays = Convert.ToDecimal(r["NoOfDays"]),
            HourlyRate = Convert.ToDecimal(r["HourlyRate"]),
            BasicPay = Convert.ToDecimal(r["BasicPay"]),
            NdPayPrem = Convert.ToDecimal(r["NdPayPrem"]),
            RiceAllowance = Convert.ToDecimal(r["RiceAllowance"]),
            DailyMeal = Convert.ToDecimal(r["DailyMeal"]),
            Uniform = Convert.ToDecimal(r["Uniform"]),
            Laundry = Convert.ToDecimal(r["Laundry"]),
            TotalOtPay = Convert.ToDecimal(r["TotalOtPay"]),
            RegHolPayPrem = Convert.ToDecimal(r["RegHolPayPrem"]),
            SpHolPayPrem = Convert.ToDecimal(r["SpHolPayPrem"]),
            LeaveWithPay = Convert.ToDecimal(r["LeaveWithPay"]),
            Adjustment = Convert.ToDecimal(r["Adjustment"]),
            Absences = Convert.ToDecimal(r["Absences"]),
            LateUtOb = Convert.ToDecimal(r["LateUtOb"]),
            SssContribution = Convert.ToDecimal(r["SssContribution"]),
            PhilHealthContribution = Convert.ToDecimal(r["PhilHealthContribution"]),
            HmdfContribution = Convert.ToDecimal(r["HmdfContribution"]),
            Loans = Convert.ToDecimal(r["Loans"]),
            AttendanceBonus = Convert.ToDecimal(r["AttendanceBonus"]),
            TenureBonus = Convert.ToDecimal(r["TenureBonus"]),
            Oic = Convert.ToDecimal(r["Oic"]),
            Account = Convert.ToDecimal(r["Account"]),
            Incentives = Convert.ToDecimal(r["Incentives"]),
            InternalCommission = Convert.ToDecimal(r["InternalCommission"]),
        };
    }
}
