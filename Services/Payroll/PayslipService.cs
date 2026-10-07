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
            CompanyService.EnsureBenefitsSchema();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            using var transaction = con.BeginTransaction();
            CreateInTransaction(con, transaction, p);
            transaction.Commit();
        }

        internal static int CreateInTransaction(MySqlConnection con, MySqlTransaction transaction, Payslip p)
        {
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
                 @AttendanceBonus, @TenureBonus, @Oic, @Account, @Incentives, @InternalCommission)", con, transaction);

            using (var duplicate = new MySqlCommand(@"SELECT PayslipID FROM Payslips
                WHERE EmployeeID=@EmployeeID AND CutoffStart=@CutoffStart AND CutoffEnd=@CutoffEnd
                ORDER BY PayslipID LIMIT 1", con, transaction))
            {
                duplicate.Parameters.AddWithValue("@EmployeeID", p.EmployeeId);
                duplicate.Parameters.AddWithValue("@CutoffStart", p.CutoffStart.Date);
                duplicate.Parameters.AddWithValue("@CutoffEnd", p.CutoffEnd.Date);
                if (duplicate.ExecuteScalar() is not null)
                    throw new InvalidOperationException("A payslip already exists for this employee and cutoff period.");
            }

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

            cmd.ExecuteNonQuery();
            int payslipId = Convert.ToInt32(cmd.LastInsertedId);
            foreach (var benefit in p.Benefits)
            {
                using var benefitCmd = new MySqlCommand(@"
                    INSERT INTO PayslipBenefitItems (PayslipID, BenefitName, Amount)
                    VALUES (@PayslipID, @BenefitName, @Amount)", con, transaction);
                benefitCmd.Parameters.AddWithValue("@PayslipID", payslipId);
                benefitCmd.Parameters.AddWithValue("@BenefitName", benefit.BenefitName);
                benefitCmd.Parameters.AddWithValue("@Amount", benefit.Amount);
                benefitCmd.ExecuteNonQuery();
            }

            using var updateEmployee = new MySqlCommand(@"UPDATE Employees SET
                HourlyRate=@Rate, BasicSalary=@BasicPay, GrossPay=@Total, OvertimePay=@TotalOtPay,
                Deductions=@TotalDeductions, NetPay=@NetPay WHERE EmployeeID=@EmployeeID", con, transaction);
            updateEmployee.Parameters.AddWithValue("@Rate", p.HourlyRate);
            updateEmployee.Parameters.AddWithValue("@BasicPay", p.BasicPay);
            updateEmployee.Parameters.AddWithValue("@Total", p.Total);
            updateEmployee.Parameters.AddWithValue("@TotalOtPay", p.TotalOtPay);
            updateEmployee.Parameters.AddWithValue("@TotalDeductions", p.TotalDeductions);
            updateEmployee.Parameters.AddWithValue("@NetPay", p.NetPay);
            updateEmployee.Parameters.AddWithValue("@EmployeeID", p.EmployeeId);
            updateEmployee.ExecuteNonQuery();
            return payslipId;
        }

        public static List<Payslip> LoadAll()
        {
            CompanyService.EnsureDepartmentSchema();
            CompanyService.EnsureBenefitsSchema();
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
            LoadBenefitItems(list);
            return list;
        }

        public static List<Payslip> LoadForEmployee(int employeeId)
        {
            CompanyService.EnsureBenefitsSchema();
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
            LoadBenefitItems(list);
            return list;
        }

        public static List<Payslip> LoadRecentForEmployee(int employeeId)
        {
            CompanyService.EnsureBenefitsSchema();
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
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    list.Add(Map(reader));
            LoadBenefitItems(list);
            return list;
        }

        private static void LoadBenefitItems(List<Payslip> payslips)
        {
            if (payslips.Count == 0) return;
            var placeholders = new List<string>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand { Connection = con };
            for (int i = 0; i < payslips.Count; i++)
            {
                string parameter = "@id" + i;
                placeholders.Add(parameter);
                cmd.Parameters.AddWithValue(parameter, payslips[i].PayslipId);
            }
            cmd.CommandText = $@"
                SELECT PayslipID, BenefitName, Amount
                FROM PayslipBenefitItems
                WHERE PayslipID IN ({string.Join(",", placeholders)})
                ORDER BY BenefitItemID";
            var byId = new Dictionary<int, Payslip>();
            foreach (var payslip in payslips) byId[payslip.PayslipId] = payslip;
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int id = Convert.ToInt32(reader["PayslipID"]);
                if (!byId.TryGetValue(id, out var payslip)) continue;
                payslip.Benefits.Add(new PayslipBenefitItem
                {
                    BenefitName = reader["BenefitName"].ToString() ?? "Benefit",
                    Amount = Convert.ToDecimal(reader["Amount"])
                });
            }
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
