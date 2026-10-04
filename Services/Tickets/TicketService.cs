using System;
using System.Collections.Generic;
using MySqlConnector;

namespace PAYROLL
{
    public class TicketRow
    {
        public int TicketId;
        public int EmployeeId;
        public string EmployeeName = "";
        public string DepartmentName = "Unassigned";
        public string Subject = "";
        public string Category = "Other";
        public string Message = "";
        public string Urgency = "Medium";
        public string Status = "";
        public string? AdminResponse;
        public DateTime CreatedAt;
    }

    // Everything that reads/writes the SalaryTickets table.
    public static class TicketService
    {
        private static bool schemaReady;

        public static int CountPending()
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM SalaryTickets
                WHERE LOWER(Status) NOT IN ('resolved', 'closed')", con);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static (int Low, int Medium, int High) CountPendingByUrgency()
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT
                    COALESCE(SUM(LOWER(Urgency) = 'low'), 0) AS LowCount,
                    COALESCE(SUM(LOWER(Urgency) = 'medium'), 0) AS MediumCount,
                    COALESCE(SUM(LOWER(Urgency) = 'high'), 0) AS HighCount
                FROM SalaryTickets
                WHERE LOWER(Status) NOT IN ('resolved', 'closed')", con);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            return (Convert.ToInt32(reader["LowCount"]), Convert.ToInt32(reader["MediumCount"]),
                Convert.ToInt32(reader["HighCount"]));
        }

        public static void Create(int employeeId, string subject, string message, string urgency = "Medium", string category = "Other")
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                INSERT INTO SalaryTickets (EmployeeID, Subject, Message, Urgency, Category)
                VALUES (@EmployeeID, @Subject, @Message, @Urgency, @Category)", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Subject", subject);
            cmd.Parameters.AddWithValue("@Message", message);
            cmd.Parameters.AddWithValue("@Urgency", NormalizeUrgency(urgency));
            cmd.Parameters.AddWithValue("@Category", NormalizeCategory(category));
            cmd.ExecuteNonQuery();
        }

        // A single employee's own tickets — used by EmployeeDashboardForm.
        public static List<TicketRow> LoadForEmployee(int employeeId)
        {
            var list = new List<TicketRow>();
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(
                "SELECT TicketID, Subject, Message, Status, AdminResponse, CreatedAt, Urgency, Category " +
                "FROM SalaryTickets WHERE EmployeeID = @EmployeeID ORDER BY CreatedAt DESC", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TicketRow
                {
                    TicketId = Convert.ToInt32(reader["TicketID"]),
                    EmployeeId = employeeId,
                    Subject = reader["Subject"].ToString() ?? "",
                    Category = reader["Category"].ToString() ?? "Other",
                    Message = reader["Message"].ToString() ?? "",
                    Urgency = reader["Urgency"].ToString() ?? "Medium",
                    Status = reader["Status"].ToString() ?? "",
                    AdminResponse = reader["AdminResponse"] is DBNull ? null : reader["AdminResponse"].ToString(),
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                });
            }
            return list;
        }

        // Every ticket from every employee, with the employee's name joined
        // in — used by the Admin "Tickets" screen.
        public static List<TicketRow> LoadAll()
        {
            CompanyService.EnsureDepartmentSchema();
            var list = new List<TicketRow>();
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT t.TicketID, t.EmployeeID, e.EmployeeName,
                       COALESCE(d.DepartmentName, 'Unassigned') AS DepartmentName,
                       t.Subject, t.Message, t.Status, t.AdminResponse, t.CreatedAt, t.Urgency, t.Category
                FROM SalaryTickets t
                JOIN Employees e ON e.EmployeeID = t.EmployeeID
                LEFT JOIN Departments d ON d.DepartmentID = e.DepartmentID
                ORDER BY t.CreatedAt DESC", con);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TicketRow
                {
                    TicketId = Convert.ToInt32(reader["TicketID"]),
                    EmployeeId = Convert.ToInt32(reader["EmployeeID"]),
                    EmployeeName = reader["EmployeeName"].ToString() ?? "",
                    DepartmentName = reader["DepartmentName"].ToString() ?? "Unassigned",
                    Subject = reader["Subject"].ToString() ?? "",
                    Category = reader["Category"].ToString() ?? "Other",
                    Message = reader["Message"].ToString() ?? "",
                    Urgency = reader["Urgency"].ToString() ?? "Medium",
                    Status = reader["Status"].ToString() ?? "",
                    AdminResponse = reader["AdminResponse"] is DBNull ? null : reader["AdminResponse"].ToString(),
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                });
            }
            return list;
        }

        public static void Respond(int ticketId, string response)
        {
            using var con = OpenConnection();
            int employeeId;
            using (var owner = new MySqlCommand("SELECT EmployeeID FROM SalaryTickets WHERE TicketID=@TicketID", con))
            {
                owner.Parameters.AddWithValue("@TicketID", ticketId);
                var value = owner.ExecuteScalar();
                if (value == null || value is DBNull) throw new InvalidOperationException("Ticket was not found.");
                employeeId = Convert.ToInt32(value);
            }
            using var cmd = new MySqlCommand(
                "UPDATE SalaryTickets SET AdminResponse=@Response, Status='Resolved', ResolvedAt=NOW() " +
                "WHERE TicketID=@TicketID", con);
            cmd.Parameters.AddWithValue("@Response", response);
            cmd.Parameters.AddWithValue("@TicketID", ticketId);
            cmd.ExecuteNonQuery();
            EmployeeNotificationService.Add(con, employeeId, "TicketResolved", ticketId.ToString(),
                "Ticket resolved", "Management responded to your ticket #" + ticketId + ": " + response.Trim());
        }

        private static string NormalizeCategory(string category)
            => category.Equals("Salary dispute", StringComparison.OrdinalIgnoreCase) ? "Salary dispute"
             : category.Equals("Report", StringComparison.OrdinalIgnoreCase) ? "Report" : "Other";

        private static string NormalizeUrgency(string urgency)
            => urgency.Equals("Low", StringComparison.OrdinalIgnoreCase) ? "Low"
             : urgency.Equals("High", StringComparison.OrdinalIgnoreCase) ? "High" : "Medium";

        private static MySqlConnection OpenConnection()
        {
            var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            if (!schemaReady)
            {
                try
                {
                    using var check = new MySqlCommand(@"
                        SELECT COUNT(*) FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SalaryTickets'
                            AND COLUMN_NAME IN ('Urgency', 'Category')", con);
                    if (Convert.ToInt32(check.ExecuteScalar()) < 2)
                    {
                        using var checkColumns = new MySqlCommand(@"SELECT COLUMN_NAME FROM information_schema.COLUMNS
                            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='SalaryTickets'", con);
                        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        using (var reader = checkColumns.ExecuteReader()) while (reader.Read()) columns.Add(reader.GetString(0));
                        if (!columns.Contains("Urgency")) new MySqlCommand("ALTER TABLE SalaryTickets ADD COLUMN Urgency VARCHAR(10) NOT NULL DEFAULT 'Medium'", con).ExecuteNonQuery();
                        if (!columns.Contains("Category")) new MySqlCommand("ALTER TABLE SalaryTickets ADD COLUMN Category VARCHAR(40) NOT NULL DEFAULT 'Other'", con).ExecuteNonQuery();
                    }
                    schemaReady = true;
                }
                catch
                {
                    con.Dispose();
                    throw;
                }
            }
            return con;
        }
    }
}
