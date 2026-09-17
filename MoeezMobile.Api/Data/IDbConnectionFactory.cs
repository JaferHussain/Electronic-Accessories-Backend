using MySqlConnector;

namespace MoeezMobile.Api.Data;

public interface IDbConnectionFactory
{
    MySqlConnection CreateConnection();

    /// <summary>Creates and opens a connection.</summary>
    Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
