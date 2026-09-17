using MySqlConnector;

namespace MoeezMobile.Api.Data;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("DefaultConnection")
                  ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        // Force the options the whole codebase assumes, regardless of what appsettings.json contains.
        // TreatTinyAsBoolean=false: TINYINT(1) columns come back as sbyte/int, so IsActive / IsVoided
        // and the enum-like TxnType / SaleType / PaymentMethod columns all map consistently.
        var builder = new MySqlConnectionStringBuilder(raw)
        {
            CharacterSet = "utf8mb4",
            AllowUserVariables = true,
            ConvertZeroDateTime = true,
            TreatTinyAsBoolean = false
        };

        _connectionString = builder.ConnectionString;
    }

    public MySqlConnection CreateConnection() => new(_connectionString);

    public async Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
