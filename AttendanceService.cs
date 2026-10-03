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
        public TimeSpan? BreakOut;
        public TimeSpan? BreakIn;
        public TimeSpan? TimeOut;
        public bool OvertimeApproved;
        public string Status = "";
    }

    public class AttendanceSummary
    {
        public decimal PresentDays;
        public decimal AbsentDays;
        public decimal RegularHolidayDays;
        public decimal SpecialHolidayDays;
        public decimal RegularHours;
        public decimal ApprovedOvertimeHours;
        public decimal OverbreakMinutes;
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
                UPDATE Attendance SET TimeOut = @Time, OvertimeApproved = 0
                WHERE EmployeeID = @EmployeeID AND AttendanceDate = @Date
                    AND TimeIn IS NOT NULL AND Status = 'Present'", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", DateTime.Today);
            cmd.Parameters.AddWithValue("@Time", time);
            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("Record time-in before recording time-out.");
        }

        public static void RecordBreakOut(int employeeId, TimeSpan time)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                UPDATE Attendance SET BreakOut = @Time
                WHERE EmployeeID = @EmployeeID AND AttendanceDate = @Date
                    AND TimeIn IS NOT NULL AND TimeOut IS NULL AND BreakOut IS NULL", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", DateTime.Today);
            cmd.Parameters.AddWithValue("@Time", time);
            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("Record time-in before recording break-out. Break-out can only be recorded once before time-out.");
        }

        public static void RecordBreakIn(int employeeId, TimeSpan time)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                UPDATE Attendance SET BreakIn = @Time
                WHERE EmployeeID = @EmployeeID AND AttendanceDate = @Date
                    AND BreakOut IS NOT NULL AND BreakIn IS NULL AND TimeOut IS NULL", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", DateTime.Today);
            cmd.Parameters.AddWithValue("@Time", time);
            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("Record break-out before recording break-in. Break-in can only be recorded once before time-out.");
        }

        public static void SetOvertimeApproval(int employeeId, DateTime date, bool approved)
        {
            var record = ListForDate(date, employeeId).Find(item => item.EmployeeId == employeeId);
            if (record?.TimeIn == null || record.TimeOut == null || GetOvertimeMinutes(record) <= 0)
                throw new InvalidOperationException("There is no completed overtime record to approve.");

            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                UPDATE Attendance SET OvertimeApproved = @Approved
                WHERE EmployeeID = @EmployeeID AND AttendanceDate = @Date
                    AND TimeIn IS NOT NULL AND TimeOut IS NOT NULL AND Status = 'Present'", con);
            cmd.Parameters.AddWithValue("@Approved", approved);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            cmd.ExecuteNonQuery();
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
                SELECT a.EmployeeID, e.EmployeeName, a.AttendanceDate, a.TimeIn,
                    a.BreakOut, a.BreakIn, a.TimeOut, a.OvertimeApproved, a.Status
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
                    BreakOut = ReadTime(reader["BreakOut"]),
                    BreakIn = ReadTime(reader["BreakIn"]),
                    TimeOut = ReadTime(reader["TimeOut"]),
                    OvertimeApproved = Convert.ToBoolean(reader["OvertimeApproved"]),
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
            var holidays = CompanyService.ListHolidays(start.Year)
                .Concat(start.Year == end.Year ? new List<Holiday>() : CompanyService.ListHolidays(end.Year))
                .ToList();
            var regularHolidayDates = holidays
                .Where(holiday => holiday.HolidayType == "Regular")
                .Select(holiday => holiday.HolidayDate.Date)
                .ToHashSet();
            var specialHolidayDates = holidays
                .Where(holiday => holiday.HolidayType == "Special")
                .Select(holiday => holiday.HolidayDate.Date)
                .ToHashSet();
            decimal regularHours = 0m;
            decimal approvedOvertimeHours = 0m;
            decimal overbreakMinutes = 0m;
            foreach (var record in attendance.Where(record => record.Status == "Present" &&
                record.TimeIn.HasValue && record.TimeOut.HasValue))
            {
                regularHours += Math.Min(GetWorkedMinutes(record), 8 * 60) / 60m;
                if (record.OvertimeApproved)
                    approvedOvertimeHours += GetOvertimeMinutes(record) / 60m;
                overbreakMinutes += GetOverbreakMinutes(record);
            }

            return new AttendanceSummary
            {
                PresentDays = Convert.ToDecimal(reader["PresentDays"]),
                AbsentDays = Convert.ToDecimal(reader["AbsentDays"]),
                RegularHolidayDays = attendance.Count(record => record.Status == "Present" &&
                    regularHolidayDates.Contains(record.AttendanceDate.Date)),
                SpecialHolidayDays = attendance.Count(record => record.Status == "Present" &&
                    specialHolidayDates.Contains(record.AttendanceDate.Date)),
                RegularHours = regularHours,
                ApprovedOvertimeHours = approvedOvertimeHours,
                OverbreakMinutes = overbreakMinutes,
            };
        }

        public static int GetOvertimeMinutes(AttendanceRecord record)
            => Math.Max(0, GetWorkedMinutes(record) - 8 * 60);

        public static int GetOverbreakMinutes(AttendanceRecord record)
        {
            if (!record.BreakOut.HasValue || !record.BreakIn.HasValue) return 0;
            return Math.Max(0, GetBreakDurationMinutes(record) - 60);
        }

        private static int GetWorkedMinutes(AttendanceRecord record)
        {
            if (!record.TimeIn.HasValue || !record.TimeOut.HasValue) return 0;
            int elapsedMinutes = GetDurationMinutes(record.TimeIn.Value, record.TimeOut.Value);
            // Worked time only loses the standard 60-minute break. Anything over that is
            // overbreak, which the payslip deducts separately, so it must not also shrink
            // the worked hours (that would charge the employee twice).
            int breakMinutes = record.BreakOut.HasValue && record.BreakIn.HasValue
                ? Math.Min(GetBreakDurationMinutes(record), 60)
                : 60;
            return Math.Max(0, elapsedMinutes - breakMinutes);
        }

        private static int GetBreakDurationMinutes(AttendanceRecord record)
            => GetDurationMinutes(record.BreakOut!.Value, record.BreakIn!.Value);

        private static int GetDurationMinutes(TimeSpan start, TimeSpan end)
        {
            double minutes = (end - start).TotalMinutes;
            if (minutes < 0) minutes += 24 * 60;
            return (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
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
            EnsureColumn(con, "BreakOut", "TIME NULL");
            EnsureColumn(con, "BreakIn", "TIME NULL");
            EnsureColumn(con, "OvertimeApproved", "BOOLEAN NOT NULL DEFAULT FALSE");
            return con;
        }

        private static void EnsureColumn(MySqlConnection con, string column, string definition)
        {
            using var check = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Attendance' AND COLUMN_NAME = @Column", con);
            check.Parameters.AddWithValue("@Column", column);
            if (Convert.ToInt32(check.ExecuteScalar()) != 0) return;

            using var alter = new MySqlCommand($"ALTER TABLE Attendance ADD COLUMN {column} {definition}", con);
            alter.ExecuteNonQuery();
        }
    }
}