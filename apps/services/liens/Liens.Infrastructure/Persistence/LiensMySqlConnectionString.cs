using MySqlConnector;

namespace Liens.Infrastructure.Persistence;

public static class LiensMySqlConnectionString
{
    /// <summary>
    /// Enables MySQL session variables used by restart-safe selling migrations.
    /// All callers use the same normalized connection string so runtime startup
    /// and design-time migration commands execute the same SQL behavior.
    /// </summary>
    public static string Configure(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true,
        };

        return builder.ConnectionString;
    }
}
