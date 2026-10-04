using System;
using System.Collections.Generic;
using MySqlConnector;

namespace PAYROLL
{
    public class PayrollDefaultsConfig
    {
        public decimal RiceAllowance;
        public decimal DailyMeal;
        public decimal Uniform;
        public decimal Laundry;
        public decimal Incentives;
        public decimal DefaultHourlyRate;
    }

    public class Holiday
    {
        public DateTime HolidayDate;
        public string HolidayName = "";
        public string HolidayType = "Regular"; // "Regular" or "Special"
    }

    public class Department
    {
        public int DepartmentId;
        public string DepartmentName = "";
        public int ActiveEmployeeCount;
    }

    public class CompanyBenefit
    {
        public int BenefitId;
        public string BenefitName = "";
        public decimal DefaultAmount;
    }

    public static class CompanyService
    {
        public static void EnsureBenefitsSchema()
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            using var exists = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CompanyBenefits'", con);
            bool tableExisted = Convert.ToInt32(exists.ExecuteScalar()) > 0;
            using (var benefits = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS CompanyBenefits (
                    BenefitID INT AUTO_INCREMENT PRIMARY KEY,
                    BenefitName VARCHAR(120) NOT NULL UNIQUE,
                    DefaultAmount DECIMAL(12,2) NOT NULL DEFAULT 0
                )", con))
                benefits.ExecuteNonQuery();
            using (var items = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS PayslipBenefitItems (
                    BenefitItemID INT AUTO_INCREMENT PRIMARY KEY,
                    PayslipID INT NOT NULL,
                    BenefitName VARCHAR(120) NOT NULL,
                    Amount DECIMAL(12,2) NOT NULL DEFAULT 0,
                    INDEX IX_PayslipBenefitItems_PayslipID (PayslipID)
                )", con))
                items.ExecuteNonQuery();

            if (!tableExisted)
                SeedBenefitsFromLegacyDefaults(con);
        }

        private static void SeedBenefitsFromLegacyDefaults(MySqlConnection con)
        {
            var values = new List<(string Name, decimal Amount)>();
            try
            {
                using var read = new MySqlCommand(@"
                    SELECT RiceAllowance, DailyMeal, Uniform, Laundry, Incentives
                    FROM PayrollDefaults WHERE ConfigID = 1", con);
                using var reader = read.ExecuteReader();
                if (reader.Read())
                {
                    values.Add(("Rice Allowance", Convert.ToDecimal(reader["RiceAllowance"])));
                    values.Add(("Daily Meal", Convert.ToDecimal(reader["DailyMeal"])));
                    values.Add(("Uniform", Convert.ToDecimal(reader["Uniform"])));
                    values.Add(("Laundry", Convert.ToDecimal(reader["Laundry"])));
                    values.Add(("Incentives", Convert.ToDecimal(reader["Incentives"])));
                }
            }
            catch (MySqlException ex) when (ex.Number == 1146) { return; }

            foreach (var value in values)
            {
                if (value.Amount <= 0) continue;
                using var insert = new MySqlCommand(
                    "INSERT IGNORE INTO CompanyBenefits (BenefitName, DefaultAmount) VALUES (@Name, @Amount)", con);
                insert.Parameters.AddWithValue("@Name", value.Name);
                insert.Parameters.AddWithValue("@Amount", value.Amount);
                insert.ExecuteNonQuery();
            }
        }

        public static List<CompanyBenefit> ListBenefits()
        {
            EnsureBenefitsSchema();
            var list = new List<CompanyBenefit>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(
                "SELECT BenefitID, BenefitName, DefaultAmount FROM CompanyBenefits ORDER BY BenefitName", con);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new CompanyBenefit
                {
                    BenefitId = Convert.ToInt32(reader["BenefitID"]),
                    BenefitName = reader["BenefitName"].ToString() ?? "",
                    DefaultAmount = Convert.ToDecimal(reader["DefaultAmount"])
                });
            return list;
        }

        public static void AddBenefit(string name, decimal amount)
        {
            EnsureBenefitsSchema();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(
                "INSERT INTO CompanyBenefits (BenefitName, DefaultAmount) VALUES (@Name, @Amount)", con);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@Amount", amount);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        public static void DeleteBenefit(int benefitId)
        {
            EnsureBenefitsSchema();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand("DELETE FROM CompanyBenefits WHERE BenefitID=@ID", con);
            cmd.Parameters.AddWithValue("@ID", benefitId);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        public static void EnsureDepartmentSchema()
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            using (var departments = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS Departments (
                    DepartmentID INT AUTO_INCREMENT PRIMARY KEY,
                    DepartmentName VARCHAR(150) NOT NULL UNIQUE
                )", con))
                departments.ExecuteNonQuery();

            using var check = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Employees'
                  AND COLUMN_NAME = 'DepartmentID'", con);
            if (Convert.ToInt32(check.ExecuteScalar()) == 0)
            {
                using var alter = new MySqlCommand(
                    "ALTER TABLE Employees ADD COLUMN DepartmentID INT NULL", con);
                alter.ExecuteNonQuery();
            }

            EnsureEmployeeColumn(con, "HireDate", "DATE NULL");
            EnsureEmployeeColumn(con, "IsActive", "TINYINT(1) NOT NULL DEFAULT 1");
        }

        private static void EnsureEmployeeColumn(MySqlConnection con, string column, string definition)
        {
            using var check = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Employees' AND COLUMN_NAME = @Column", con);
            check.Parameters.AddWithValue("@Column", column);
            if (Convert.ToInt32(check.ExecuteScalar()) != 0) return;

            using var alter = new MySqlCommand($"ALTER TABLE Employees ADD COLUMN {column} {definition}", con);
            alter.ExecuteNonQuery();
        }

        // ---- Payroll defaults (single row, ConfigID = 1) ----
        public static PayrollDefaultsConfig GetPayrollDefaults()
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            EnsureDailyMealColumn(con);
            using var cmd = new MySqlCommand("SELECT * FROM PayrollDefaults WHERE ConfigID = 1", con);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new PayrollDefaultsConfig
                {
                    RiceAllowance = Convert.ToDecimal(reader["RiceAllowance"]),
                    DailyMeal = Convert.ToDecimal(reader["DailyMeal"]),
                    Uniform = Convert.ToDecimal(reader["Uniform"]),
                    Laundry = Convert.ToDecimal(reader["Laundry"]),
                    Incentives = Convert.ToDecimal(reader["Incentives"]),
                    DefaultHourlyRate = Convert.ToDecimal(reader["DefaultHourlyRate"]),
                };
            }
            // Falls back to the original hardcoded values if the table is
            // somehow empty (e.g. migration not yet run).
            return new PayrollDefaultsConfig
            {
                RiceAllowance = PayrollDefaults.RiceAllowance,
                DailyMeal = PayrollDefaults.DailyMeal,
                Uniform = PayrollDefaults.Uniform,
                Laundry = PayrollDefaults.Laundry,
                Incentives = PayrollDefaults.Incentives,
                DefaultHourlyRate = PayrollDefaults.DefaultHourlyRate,
            };
        }

        public static void SavePayrollDefaults(PayrollDefaultsConfig c)
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            EnsureDailyMealColumn(con);
            using var cmd = new MySqlCommand(@"
                UPDATE PayrollDefaults SET
                    RiceAllowance=@RiceAllowance, DailyMeal=@DailyMeal, Uniform=@Uniform, Laundry=@Laundry,
                    Incentives=@Incentives, DefaultHourlyRate=@DefaultHourlyRate
                WHERE ConfigID = 1", con);
            cmd.Parameters.AddWithValue("@RiceAllowance", c.RiceAllowance);
            cmd.Parameters.AddWithValue("@DailyMeal", c.DailyMeal);
            cmd.Parameters.AddWithValue("@Uniform", c.Uniform);
            cmd.Parameters.AddWithValue("@Laundry", c.Laundry);
            cmd.Parameters.AddWithValue("@Incentives", c.Incentives);
            cmd.Parameters.AddWithValue("@DefaultHourlyRate", c.DefaultHourlyRate);
            cmd.ExecuteNonQuery();
        }

        private static void EnsureDailyMealColumn(MySqlConnection con)
        {
            using var check = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PayrollDefaults' AND COLUMN_NAME = 'DailyMeal'", con);
            if (Convert.ToInt32(check.ExecuteScalar()) != 0) return;

            using var alter = new MySqlCommand(
                "ALTER TABLE PayrollDefaults ADD COLUMN DailyMeal DECIMAL(10,2) NOT NULL DEFAULT 0", con);
            alter.ExecuteNonQuery();
        }

        // ---- Holidays ----
        public static List<Holiday> ListHolidays() => ListHolidays(DateTime.Today.Year);

        public static List<Holiday> ListHolidays(int year)
        {
            var list = new List<Holiday>();
            void Add(DateTime date, string name, string type = "Regular") => list.Add(new Holiday
            {
                HolidayDate = date.Date,
                HolidayName = name,
                HolidayType = type,
            });

            DateTime easterSunday = GetEasterSunday(year);
            Add(new DateTime(year, 1, 1), "New Year's Day");
            Add(easterSunday.AddDays(-3), "Maundy Thursday");
            Add(easterSunday.AddDays(-2), "Good Friday");
            Add(easterSunday.AddDays(-1), "Black Saturday", "Special");
            Add(new DateTime(year, 2, 25), "EDSA People Power Revolution", "Special");
            Add(new DateTime(year, 4, 9), "Araw ng Kagitingan");
            Add(new DateTime(year, 5, 1), "Labor Day");
            Add(new DateTime(year, 6, 12), "Independence Day");
            Add(new DateTime(year, 8, 21), "Ninoy Aquino Day", "Special");
            Add(LastWeekdayOfMonth(year, 8, DayOfWeek.Monday), "National Heroes Day");
            Add(new DateTime(year, 11, 1), "All Saints' Day", "Special");
            Add(new DateTime(year, 11, 2), "All Souls' Day", "Special");
            Add(new DateTime(year, 11, 30), "Bonifacio Day");
            Add(new DateTime(year, 12, 8), "Feast of the Immaculate Conception", "Special");
            Add(new DateTime(year, 12, 24), "Christmas Eve", "Special");
            Add(new DateTime(year, 12, 25), "Christmas Day");
            Add(new DateTime(year, 12, 30), "Rizal Day");
            Add(new DateTime(year, 12, 31), "Last Day of the Year", "Special");

            list.Sort((left, right) => left.HolidayDate.CompareTo(right.HolidayDate));
            return list;
        }

        private static DateTime LastWeekdayOfMonth(int year, int month, DayOfWeek weekday)
        {
            var date = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            while (date.DayOfWeek != weekday) date = date.AddDays(-1);
            return date;
        }

        private static DateTime GetEasterSunday(int year)
        {
            int a = year % 19;
            int b = year / 100;
            int c = year % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31;
            int day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateTime(year, month, day);
        }

        // Looks up whether a given date is a holiday, and if so what type —
        // this is what Payroll will use to auto-suggest Reg./Sp. Hol pay.
        public static Holiday? FindHoliday(DateTime date)
        {
            return ListHolidays(date.Year).Find(holiday => holiday.HolidayDate == date.Date);
        }

        // ---- Departments ----
        public static List<Department> ListDepartments()
        {
            var list = new List<Department>();
            EnsureDepartmentSchema();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"
                SELECT d.DepartmentID, d.DepartmentName, COUNT(e.EmployeeID) AS ActiveEmployeeCount
                FROM Departments d
                LEFT JOIN Employees e ON e.DepartmentID = d.DepartmentID AND e.IsActive = 1
                GROUP BY d.DepartmentID, d.DepartmentName
                ORDER BY d.DepartmentName", con);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new Department
                {
                    DepartmentId = Convert.ToInt32(reader["DepartmentID"]),
                    DepartmentName = reader["DepartmentName"].ToString() ?? "",
                    ActiveEmployeeCount = Convert.ToInt32(reader["ActiveEmployeeCount"]),
                });
            return list;
        }

