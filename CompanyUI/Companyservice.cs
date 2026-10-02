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
    }

    public static class CompanyService
    {
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
            using var con = new MySqlConnection(AppConfig.ConnectionString);
            using var cmd = new MySqlCommand("SELECT * FROM Departments ORDER BY DepartmentName", con);
            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new Department
                {
                    DepartmentId = Convert.ToInt32(reader["DepartmentID"]),
                    DepartmentName = reader["DepartmentName"].ToString() ?? "",
                });
            return list;
        }

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