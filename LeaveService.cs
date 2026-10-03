using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace PAYROLL
{
    public class LeaveRequestRecord
    {
        public int LeaveRequestId;
        public int EmployeeId;
        public string EmployeeName = "";
        public DateTime StartDate;
        public DateTime EndDate;
        public string Reason = "";
        public string Status = "Pending";
        public bool Paid;
    }

    public static class LeaveService
    {
        public static void CreateRequest(int employeeId, DateTime startDate, DateTime endDate, string reason)
        {
            if (startDate.Date < DateTime.Today)
                throw new ArgumentOutOfRangeException(nameof(startDate), "Leave requests must start today or later.");
            if (endDate.Date < startDate.Date)
                throw new ArgumentException("The leave end date must be on or after the start date.");
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
                throw new ArgumentException("Enter a reason of 1 to 500 characters.", nameof(reason));

            using var con = OpenConnection();
            using (var overlap = new MySqlCommand(@"
                SELECT COUNT(*) FROM LeaveRequests
                WHERE EmployeeID = @EmployeeID AND Status IN ('Pending', 'Approved')
                    AND StartDate <= @EndDate AND EndDate >= @StartDate", con))
            {
                overlap.Parameters.AddWithValue("@EmployeeID", employeeId);
                overlap.Parameters.AddWithValue("@StartDate", startDate.Date);
                overlap.Parameters.AddWithValue("@EndDate", endDate.Date);
                if (Convert.ToInt32(overlap.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("A pending or approved leave request already overlaps these dates.");
            }

            using var cmd = new MySqlCommand(@"
                INSERT INTO LeaveRequests (EmployeeID, StartDate, EndDate, Reason)
                VALUES (@EmployeeID, @StartDate, @EndDate, @Reason)", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@StartDate", startDate.Date);
            cmd.Parameters.AddWithValue("@EndDate", endDate.Date);
            cmd.Parameters.AddWithValue("@Reason", reason.Trim());
            cmd.ExecuteNonQuery();
        }

        public static List<LeaveRequestRecord> ListForRange(DateTime start, DateTime end, int? employeeId = null)
        {
            var requests = new List<LeaveRequestRecord>();
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT l.LeaveRequestID, l.EmployeeID, e.EmployeeName, l.StartDate, l.EndDate,
                    l.Reason, l.Status, l.Paid
                FROM LeaveRequests l
                JOIN Employees e ON e.EmployeeID = l.EmployeeID
                WHERE l.StartDate <= @End AND l.EndDate >= @Start
                    AND (@EmployeeID IS NULL OR l.EmployeeID = @EmployeeID)
                ORDER BY l.StartDate, e.EmployeeName", con);
            cmd.Parameters.AddWithValue("@Start", start.Date);
            cmd.Parameters.AddWithValue("@End", end.Date);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId.HasValue ? employeeId.Value : DBNull.Value);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                requests.Add(new LeaveRequestRecord
                {
                    LeaveRequestId = Convert.ToInt32(reader["LeaveRequestID"]),
                    EmployeeId = Convert.ToInt32(reader["EmployeeID"]),
                    EmployeeName = reader["EmployeeName"].ToString() ?? "",
                    StartDate = Convert.ToDateTime(reader["StartDate"]),
                    EndDate = Convert.ToDateTime(reader["EndDate"]),
                    Reason = reader["Reason"].ToString() ?? "",
                    Status = reader["Status"].ToString() ?? "Pending",
                    Paid = Convert.ToBoolean(reader["Paid"]),
                });
            return requests;
        }

        public static void Review(int requestId, bool approved, string reviewer)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                UPDATE LeaveRequests SET
                    Status = @Status, Paid = @Paid, ReviewedBy = @Reviewer, ReviewedAt = NOW()
                WHERE LeaveRequestID = @RequestID AND Status = 'Pending'", con);
            cmd.Parameters.AddWithValue("@Status", approved ? "Approved" : "Rejected");
            cmd.Parameters.AddWithValue("@Paid", approved);
            cmd.Parameters.AddWithValue("@Reviewer", reviewer);
            cmd.Parameters.AddWithValue("@RequestID", requestId);
            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("This leave request is no longer pending.");
        }

        public static decimal GetPaidWorkdays(int employeeId, DateTime start, DateTime end)
        {
            var approvedLeaves = ListForRange(start, end, employeeId);
            var holidays = new HashSet<DateTime>();
            for (int year = start.Year; year <= end.Year; year++)
                foreach (var holiday in CompanyService.ListHolidays(year))
                    if (holiday.HolidayType == "Regular") holidays.Add(holiday.HolidayDate.Date);

            // A day the employee actually worked is already paid through their hours,
            // so it must not be paid a second time as leave.
            var workedDates = AttendanceService.ListForRange(start, end, employeeId)
                .Where(record => record.Status == "Present")
                .Select(record => record.AttendanceDate.Date)
                .ToHashSet();

            var paidDates = new HashSet<DateTime>();
            foreach (var leave in approvedLeaves)
            {
                if (leave.Status != "Approved" || !leave.Paid) continue;
                DateTime leaveStart = leave.StartDate.Date < start.Date ? start.Date : leave.StartDate.Date;
                DateTime leaveEnd = leave.EndDate.Date > end.Date ? end.Date : leave.EndDate.Date;
                for (DateTime date = leaveStart; date <= leaveEnd; date = date.AddDays(1))
                    if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
                        && !holidays.Contains(date) && !workedDates.Contains(date))
                        paidDates.Add(date);
            }
            return paidDates.Count;
        }

        private static MySqlConnection OpenConnection()
        {
            var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            using var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS LeaveRequests (
                    LeaveRequestID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    EmployeeID INT NOT NULL,
                    StartDate DATE NOT NULL,
                    EndDate DATE NOT NULL,
                    Reason VARCHAR(500) NOT NULL,
                    Status VARCHAR(16) NOT NULL DEFAULT 'Pending',
                    Paid BOOLEAN NOT NULL DEFAULT FALSE,
                    RequestedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ReviewedBy VARCHAR(100) NULL,
                    ReviewedAt DATETIME NULL,
                    INDEX IX_LeaveRequests_Employee_Dates (EmployeeID, StartDate, EndDate)
                )", con);
            cmd.ExecuteNonQuery();
            return con;
        }
    }
}