        public static List<AttendanceRecord> ListDepartmentAttendance(int departmentId, DateTime date)
        {
            EnsureDepartmentSchema();
            AttendanceService.EnsureAttendanceSchema();
            var list = new List<AttendanceRecord>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"
                SELECT e.EmployeeID, e.EmployeeName, a.AttendanceDate, a.TimeIn,
                    a.BreakOut, a.BreakIn, a.TimeOut, COALESCE(a.OvertimeApproved, 0) AS OvertimeApproved,
                    COALESCE(a.Status, 'Not recorded') AS Status,
                    a.UndertimeStatus, a.UndertimeRemarks, a.UndertimeReviewedBy, a.UndertimeReviewedAt
                FROM Employees e
                LEFT JOIN Attendance a
                    ON a.EmployeeID = e.EmployeeID AND a.AttendanceDate = @Date
                WHERE e.DepartmentID = @DepartmentID AND e.IsActive = 1
                ORDER BY e.EmployeeName", con);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            cmd.Parameters.AddWithValue("@DepartmentID", departmentId);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new AttendanceRecord
                {
                    EmployeeId = Convert.ToInt32(reader["EmployeeID"]),
                    EmployeeName = reader["EmployeeName"].ToString() ?? "",
                    AttendanceDate = reader["AttendanceDate"] is DBNull
                        ? date.Date : Convert.ToDateTime(reader["AttendanceDate"]),
                    TimeIn = ReadAttendanceTime(reader["TimeIn"]),
                    BreakOut = ReadAttendanceTime(reader["BreakOut"]),
                    BreakIn = ReadAttendanceTime(reader["BreakIn"]),
                    TimeOut = ReadAttendanceTime(reader["TimeOut"]),
                    OvertimeApproved = Convert.ToBoolean(reader["OvertimeApproved"]),
                    Status = reader["Status"].ToString() ?? "Not recorded",
                    UndertimeStatus = reader["UndertimeStatus"] is DBNull
                        ? null : reader["UndertimeStatus"].ToString(),
                    UndertimeRemarks = reader["UndertimeRemarks"] is DBNull
                        ? "" : reader["UndertimeRemarks"].ToString() ?? "",
                    UndertimeReviewedBy = reader["UndertimeReviewedBy"] is DBNull
                        ? "" : reader["UndertimeReviewedBy"].ToString() ?? "",
                    UndertimeReviewedAt = reader["UndertimeReviewedAt"] is DBNull
                        ? null : Convert.ToDateTime(reader["UndertimeReviewedAt"]),
                });
            return list;
        }

