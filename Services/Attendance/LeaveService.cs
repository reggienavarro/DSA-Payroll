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
        public static int CountPending()
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM LeaveRequests WHERE Status = 'Pending'", con);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static void CreateRequest(int employeeId, DateTime startDate, DateTime endDate, string reason)
        {
            if (startDate.Date < DateTime.Today)
                throw new ArgumentOutOfRangeException(nameof(startDate), "Leave requests must start today or later.");
            if (endDate.Date < startDate.Date)
                throw new ArgumentException("The leave end date must be on or after the start date.");
            if (startDate.Year != endDate.Year)
                throw new ArgumentException("A leave request must be within one calendar year so the correct annual leave credits can be checked.");
            if (GetRequestedWorkdays(startDate, endDate) <= 0)
                throw new ArgumentException("The selected dates contain no eligible workdays for paid leave.");
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
            using var transaction = con.BeginTransaction();
            int employeeId;
            DateTime startDate;
            DateTime endDate;
            using (var owner = new MySqlCommand("SELECT EmployeeID, StartDate, EndDate, Status FROM LeaveRequests WHERE LeaveRequestID=@RequestID FOR UPDATE", con, transaction))
            {
                owner.Parameters.AddWithValue("@RequestID", requestId);
                using var reader = owner.ExecuteReader();
                if (!reader.Read()) throw new InvalidOperationException("This leave request was not found.");
                if (!string.Equals(reader["Status"].ToString(), "Pending", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("This leave request is no longer pending.");
                employeeId = Convert.ToInt32(reader["EmployeeID"]);
                startDate = Convert.ToDateTime(reader["StartDate"]);
                endDate = Convert.ToDateTime(reader["EndDate"]);
            }
            if (approved)
            {
                EnsureCreditRow(con, transaction, employeeId, startDate.Year, reviewer);
                decimal allowance;
                using (var credit = new MySqlCommand(@"SELECT EntitledDays FROM EmployeeLeaveBalances
                    WHERE EmployeeID=@EmployeeID AND LeaveYear=@Year FOR UPDATE", con, transaction))
                {
                    credit.Parameters.AddWithValue("@EmployeeID", employeeId);
                    credit.Parameters.AddWithValue("@Year", startDate.Year);
                    allowance = Convert.ToDecimal(credit.ExecuteScalar());
                }
                decimal alreadyUsed = GetApprovedPaidDays(con, transaction, employeeId, startDate.Year, requestId);
                decimal requested = GetRequestedWorkdays(startDate, endDate);
                if (requested > allowance - alreadyUsed)
                    throw new InvalidOperationException($"Insufficient paid leave credits. Requested {requested:0.##} workday(s), available {Math.Max(0m, allowance - alreadyUsed):0.##}. Update the employee's annual allowance or reject this request.");
            }
            using var cmd = new MySqlCommand(@"
                UPDATE LeaveRequests SET
                    Status = @Status, Paid = @Paid, ReviewedBy = @Reviewer, ReviewedAt = NOW()
                WHERE LeaveRequestID = @RequestID AND Status = 'Pending'", con, transaction);
            cmd.Parameters.AddWithValue("@Status", approved ? "Approved" : "Rejected");
            cmd.Parameters.AddWithValue("@Paid", approved);
            cmd.Parameters.AddWithValue("@Reviewer", reviewer);
            cmd.Parameters.AddWithValue("@RequestID", requestId);
            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("This leave request is no longer pending.");
            transaction.Commit();
            string decision = approved ? "approved" : "rejected";
            EmployeeNotificationService.Add(employeeId, "LeaveDecision", requestId.ToString(),
                "Leave request " + (approved ? "approved" : "not approved"),
                $"Your leave request for {startDate:MMM d, yyyy} to {endDate:MMM d, yyyy} was {decision}.");
        }

        public static List<LeaveCreditBalance> ListCreditBalances(int year)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"SELECT e.EmployeeID, e.EmployeeName, COALESCE(b.EntitledDays,0) AS EntitledDays
                FROM Employees e LEFT JOIN EmployeeLeaveBalances b ON b.EmployeeID=e.EmployeeID AND b.LeaveYear=@Year
                WHERE e.IsActive=1 ORDER BY e.EmployeeName", con);
            cmd.Parameters.AddWithValue("@Year", year);
            var result = new List<LeaveCreditBalance>();
            using (var reader = cmd.ExecuteReader())
                while (reader.Read()) result.Add(new LeaveCreditBalance
                {
                    EmployeeId = Convert.ToInt32(reader["EmployeeID"]),
                    EmployeeName = reader["EmployeeName"].ToString() ?? "",
                    LeaveYear = year,
                    EntitledDays = Convert.ToDecimal(reader["EntitledDays"]),
                });

            var approved = ListForRange(new DateTime(year, 1, 1), new DateTime(year, 12, 31))
                .Where(request => request.Status == "Approved" && request.Paid).ToList();
            var holidays = GetRegularHolidays(year);
            foreach (var balance in result)
                balance.UsedDays = approved.Where(request => request.EmployeeId == balance.EmployeeId)
                    .Sum(request => CountWorkdaysInYear(request.StartDate, request.EndDate, year, holidays));
            return result;
        }

        public static LeaveCreditBalance GetCreditBalance(int employeeId, int year)
        {
            var balance = new LeaveCreditBalance { EmployeeId = employeeId, LeaveYear = year };
            using (var con = OpenConnection())
            using (var cmd = new MySqlCommand(@"SELECT e.EmployeeName, COALESCE(b.EntitledDays,0) AS EntitledDays
                FROM Employees e LEFT JOIN EmployeeLeaveBalances b ON b.EmployeeID=e.EmployeeID AND b.LeaveYear=@Year
                WHERE e.EmployeeID=@EmployeeID", con))
            {
                cmd.Parameters.AddWithValue("@Year", year);
                cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    balance.EmployeeName = reader["EmployeeName"].ToString() ?? "";
                    balance.EntitledDays = Convert.ToDecimal(reader["EntitledDays"]);
                }
            }
            var holidays = GetRegularHolidays(year);
            balance.UsedDays = ListForRange(new DateTime(year, 1, 1), new DateTime(year, 12, 31), employeeId)
                .Where(request => request.Status == "Approved" && request.Paid)
                .Sum(request => CountWorkdaysInYear(request.StartDate, request.EndDate, year, holidays));
            return balance;
        }

        public static void SetAnnualCredits(int employeeId, int year, decimal entitledDays, string updatedBy)
        {
            if (entitledDays < 0 || entitledDays > 366) throw new ArgumentOutOfRangeException(nameof(entitledDays), "Annual leave credits must be between 0 and 366 days.");
            using var con = OpenConnection();
            using var transaction = con.BeginTransaction();
            EnsureCreditRow(con, transaction, employeeId, year, updatedBy);
            decimal used = GetApprovedPaidDays(con, transaction, employeeId, year, null);
            if (entitledDays < used)
                throw new InvalidOperationException($"The employee has already used {used:0.##} paid leave day(s). The allowance cannot be set below that amount.");
            using var cmd = new MySqlCommand(@"UPDATE EmployeeLeaveBalances SET EntitledDays=@Days, UpdatedBy=@User, UpdatedAt=NOW()
                WHERE EmployeeID=@EmployeeID AND LeaveYear=@Year", con, transaction);
            cmd.Parameters.AddWithValue("@Days", entitledDays);
            cmd.Parameters.AddWithValue("@User", updatedBy);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }

        public static decimal GetRequestedWorkdays(DateTime start, DateTime end)
        {
            if (end.Date < start.Date) return 0;
            var holidays = new HashSet<DateTime>();
            for (int year = start.Year; year <= end.Year; year++) holidays.UnionWith(GetRegularHolidays(year));
            return CountWorkdays(start, end, holidays);
        }

        private static decimal GetApprovedPaidDays(MySqlConnection con, MySqlTransaction transaction, int employeeId, int year, int? excludeRequestId)
        {
            var requests = new List<(DateTime Start, DateTime End)>();
            using (var cmd = new MySqlCommand(@"SELECT StartDate, EndDate FROM LeaveRequests
                WHERE EmployeeID=@EmployeeID AND Status='Approved' AND Paid=1
                  AND StartDate<=@YearEnd AND EndDate>=@YearStart
                  AND (@ExcludeID IS NULL OR LeaveRequestID<>@ExcludeID)", con, transaction))
            {
                cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
                cmd.Parameters.AddWithValue("@YearStart", new DateTime(year, 1, 1));
                cmd.Parameters.AddWithValue("@YearEnd", new DateTime(year, 12, 31));
                cmd.Parameters.AddWithValue("@ExcludeID", excludeRequestId.HasValue ? excludeRequestId.Value : DBNull.Value);
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) requests.Add((Convert.ToDateTime(reader["StartDate"]), Convert.ToDateTime(reader["EndDate"])));
            }
            var holidays = GetRegularHolidays(year);
            return requests.Sum(request => CountWorkdaysInYear(request.Start, request.End, year, holidays));
        }

        private static int CountWorkdays(DateTime start, DateTime end, HashSet<DateTime> holidays)
        {
            int count = 0;
            for (DateTime date = start.Date; date <= end.Date; date = date.AddDays(1))
                if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !holidays.Contains(date)) count++;
            return count;
        }

        private static int CountWorkdaysInYear(DateTime start, DateTime end, int year, HashSet<DateTime> holidays)
        {
            DateTime from = start.Date < new DateTime(year, 1, 1) ? new DateTime(year, 1, 1) : start.Date;
            DateTime to = end.Date > new DateTime(year, 12, 31) ? new DateTime(year, 12, 31) : end.Date;
            return from <= to ? CountWorkdays(from, to, holidays) : 0;
        }

        private static HashSet<DateTime> GetRegularHolidays(int year) => CompanyService.ListHolidays(year)
            .Where(holiday => holiday.HolidayType == "Regular")
            .Select(holiday => holiday.HolidayDate.Date).ToHashSet();

        private static void EnsureCreditRow(MySqlConnection con, MySqlTransaction transaction, int employeeId, int year, string user)
        {
            using var cmd = new MySqlCommand(@"INSERT IGNORE INTO EmployeeLeaveBalances (EmployeeID, LeaveYear, EntitledDays, UpdatedBy)
                VALUES (@EmployeeID, @Year, 0, @User)", con, transaction);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@User", user);
            cmd.ExecuteNonQuery();
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
            using var balances = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS EmployeeLeaveBalances (
                EmployeeID INT NOT NULL,
                LeaveYear SMALLINT NOT NULL,
                EntitledDays DECIMAL(6,2) NOT NULL DEFAULT 0,
                UpdatedBy VARCHAR(100) NOT NULL,
                UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (EmployeeID, LeaveYear)
            )", con);
            balances.ExecuteNonQuery();
            return con;
        }
    }
}
