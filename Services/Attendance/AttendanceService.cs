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

        // Raised when the employee times out before completing the 8-hour duty:
        // null = no undertime, "Open" = management still has to talk to the employee,
        // "Resolved" = discussed (the reason and decision are in UndertimeRemarks).
        public string? UndertimeStatus;
        public string UndertimeRemarks = "";
        public string UndertimeReviewedBy = "";
        public DateTime? UndertimeReviewedAt;
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
        public static void EnsureAttendanceSchema()
        {
            using var con = OpenConnection();
        }

        // The standard break is one hour, and it can only start once the employee
        // has worked one full hour since time-in.
        private const int StandardBreakMinutes = 60;
        private const int MinMinutesBeforeBreak = 60;

        // The 8-hour duty: time worked (after the standard break) below this is undertime.
        public const int RegularDayMinutes = 8 * 60;

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
            var today = TodayRecord(employeeId);
            string? problem = TimeOutProblem(today, time);
            if (problem != null) throw new InvalidOperationException(problem);

            // Timing out before the 8-hour duty is done is allowed (the screen warns first),
            // but it raises a notice so management can talk to the employee.
            int undertime = UndertimeIfTimedOutAt(today!, time);

            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                UPDATE Attendance SET TimeOut = @Time, OvertimeApproved = 0, UndertimeStatus = @UndertimeStatus
                WHERE EmployeeID = @EmployeeID AND AttendanceDate = @Date
                    AND TimeIn IS NOT NULL AND Status = 'Present'
                    AND BreakOut IS NOT NULL AND BreakIn IS NOT NULL AND TimeOut IS NULL", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", DateTime.Today);
            cmd.Parameters.AddWithValue("@Time", time);
            cmd.Parameters.AddWithValue("@UndertimeStatus", undertime > 0 ? (object)"Open" : DBNull.Value);
            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("Time-out could not be recorded. Check your time-in and break, and that you haven't already timed out.");
        }

        // Why a time-out can't be recorded right now (null = it can). Time-out is only
        // allowed once, and only after the break is finished (break-in recorded).
        private static string? TimeOutProblem(AttendanceRecord? today, TimeSpan time)
        {
            if (today?.TimeIn == null || today.Status != "Present")
                return "Record time-in before recording time-out.";
            if (today.TimeOut != null)
                return "Time-out is already recorded for today.";
            if (today.BreakOut == null || today.BreakIn == null)
                return "Finish your break first. Record break-out and break-in before you time out.";
            if (time <= today.BreakIn.Value)
                return $"Time-out must be after your {FormatClock(today.BreakIn.Value)} break-in.";
            return null;
        }

        // How many minutes short of the 8-hour duty the employee would be if they timed out
        // at 'time'. 0 if they would complete it, or if the time-out isn't allowed yet (then
        // RecordTimeOut explains why). The screen uses this to warn before an early time-out.
        public static int GetUndertimeMinutes(int employeeId, TimeSpan time)
        {
            var today = TodayRecord(employeeId);
            return TimeOutProblem(today, time) != null ? 0 : UndertimeIfTimedOutAt(today!, time);
        }

        private static int UndertimeIfTimedOutAt(AttendanceRecord today, TimeSpan time)
            => GetUndertimeMinutes(new AttendanceRecord { Status = "Present", TimeIn = today.TimeIn, TimeOut = time });

        // Undertime of a finished day: the part of the 8-hour duty that wasn't worked.
        public static int GetUndertimeMinutes(AttendanceRecord record)
        {
            if (record.Status != "Present" || !record.TimeIn.HasValue || !record.TimeOut.HasValue) return 0;
            return Math.Max(0, RegularDayMinutes - GetWorkedMinutes(record));
        }

        public static string FormatDuration(int minutes)
            => minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}m" : $"{minutes}m";

        // ---------------- undertime notices (management side) ----------------

        public static List<AttendanceRecord> ListUndertimeNotices(bool openOnly)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand("SELECT " + RecordColumns + @"
                FROM Attendance a
                JOIN Employees e ON e.EmployeeID = a.EmployeeID
                WHERE a.UndertimeStatus IS NOT NULL
                    AND (@OpenOnly = 0 OR a.UndertimeStatus = 'Open')
                ORDER BY a.AttendanceDate DESC, e.EmployeeName", con);
            cmd.Parameters.AddWithValue("@OpenOnly", openOnly ? 1 : 0);
            return ReadRecords(cmd);
        }

        public static int CountOpenUndertimeNotices()
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM Attendance WHERE UndertimeStatus = 'Open'", con);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static List<AttendanceRecord> ListPendingOvertimeApprovals()
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand("SELECT " + RecordColumns + @"
                FROM Attendance a
                JOIN Employees e ON e.EmployeeID = a.EmployeeID
                WHERE a.Status = 'Present' AND a.TimeIn IS NOT NULL AND a.TimeOut IS NOT NULL
                    AND a.OvertimeApproved = 0
                ORDER BY a.AttendanceDate DESC, e.EmployeeName", con);
            return ReadRecords(cmd).FindAll(record => GetOvertimeMinutes(record) > 0);
        }

        // Management talked to the employee: record what was said and decided.
        public static void ResolveUndertime(int employeeId, DateTime date, string remarks, string reviewer)
        {
            if (string.IsNullOrWhiteSpace(remarks))
                throw new ArgumentException("Write the reason the employee gave and what was decided before marking this as discussed.");
            if (remarks.Trim().Length > 500)
                throw new ArgumentException("Keep the remarks to 500 characters or fewer.");

            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"
                UPDATE Attendance SET
                    UndertimeStatus = 'Resolved', UndertimeRemarks = @Remarks,
                    UndertimeReviewedBy = @Reviewer, UndertimeReviewedAt = NOW()
                WHERE EmployeeID = @EmployeeID AND AttendanceDate = @Date AND UndertimeStatus = 'Open'", con);
            cmd.Parameters.AddWithValue("@Remarks", remarks.Trim());
            cmd.Parameters.AddWithValue("@Reviewer", reviewer);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            if (cmd.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("This notice has already been marked as discussed.");
            EmployeeNotificationService.Add(employeeId, "UndertimeFollowUp", date.Date.ToString("yyyy-MM-dd"),
                "Undertime follow-up", $"Management recorded a follow-up for {date:MMM d, yyyy}: {remarks.Trim()}");
        }

        public static void RecordBreakOut(int employeeId, TimeSpan time)
        {
            var today = TodayRecord(employeeId);
            if (today?.TimeIn != null && today.TimeOut == null && today.BreakOut == null)
            {
                double minutesSinceTimeIn = (time - today.TimeIn.Value).TotalMinutes;
                if (minutesSinceTimeIn < MinMinutesBeforeBreak)
                {
                    var earliest = today.TimeIn.Value + TimeSpan.FromMinutes(MinMinutesBeforeBreak);
                    throw new InvalidOperationException(
                        $"You can't start your break yet. Break-out opens one hour after time-in, " +
                        $"which is {FormatClock(earliest)} for your {FormatClock(today.TimeIn.Value)} time-in.");
                }
            }

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
            var today = TodayRecord(employeeId);
            if (today?.BreakOut != null && today.BreakIn == null && time <= today.BreakOut.Value)
                throw new InvalidOperationException(
                    $"Break-in must be after your {FormatClock(today.BreakOut.Value)} break-out.");

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
            if (cmd.ExecuteNonQuery() > 0 && approved)
                EmployeeNotificationService.Add(employeeId, "OvertimeApproval", date.Date.ToString("yyyy-MM-dd"),
                    "Overtime approved", $"Your overtime for {date:MMM d, yyyy} has been approved.");
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

        private const string RecordColumns = @"
                a.EmployeeID, e.EmployeeName, a.AttendanceDate, a.TimeIn,
                a.BreakOut, a.BreakIn, a.TimeOut, a.OvertimeApproved, a.Status,
                a.UndertimeStatus, a.UndertimeRemarks, a.UndertimeReviewedBy, a.UndertimeReviewedAt";

        public static List<AttendanceRecord> ListForRange(DateTime start, DateTime end, int? employeeId = null)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand("SELECT " + RecordColumns + @"
                FROM Attendance a
                JOIN Employees e ON e.EmployeeID = a.EmployeeID
                WHERE a.AttendanceDate BETWEEN @Start AND @End
                    AND (@EmployeeID IS NULL OR a.EmployeeID = @EmployeeID)
                ORDER BY a.AttendanceDate, e.EmployeeName", con);
            cmd.Parameters.AddWithValue("@Start", start.Date);
            cmd.Parameters.AddWithValue("@End", end.Date);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId.HasValue ? (object)employeeId.Value : DBNull.Value);
            return ReadRecords(cmd);
        }

        private static List<AttendanceRecord> ReadRecords(MySqlCommand cmd)
        {
            var records = new List<AttendanceRecord>();
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
                    UndertimeStatus = reader["UndertimeStatus"] is DBNull ? null : reader["UndertimeStatus"].ToString(),
                    UndertimeRemarks = reader["UndertimeRemarks"] is DBNull ? "" : reader["UndertimeRemarks"].ToString() ?? "",
                    UndertimeReviewedBy = reader["UndertimeReviewedBy"] is DBNull ? "" : reader["UndertimeReviewedBy"].ToString() ?? "",
                    UndertimeReviewedAt = reader["UndertimeReviewedAt"] is DBNull ? null : Convert.ToDateTime(reader["UndertimeReviewedAt"]),
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
                regularHours += Math.Min(GetWorkedMinutes(record), RegularDayMinutes) / 60m;
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

        // How many minutes of the standard break would still be left if the employee
        // returned at 'time'. 0 when the break is complete or hasn't started. The screen
        // uses this to warn before an early break-in (which is still allowed).
        public static int GetUnfinishedBreakMinutes(int employeeId, TimeSpan time)
        {
            var today = TodayRecord(employeeId);
            if (today?.BreakOut == null || today.BreakIn != null) return 0;

            double taken = Math.Max(0, (time - today.BreakOut.Value).TotalMinutes);
            return taken >= StandardBreakMinutes ? 0 : (int)Math.Ceiling(StandardBreakMinutes - taken);
        }

        private static AttendanceRecord? TodayRecord(int employeeId)
            => ListForDate(DateTime.Today, employeeId).Find(item => item.EmployeeId == employeeId);

        private static string FormatClock(TimeSpan time)
            => DateTime.Today.Add(time).ToString("h:mm tt");

        public static int GetOvertimeMinutes(AttendanceRecord record)
            => Math.Max(0, GetWorkedMinutes(record) - RegularDayMinutes);

        public static int GetOverbreakMinutes(AttendanceRecord record)
        {
            if (!record.BreakOut.HasValue || !record.BreakIn.HasValue) return 0;
            return Math.Max(0, GetBreakDurationMinutes(record) - StandardBreakMinutes);
        }

        private static int GetWorkedMinutes(AttendanceRecord record)
        {
            if (!record.TimeIn.HasValue || !record.TimeOut.HasValue) return 0;
            int elapsedMinutes = GetDurationMinutes(record.TimeIn.Value, record.TimeOut.Value);
            // The standard hour always comes off, whatever break was actually taken:
            //   - a longer break is charged once, as overbreak on the payslip, so it must
            //     not also shrink the worked hours;
            //   - a shorter (unfinished) break earns no extra paid time.
            return Math.Max(0, elapsedMinutes - StandardBreakMinutes);
        }

        private static int GetBreakDurationMinutes(AttendanceRecord record)
            => GetDurationMinutes(record.BreakOut!.Value, record.BreakIn!.Value);

        private static int GetDurationMinutes(TimeSpan start, TimeSpan end)
        {
            double minutes = (end - start).TotalMinutes;
            if (minutes < 0) minutes += 24 * 60;
            return (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
        }

        private static bool schemaReady;

        private static MySqlConnection OpenConnection()
        {
            var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            if (!schemaReady)
            {
                try { EnsureSchema(con); schemaReady = true; }
                catch { con.Dispose(); throw; }
            }
            return con;
        }

        private static void EnsureSchema(MySqlConnection con)
        {
            using (var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS Attendance (
                    AttendanceID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    EmployeeID INT NOT NULL,
                    AttendanceDate DATE NOT NULL,
                    TimeIn TIME NULL,
                    TimeOut TIME NULL,
                    Status VARCHAR(16) NOT NULL,
                    UNIQUE KEY UQ_Attendance_Employee_Date (EmployeeID, AttendanceDate)
                )", con))
                cmd.ExecuteNonQuery();
            EnsureColumn(con, "BreakOut", "TIME NULL");
            EnsureColumn(con, "BreakIn", "TIME NULL");
            EnsureColumn(con, "OvertimeApproved", "BOOLEAN NOT NULL DEFAULT FALSE");
            EnsureColumn(con, "UndertimeStatus", "VARCHAR(16) NULL");
            EnsureColumn(con, "UndertimeRemarks", "VARCHAR(500) NULL");
            EnsureColumn(con, "UndertimeReviewedBy", "VARCHAR(100) NULL");
            EnsureColumn(con, "UndertimeReviewedAt", "DATETIME NULL");
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