        public static Dictionary<int, int> CountDepartmentCheckIns(DateTime date)
        {
            EnsureDepartmentSchema();
            var counts = new Dictionary<int, int>();
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand(@"
                SELECT e.DepartmentID, COUNT(DISTINCT e.EmployeeID) AS CheckedInCount
                FROM Employees e
                JOIN Attendance a ON a.EmployeeID = e.EmployeeID
                    AND a.AttendanceDate = @Date
                    AND a.Status = 'Present'
                    AND a.TimeIn IS NOT NULL
                WHERE e.IsActive = 1 AND e.DepartmentID IS NOT NULL
                GROUP BY e.DepartmentID", con);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                counts[Convert.ToInt32(reader["DepartmentID"])] = Convert.ToInt32(reader["CheckedInCount"]);
            return counts;
        }

        private static TimeSpan? ReadAttendanceTime(object value) => value switch
        {
            DBNull => null,
            TimeSpan time => time,
            DateTime dateTime => dateTime.TimeOfDay,
            _ => TimeSpan.Parse(value.ToString() ?? "00:00:00"),
        };

        public static void AddDepartment(string name)
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand("INSERT INTO Departments (DepartmentName) VALUES (@Name)", con);
            cmd.Parameters.AddWithValue("@Name", name);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        public static void DeleteDepartment(int departmentId)
        {
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand("DELETE FROM Departments WHERE DepartmentID = @Id", con);
            cmd.Parameters.AddWithValue("@Id", departmentId);
            con.Open();
            cmd.ExecuteNonQuery();
        }
    }
}
