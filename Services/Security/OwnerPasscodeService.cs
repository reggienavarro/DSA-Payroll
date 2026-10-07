using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace PAYROLL;

internal static class OwnerPasscodeService
{
    private const int SettingsId = 1;
    private const int HashIterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);

    public static bool IsConfigured(string connectionString)
    {
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        EnsureSchema(connection);
        using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM OwnerSecuritySettings WHERE SettingsID=@ID", connection);
        command.Parameters.AddWithValue("@ID", SettingsId);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public static bool IsLockedOut(string connectionString)
    {
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        EnsureSchema(connection);
        using var command = new MySqlCommand(
            "SELECT LockedUntilUtc FROM OwnerSecuritySettings WHERE SettingsID=@ID", connection);
        command.Parameters.AddWithValue("@ID", SettingsId);
        object? value = command.ExecuteScalar();
        return value is not null && value is not DBNull && ToUtcDateTime(value) > DateTime.UtcNow;
    }

    public static bool Configure(string connectionString, string passcode, string configuredBy)
    {
        if (passcode.Length < 12 || passcode.Length > 256)
            throw new ArgumentException("The owner passcode must be between 12 and 256 characters.");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = DeriveHash(passcode, salt, HashIterations);
        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureSchema(connection);
            using var command = new MySqlCommand(@"
                INSERT INTO OwnerSecuritySettings
                    (SettingsID, PasscodeHash, PasscodeSalt, HashIterations, ConfiguredBy)
                VALUES (@ID, @Hash, @Salt, @Iterations, @ConfiguredBy)", connection);
            command.Parameters.AddWithValue("@ID", SettingsId);
            command.Parameters.AddWithValue("@Hash", Convert.ToHexString(hash));
            command.Parameters.AddWithValue("@Salt", Convert.ToHexString(salt));
            command.Parameters.AddWithValue("@Iterations", HashIterations);
            command.Parameters.AddWithValue("@ConfiguredBy", string.IsNullOrWhiteSpace(configuredBy) ? "Admin" : configuredBy);
            try
            {
                return command.ExecuteNonQuery() == 1;
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return false; // Never replace an already-configured owner passcode here.
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    public static bool Verify(string connectionString, string suppliedPasscode)
    {
        if (string.IsNullOrEmpty(suppliedPasscode)) return false;

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        EnsureSchema(connection);
        using var transaction = connection.BeginTransaction();

        string storedHash;
        string storedSalt;
        int iterations;
        int failedAttempts;
        DateTime? lockedUntil;
        using (var command = new MySqlCommand(@"
            SELECT PasscodeHash, PasscodeSalt, HashIterations, FailedAttempts, LockedUntilUtc
            FROM OwnerSecuritySettings WHERE SettingsID=@ID FOR UPDATE", connection, transaction))
        {
            command.Parameters.AddWithValue("@ID", SettingsId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                reader.Close();
                transaction.Commit();
                return false;
            }
            storedHash = reader.GetString("PasscodeHash");
            storedSalt = reader.GetString("PasscodeSalt");
            iterations = reader.GetInt32("HashIterations");
            failedAttempts = reader.GetInt32("FailedAttempts");
            lockedUntil = reader.IsDBNull(reader.GetOrdinal("LockedUntilUtc"))
                ? null
                : ToUtcDateTime(reader.GetValue(reader.GetOrdinal("LockedUntilUtc")));
        }

        DateTime now = DateTime.UtcNow;
        if (lockedUntil.HasValue && lockedUntil.Value > now)
        {
            transaction.Commit();
            return false;
        }

        byte[] salt = Convert.FromHexString(storedSalt);
        byte[] expectedHash = Convert.FromHexString(storedHash);
        byte[] suppliedHash = DeriveHash(suppliedPasscode, salt, iterations);
        try
        {
            bool matches = CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
            if (matches)
            {
                UpdateFailureState(connection, transaction, 0, null);
                transaction.Commit();
                return true;
            }

            failedAttempts++;
            DateTime? newLockout = failedAttempts >= MaxFailedAttempts ? now.Add(LockoutDuration) : null;
            if (newLockout.HasValue) failedAttempts = 0;
            UpdateFailureState(connection, transaction, failedAttempts, newLockout);
            transaction.Commit();
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(expectedHash);
            CryptographicOperations.ZeroMemory(suppliedHash);
        }
    }

    public static void AddPromotionAudit(
        MySqlConnection connection, MySqlTransaction transaction,
        int employeeId, string changedBy)
    {
        using var command = new MySqlCommand(@"
            INSERT INTO AdminRoleAudit (EmployeeID, PreviousRole, NewRole, ChangedBy)
            VALUES (@EmployeeID, 'Employee', 'Admin', @ChangedBy)", connection, transaction);
        command.Parameters.AddWithValue("@EmployeeID", employeeId);
        command.Parameters.AddWithValue("@ChangedBy", string.IsNullOrWhiteSpace(changedBy) ? "Admin" : changedBy);
        command.ExecuteNonQuery();
    }

    private static void EnsureSchema(MySqlConnection connection)
    {
        using var settings = new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS OwnerSecuritySettings (
                SettingsID TINYINT NOT NULL PRIMARY KEY,
                PasscodeHash CHAR(64) NOT NULL,
                PasscodeSalt CHAR(32) NOT NULL,
                HashIterations INT NOT NULL,
                ConfiguredBy VARCHAR(100) NOT NULL,
                ConfiguredAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FailedAttempts INT NOT NULL DEFAULT 0,
                LockedUntilUtc DATETIME NULL
            ) ENGINE=InnoDB", connection);
        settings.ExecuteNonQuery();

        using var audit = new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS AdminRoleAudit (
                AuditID BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                EmployeeID INT NOT NULL,
                PreviousRole VARCHAR(40) NOT NULL,
                NewRole VARCHAR(40) NOT NULL,
                ChangedBy VARCHAR(100) NOT NULL,
                ChangedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX IX_AdminRoleAudit_Employee (EmployeeID, ChangedAt)
            ) ENGINE=InnoDB", connection);
        audit.ExecuteNonQuery();
    }

    private static void UpdateFailureState(
        MySqlConnection connection, MySqlTransaction transaction, int failedAttempts, DateTime? lockedUntil)
    {
        using var command = new MySqlCommand(@"
            UPDATE OwnerSecuritySettings
            SET FailedAttempts=@FailedAttempts, LockedUntilUtc=@LockedUntil
            WHERE SettingsID=@ID", connection, transaction);
        command.Parameters.AddWithValue("@FailedAttempts", failedAttempts);
        command.Parameters.AddWithValue("@LockedUntil", lockedUntil.HasValue
            ? lockedUntil.Value
            : DBNull.Value);
        command.Parameters.AddWithValue("@ID", SettingsId);
        command.ExecuteNonQuery();
    }

    private static byte[] DeriveHash(string passcode, byte[] salt, int iterations)
    {
        byte[] passcodeBytes = Encoding.UTF8.GetBytes(passcode);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                passcodeBytes, salt, iterations, HashAlgorithmName.SHA256, HashSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passcodeBytes);
        }
    }

    private static DateTime ToUtcDateTime(object value)
    {
        DateTime dateTime = Convert.ToDateTime(value);
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}
