using System;
using System.Collections.Generic;
using MySqlConnector;

namespace PAYROLL
{
    public sealed class EmployeeNotification
    {
        public long NotificationId;
        public string EventType = "";
        public string Title = "";
        public string Message = "";
        public DateTime CreatedAt;
        public bool IsRead;
    }

    public static class EmployeeNotificationService
    {
        public static int CountUnread(int employeeId)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM EmployeeNotifications WHERE EmployeeID=@EmployeeID AND IsRead=0", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static List<EmployeeNotification> List(int employeeId)
        {
            var items = new List<EmployeeNotification>();
            using var con = OpenConnection();
            using var cmd = new MySqlCommand(@"SELECT NotificationID, EventType, Title, Message, CreatedAt, IsRead
                FROM EmployeeNotifications WHERE EmployeeID=@EmployeeID ORDER BY CreatedAt DESC LIMIT 100", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) items.Add(new EmployeeNotification
            {
                NotificationId = Convert.ToInt64(reader["NotificationID"]),
                EventType = reader["EventType"].ToString() ?? "",
                Title = reader["Title"].ToString() ?? "Notification",
                Message = reader["Message"].ToString() ?? "",
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                IsRead = Convert.ToBoolean(reader["IsRead"])
            });
            return items;
        }

        public static void MarkAllRead(int employeeId)
        {
            using var con = OpenConnection();
            using var cmd = new MySqlCommand("UPDATE EmployeeNotifications SET IsRead=1 WHERE EmployeeID=@EmployeeID AND IsRead=0", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.ExecuteNonQuery();
        }

        public static void Add(int employeeId, string eventType, string sourceKey, string title, string message)
        {
            using var con = OpenConnection();
            Add(con, employeeId, eventType, sourceKey, title, message);
        }

        internal static void Add(MySqlConnection con, int employeeId, string eventType, string sourceKey, string title, string message)
        {
            EnsureSchema(con);
            using var cmd = new MySqlCommand(@"INSERT IGNORE INTO EmployeeNotifications
                (EmployeeID, EventType, SourceKey, Title, Message) VALUES
                (@EmployeeID, @EventType, @SourceKey, @Title, @Message)", con);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@EventType", eventType);
            cmd.Parameters.AddWithValue("@SourceKey", sourceKey);
            cmd.Parameters.AddWithValue("@Title", title);
            cmd.Parameters.AddWithValue("@Message", message);
            cmd.ExecuteNonQuery();
        }

        private static MySqlConnection OpenConnection()
        {
            var con = new MySqlConnection(AppConfig.ConnectionString);
            con.Open();
            EnsureSchema(con);
            return con;
        }

        private static void EnsureSchema(MySqlConnection con)
        {
            using var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS EmployeeNotifications (
                NotificationID BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                EmployeeID INT NOT NULL,
                EventType VARCHAR(40) NOT NULL,
                SourceKey VARCHAR(100) NOT NULL,
                Title VARCHAR(160) NOT NULL,
                Message TEXT NOT NULL,
                CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                IsRead TINYINT(1) NOT NULL DEFAULT 0,
                UNIQUE KEY UX_EmployeeNotification (EmployeeID, EventType, SourceKey),
                INDEX IX_EmployeeNotifications_Inbox (EmployeeID, IsRead, CreatedAt)
            )", con);
            cmd.ExecuteNonQuery();
        }
    }
}
