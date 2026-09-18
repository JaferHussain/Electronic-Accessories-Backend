using Microsoft.Data.SqlClient;

namespace MoeezMobile.Api.Data;

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("DefaultConnection")
                  ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        // Force the options the whole codebase assumes, regardless of what appsettings.json contains.
        // Urdu text lives in NVARCHAR columns, so no server-side charset switch is needed here the
        // way it was under MySQL; what still has to be guaranteed is that a self-signed certificate
        // on the shop's SQL Server instance does not turn into a connection failure.
        var builder = new SqlConnectionStringBuilder(raw)
        {
            TrustServerCertificate = true
        };

        _connectionString = builder.ConnectionString;
    }

    public SqlConnection CreateConnection() => new(_connectionString);

    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
