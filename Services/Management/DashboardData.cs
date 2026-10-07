using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySqlConnector;

namespace PAYROLL
{
    public sealed class PayrollTrendPoint
    {
        public string Label = "";
        public decimal Total;
    }

    public class DashboardData
    {
        public static string ConnString = AppConfig.ConnectionString;

        public List<string> Unavailable = new();
        public int TotalEmployees;
        public decimal TotalGrossPay, TotalNetPay, TotalOvertimePay, TotalDeductions;
        public List<(string Label, decimal Total)> PayByDepartment = new();
        public List<PayrollTrendPoint> PayrollByCutoff = new();
        public List<PayrollTrendPoint> PayrollByMonth = new();
        public List<PayrollTrendPoint> PayrollByYear = new();
        public DataTable Employees = new();

        public static DashboardData Load()
        {
            var d = new DashboardData();

            try
            {
                CompanyService.EnsureDepartmentSchema();
                var totals = d.Query(@"
                    SELECT COUNT(*) AS TotalEmployees,
                           IFNULL(SUM(GrossPay), 0) AS TotalGrossPay,
                           IFNULL(SUM(NetPay), 0) AS TotalNetPay,
                           IFNULL(SUM(OvertimePay), 0) AS TotalOvertimePay,
                           IFNULL(SUM(Deductions), 0) AS TotalDeductions
                    FROM Employees
                    WHERE IsActive=1");
                if (totals.Rows.Count > 0)
                {
                    var row = totals.Rows[0];
                    d.TotalEmployees = Convert.ToInt32(row["TotalEmployees"]);
                    d.TotalGrossPay = Convert.ToDecimal(row["TotalGrossPay"]);
                    d.TotalNetPay = Convert.ToDecimal(row["TotalNetPay"]);
                    d.TotalOvertimePay = Convert.ToDecimal(row["TotalOvertimePay"]);
                    d.TotalDeductions = Convert.ToDecimal(row["TotalDeductions"]);
                }
            }
            catch { d.Unavailable.Add("active employee and payroll totals (table: Employees)"); }

            try
            {
                var departments = d.Query(@"
                    SELECT COALESCE(dep.DepartmentName, 'Unassigned') AS Department,
                           SUM(e.NetPay) AS Total
                    FROM Employees e
                    LEFT JOIN Departments dep ON dep.DepartmentID=e.DepartmentID
                    WHERE e.IsActive=1
                    GROUP BY COALESCE(dep.DepartmentName, 'Unassigned')
                    HAVING SUM(e.NetPay) > 0
                    ORDER BY SUM(e.NetPay) DESC");
                var allDepartments = new List<(string Label, decimal Total)>();
                foreach (DataRow row in departments.Rows)
                    allDepartments.Add((row["Department"].ToString() ?? "Unassigned", Convert.ToDecimal(row["Total"])));

                // Keep the donut readable on firms with many departments.
                if (allDepartments.Count <= 5)
                    d.PayByDepartment = allDepartments;
                else
                {
                    d.PayByDepartment = allDepartments.Take(4).ToList();
                    d.PayByDepartment.Add(("Other departments", allDepartments.Skip(4).Sum(item => item.Total)));
                }
            }
            catch { d.Unavailable.Add("payroll by department (tables: Employees, Departments)"); }

            try
            {
                CompanyService.EnsurePayslipSchema();
                CompanyService.EnsureBenefitsSchema();
                string netPay = NetPayExpression("p", "b");
                d.PayrollByCutoff = d.LoadTrend($@"
                    SELECT CONCAT(DATE_FORMAT(MIN(p.CutoffStart), '%m/%d'), '–',
                                  DATE_FORMAT(MAX(p.CutoffEnd), '%m/%d')) AS Label,
                           SUM({netPay}) AS Total
                    FROM Payslips p
                    LEFT JOIN (SELECT PayslipID, SUM(Amount) AS BenefitTotal
                               FROM PayslipBenefitItems GROUP BY PayslipID) b ON b.PayslipID=p.PayslipID
                    GROUP BY p.CutoffStart, p.CutoffEnd
                    ORDER BY p.CutoffEnd DESC, p.CutoffStart DESC
                    LIMIT 12");
                d.PayrollByMonth = d.LoadTrend($@"
                    SELECT DATE_FORMAT(MAX(p.CutoffEnd), '%b %Y') AS Label,
                           SUM({netPay}) AS Total
                    FROM Payslips p
                    LEFT JOIN (SELECT PayslipID, SUM(Amount) AS BenefitTotal
                               FROM PayslipBenefitItems GROUP BY PayslipID) b ON b.PayslipID=p.PayslipID
                    GROUP BY YEAR(p.CutoffEnd), MONTH(p.CutoffEnd)
                    ORDER BY YEAR(p.CutoffEnd) DESC, MONTH(p.CutoffEnd) DESC
                    LIMIT 12");
                d.PayrollByYear = d.LoadTrend($@"
                    SELECT CAST(YEAR(MAX(p.CutoffEnd)) AS CHAR) AS Label,
                           SUM({netPay}) AS Total
                    FROM Payslips p
                    LEFT JOIN (SELECT PayslipID, SUM(Amount) AS BenefitTotal
                               FROM PayslipBenefitItems GROUP BY PayslipID) b ON b.PayslipID=p.PayslipID
                    GROUP BY YEAR(p.CutoffEnd)
                    ORDER BY YEAR(p.CutoffEnd) DESC
                    LIMIT 5");
            }
            catch (Exception ex)
            {
                // Keep the banner actionable without ever displaying the connection string.
                d.Unavailable.Add("payroll trends: " + ex.GetBaseException().Message);
            }

            try
            {
                d.Employees = d.Query(@"
                    SELECT EmployeeID, EmployeeName, Position, BasicSalary, GrossPay, NetPay
                    FROM Employees
                    ORDER BY IsActive DESC, EmployeeName");
            }
            catch { d.Unavailable.Add("employee list (table: Employees)"); }

            return d;
        }

        private List<PayrollTrendPoint> LoadTrend(string sql)
        {
            var rows = Query(sql);
            var points = new List<PayrollTrendPoint>();
            foreach (DataRow row in rows.Rows)
                points.Add(new PayrollTrendPoint
                {
                    Label = row["Label"].ToString() ?? "",
                    Total = Convert.ToDecimal(row["Total"])
                });
            // Queries take the newest periods first for LIMIT; charts read chronologically.
            points.Reverse();
            return points;
        }

        private DataTable Query(string sql)
        {
            var table = new DataTable();
            using var connection = new MySqlConnection(ConnString);
            using var adapter = new MySqlDataAdapter(sql, connection);
            adapter.Fill(table);
            return table;
        }

        private static string NetPayExpression(string payslipAlias, string benefitAlias) => $@"
            (COALESCE({payslipAlias}.BasicPay,0) + COALESCE({payslipAlias}.NdPayPrem,0) +
             COALESCE({payslipAlias}.RiceAllowance,0) + COALESCE({payslipAlias}.DailyMeal,0) +
             COALESCE({payslipAlias}.Uniform,0) + COALESCE({payslipAlias}.Laundry,0) +
             COALESCE({payslipAlias}.TotalOtPay,0) + COALESCE({payslipAlias}.RegHolPayPrem,0) +
             COALESCE({payslipAlias}.SpHolPayPrem,0) + COALESCE({payslipAlias}.LeaveWithPay,0) +
             COALESCE({payslipAlias}.Adjustment,0) + COALESCE({benefitAlias}.BenefitTotal,0) -
             COALESCE({payslipAlias}.Absences,0) - COALESCE({payslipAlias}.LateUtOb,0) -
             COALESCE({payslipAlias}.SssContribution,0) -
             COALESCE({payslipAlias}.PhilHealthContribution,0) -
             COALESCE({payslipAlias}.HmdfContribution,0) - COALESCE({payslipAlias}.Loans,0))";
    }
}
