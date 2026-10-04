using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;

namespace PAYROLL
{
    public class DashboardData
    {
        public static string ConnString = AppConfig.ConnectionString;

        public List<string> Unavailable = new();

        public int TotalEmployees;
        public decimal TotalGrossPay, TotalNetPay, TotalOvertimePay, TotalDeductions;

        public string NextPayDate = "--";
        public string NextPayNote = "no payroll schedule in database";

        public List<(string Label, decimal Total)> PayByPosition = new();

        public List<(string Label, decimal Value)> Summary = new();

        public DataTable Employees = new();

        public static DashboardData Load()
        {
            var d = new DashboardData();

            try
            {
                var t = d.Query(@"
                    SELECT COUNT(*)                     AS TotalEmployees,
                           IFNULL(SUM(GrossPay), 0)    AS TotalGrossPay,
                           IFNULL(SUM(NetPay), 0)      AS TotalNetPay,
                           IFNULL(SUM(OvertimePay), 0) AS TotalOvertimePay,
                           IFNULL(SUM(Deductions), 0)  AS TotalDeductions
                    FROM Employees");
                if (t.Rows.Count > 0)
                {
                    var r = t.Rows[0];
                    d.TotalEmployees   = Convert.ToInt32(r["TotalEmployees"]);
                    d.TotalGrossPay    = Convert.ToDecimal(r["TotalGrossPay"]);
                    d.TotalNetPay      = Convert.ToDecimal(r["TotalNetPay"]);
                    d.TotalOvertimePay = Convert.ToDecimal(r["TotalOvertimePay"]);
                    d.TotalDeductions  = Convert.ToDecimal(r["TotalDeductions"]);
                }
            }
            catch { d.Unavailable.Add("employee totals (table: Employees)"); }

            try
            {
                var t = d.Query(@"
                    SELECT IFNULL(Position, '(none)') AS Position, SUM(NetPay) AS Total
                    FROM Employees
                    GROUP BY Position
                    ORDER BY SUM(NetPay) DESC
                    LIMIT 12");
                foreach (DataRow r in t.Rows)
                                    d.PayByPosition.Add((r["Position"].ToString() ?? "", Convert.ToDecimal(r["Total"])));
            }
            catch { d.Unavailable.Add("payroll by position (table: Employees)"); }

            if (d.TotalNetPay + d.TotalOvertimePay + d.TotalDeductions > 0)
            {
                d.Summary.Add(("Net Pay", d.TotalNetPay));
                d.Summary.Add(("Overtime Pay", d.TotalOvertimePay));
                d.Summary.Add(("Deductions", d.TotalDeductions));
            }

            try
            {
                d.Employees = d.Query(@"
                    SELECT EmployeeID, EmployeeName, Position, BasicSalary, GrossPay, NetPay
                    FROM Employees
                    ORDER BY EmployeeName");
            }
            catch { d.Unavailable.Add("employee list (table: Employees)"); }

            return d;
        }

        private DataTable Query(string sql)
        {
            var dt = new DataTable();
            using (var con = new MySqlConnection(ConnString))
            using (var da = new MySqlDataAdapter(sql, con))
                da.Fill(dt);
            return dt;
        }
    }
}