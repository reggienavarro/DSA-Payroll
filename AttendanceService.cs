using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace PAYROLL
{
    public class AttendanceRecord
    {
        public int EmployeeId;
        public string EmployeeName = "";
        public DateTime AttendanceDate;
        public TimeSpan? TimeIn;
        public TimeSpan? TimeOut;
        public string Status = "";
    }

    public class AttendanceSummary
    {
        public decimal PresentDays;
        public decimal AbsentDays;
        public decimal RegularHolidayDays;
    }

    public static class AttendanceService
    {
        public static void RecordTimeIn(int employeeId, TimeSpan time)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                INSERT INTO Attendance (EmployeeID, AttendanceDate, TimeIn, Status)
                VALUES (@EmployeeID, @Date, @Time, 'Present')
                ON DUPLICATE KEY UPDATE
                    TimeIn = COALESCE(TimeIn, @Time), Status = 'Present'", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", DateTime.Today);
            cmd.Parameters.AddWithValue("@Time", time);
            cmd.ExecuteNonQuery();
        }

        public static void RecordTimeOut(int employeeId, TimeSpan time)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                UPDATE Attendance SET TimeOut = @Time
                WHERE EmployeeID = @EmployeeID AND AttendanceDate = @Date
                    AND TimeIn IS NOT NULL AND Status = 'Present'", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", DateTime.Today);
            cmd.Parameters.AddWithValue("@Time", time);
            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("Record time-in before recording time-out.");
        }

        public static void ReportAbsence(int employeeId, DateTime date)
        {
            if (date.Date > DateTime.Today)
                throw new ArgumentOutOfRangeException(nameof(date), "Future dates cannot be reported as absent.");

            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                INSERT INTO Attendance (EmployeeID, AttendanceDate, Status)
                VALUES (@EmployeeID, @Date, 'Absent')
                ON DUPLICATE KEY UPDATE
                    Status = IF(TimeIn IS NULL, 'Absent', Status)", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            cmd.ExecuteNonQuery();

            using var check = new MySqlCommand(@"
                SELECT Status FROM Attendance
                WHERE EmployeeID = @EmployeeID AND AttendanceDate = @Date", con);
            check.Parameters.AddWithValue("@EmployeeID", employeeId);
            check.Parameters.AddWithValue("@Date", date.Date);
            if (Convert.ToString(check.ExecuteScalar()) != "Absent")
                throw new InvalidOperationException("An employee with a recorded time-in cannot be marked absent.");
        }

        public static List<AttendanceRecord> ListForDate(DateTime date, int? employeeId = null)
            => ListForRange(date, date, employeeId);

        public static List<AttendanceRecord> ListForRange(DateTime start, DateTime end, int? employeeId = null)
        {
            var records = new List<AttendanceRecord>();
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT a.EmployeeID, e.EmployeeName, a.AttendanceDate, a.TimeIn, a.TimeOut, a.Status
                FROM Attendance a
                JOIN Employees e ON e.EmployeeID = a.EmployeeID
                WHERE a.AttendanceDate BETWEEN @Start AND @End
                    AND (@EmployeeID IS NULL OR a.EmployeeID = @EmployeeID)
                ORDER BY a.AttendanceDate, e.EmployeeName", con);
            cmd.Parameters.AddWithValue("@Start", start.Date);
            cmd.Parameters.AddWithValue("@End", end.Date);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId.HasValue ? employeeId.Value : DBNull.Value);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                records.Add(new AttendanceRecord
                {
                    EmployeeId = Convert.ToInt32(reader["EmployeeID"]),
                    EmployeeName = reader["EmployeeName"].ToString() ?? "",
                    AttendanceDate = Convert.ToDateTime(reader["AttendanceDate"]),
                    TimeIn = ReadTime(reader["TimeIn"]),
                    TimeOut = ReadTime(reader["TimeOut"]),
                    Status = reader["Status"].ToString() ?? "",
                });
            return records;
        }

        private static TimeSpan? ReadTime(object value) => value switch
        {
            DBNull => null,
            TimeSpan time => time,
            DateTime dateTime => dateTime.TimeOfDay,
            _ => TimeSpan.Parse(value.ToString() ?? "00:00:00"),
        };

        public static AttendanceSummary GetSummary(int employeeId, DateTime start, DateTime end)
        {
            var attendance = ListForRange(start, end, employeeId);
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT
                    COALESCE(SUM(Status = 'Present'), 0) AS PresentDays,
                    COALESCE(SUM(Status = 'Absent'), 0) AS AbsentDays
                FROM Attendance
                WHERE EmployeeID = @EmployeeID
                    AND AttendanceDate BETWEEN @Start AND @End", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Start", start.Date);
            cmd.Parameters.AddWithValue("@End", end.Date);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            var regularHolidayDates = CompanyService.ListHolidays(start.Year)
                .Concat(start.Year == end.Year ? new List<Holiday>() : CompanyService.ListHolidays(end.Year))
                .Where(holiday => holiday.HolidayType == "Regular")
                .Select(holiday => holiday.HolidayDate.Date)
                .ToHashSet();
            return new AttendanceSummary
            {
                PresentDays = Convert.ToDecimal(reader["PresentDays"]),
                AbsentDays = Convert.ToDecimal(reader["AbsentDays"]),
                RegularHolidayDays = attendance.Count(record => record.Status == "Present" &&
                    regularHolidayDates.Contains(record.AttendanceDate.Date)),
            };
        }

        private static MySqlConnection OpenConnection()
        {
            var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            using var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS Attendance (
                    AttendanceID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    EmployeeID INT NOT NULL,
                    AttendanceDate DATE NOT NULL,
                    TimeIn TIME NULL,
                    TimeOut TIME NULL,
                    Status VARCHAR(16) NOT NULL,
                    UNIQUE KEY UQ_Attendance_Employee_Date (EmployeeID, AttendanceDate)
                )", con);
            cmd.ExecuteNonQuery();
            return con;
        }
    }
}