using Microsoft.Data.SqlClient;

namespace MoeezMobile.Api.Data;

public interface IDbConnectionFactory
{
    SqlConnection CreateConnection();

    /// <summary>Creates and opens a connection.</summary>
    Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
