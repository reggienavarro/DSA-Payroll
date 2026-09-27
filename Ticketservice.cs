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
        public string Subject = "";
        public string Message = "";
        public string Status = "";
        public string? AdminResponse;
        public DateTime CreatedAt;
    }

    // Everything that reads/writes the SalaryTickets table.
    public static class TicketService
    {
        public static void Create(int employeeId, string subject, string message)
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(
                "INSERT INTO SalaryTickets (EmployeeID, Subject, Message) VALUES (@EmployeeID, @Subject, @Message)", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Subject", subject);
            cmd.Parameters.AddWithValue("@Message", message);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        // A single employee's own tickets — used by EmployeeDashboardForm.
        public static List<TicketRow> LoadForEmployee(int employeeId)
        {
            var list = new List<TicketRow>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(
                "SELECT TicketID, Subject, Message, Status, AdminResponse, CreatedAt " +
                "FROM SalaryTickets WHERE EmployeeID = @EmployeeID ORDER BY CreatedAt DESC", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TicketRow
                {
                    TicketId = Convert.ToInt32(reader["TicketID"]),
                    EmployeeId = employeeId,
                    Subject = reader["Subject"].ToString() ?? "",
                    Message = reader["Message"].ToString() ?? "",
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
            var list = new List<TicketRow>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"
                SELECT t.TicketID, t.EmployeeID, e.EmployeeName, t.Subject, t.Message,
                       t.Status, t.AdminResponse, t.CreatedAt
                FROM SalaryTickets t
                JOIN Employees e ON e.EmployeeID = t.EmployeeID
                ORDER BY t.CreatedAt DESC", con);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TicketRow
                {
                    TicketId = Convert.ToInt32(reader["TicketID"]),
                    EmployeeId = Convert.ToInt32(reader["EmployeeID"]),
                    EmployeeName = reader["EmployeeName"].ToString() ?? "",
                    Subject = reader["Subject"].ToString() ?? "",
                    Message = reader["Message"].ToString() ?? "",
                    Status = reader["Status"].ToString() ?? "",
                    AdminResponse = reader["AdminResponse"] is DBNull ? null : reader["AdminResponse"].ToString(),
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                });
            }
            return list;
        }

        public static void Respond(int ticketId, string response)
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(
                "UPDATE SalaryTickets SET AdminResponse=@Response, Status='Resolved', ResolvedAt=NOW() " +
                "WHERE TicketID=@TicketID", con);
            cmd.Parameters.AddWithValue("@Response", response);
            cmd.Parameters.AddWithValue("@TicketID", ticketId);
            con.Open();
            cmd.ExecuteNonQuery();
        }
    }
}