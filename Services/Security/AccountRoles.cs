using MySqlConnector;

namespace PAYROLL;

internal static class AccountRoles
{
    public const string Employee = "Employee";
    public const string HrCashManager = "HR";
    public const string TeamLeaderSupervisor = "Supervisor";

    public static bool IsHrCashManager(string? role)
        => role is not null && (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("HR", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("HR/Cash Manager", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("Cash Management", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("Cash Manager", StringComparison.OrdinalIgnoreCase));

    public static bool IsTeamLeaderSupervisor(string? role)
        => role is not null && (role.Equals("Supervisor", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("Team Leader", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("Team Leader/Supervisor", StringComparison.OrdinalIgnoreCase));

    public static string DisplayName(string? role)
    {
        if (IsHrCashManager(role)) return "HR / Cash Manager";
        if (IsTeamLeaderSupervisor(role)) return "Team Leader / Supervisor";
        return string.IsNullOrWhiteSpace(role) ? "No account yet" : "Employee";
    }

    // Older project databases may constrain Accounts.Role to ENUM('Admin','Employee').
    // Widen it only when role assignment is used so new canonical roles can be saved.
    public static void EnsureRoleColumnSupportsAssignments(string connectionString)
    {
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var inspect = new MySqlCommand(@"
            SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Accounts' AND COLUMN_NAME='Role'
            LIMIT 1", connection);
        using var reader = inspect.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("The Accounts.Role column could not be found.");

        string dataType = reader.GetString("DATA_TYPE");
        int length = reader.IsDBNull(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH"))
            ? 0
            : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH")));
        reader.Close();

        if (!dataType.Equals("enum", StringComparison.OrdinalIgnoreCase) && length >= 40)
            return;

        using var alter = new MySqlCommand(
            "ALTER TABLE Accounts MODIFY COLUMN Role VARCHAR(40) NOT NULL DEFAULT 'Employee'", connection);
        alter.ExecuteNonQuery();
    }
}
