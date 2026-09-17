using System.Text.RegularExpressions;
using MySqlConnector;

namespace MoeezMobile.Api.Tests.Integration;

/// <summary>
/// Resolves and provisions the duplicate database every test runs against.
///
/// Tests run against a real MySQL schema rather than in-memory substitutes, so that what the
/// suite proves is what MySQL actually does: row locking, utf8mb4 collation,
/// ON DUPLICATE KEY behaviour, DECIMAL(18,2) rounding on write, and transaction rollback.
/// An in-memory stand-in agrees with none of those.
///
/// The schema is applied from db/schema.sql — the same file production uses — so test and
/// production schemas cannot drift.
/// </summary>
public static class TestDatabase
{
    /// <summary>Environment variable that overrides the default local connection.</summary>
    public const string ConnectionEnvVar = "MOEEZ_TEST_CONNECTION";

    private const string DefaultConnection =
        "Server=localhost;Port=3306;Database=moeez_test;User ID=root;Password=;"
        + "CharSet=utf8mb4;AllowUserVariables=True;ConvertZeroDateTime=True;TreatTinyAsBoolean=False;";

    /// <summary>
    /// A schema is only usable as a test target if its name says so. The shop's live
    /// database sits on this same MySQL server, and a truncating test suite pointed at it
    /// would empty the shop's ledger. This pattern is the thing standing between the two.
    /// </summary>
    private static readonly Regex AllowedSchemaName =
        new(@"^moeez_test(_[a-z0-9]+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly SemaphoreSlim ProvisionLock = new(1, 1);
    private static bool _provisioned;

    private static readonly (string? Resolved, string? Rejection) Guarded = ResolveAndGuard();

    /// <summary>
    /// Why the configured connection was refused, or null when it was accepted.
    ///
    /// Deliberately a value rather than an exception thrown from a static constructor: a
    /// throwing initializer surfaces as TypeInitializationException wrapped inside whatever
    /// catch happens to see it first, and the real reason gets reported as "MySQL is not
    /// reachable". Someone pointed at the wrong schema would then go restart a healthy
    /// server instead of learning what they actually did wrong.
    /// </summary>
    public static string? RejectionReason => Guarded.Rejection;

    /// <summary>Connection string for the duplicate database, after the safety check.</summary>
    public static string ConnectionString =>
        Guarded.Resolved ?? throw new InvalidOperationException(Guarded.Rejection);

    /// <summary>Schema name in use, for diagnostics and assertions.</summary>
    public static string SchemaName =>
        Guarded.Resolved is null
            ? "<refused>"
            : new MySqlConnectionStringBuilder(Guarded.Resolved).Database;

    private static (string?, string?) ResolveAndGuard()
    {
        var raw = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        var connectionString = string.IsNullOrWhiteSpace(raw) ? DefaultConnection : raw;

        var rejection = Reject(connectionString);
        return rejection is null ? (connectionString, null) : (null, rejection);
    }

    /// <summary>
    /// Pure decision: why this connection string may not be used, or null if it may be.
    /// Exposed so the guard is testable without a server and without environment fiddling.
    /// </summary>
    public static string? Reject(string connectionString)
    {
        MySqlConnectionStringBuilder builder;
        try
        {
            builder = new MySqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            return $"REFUSED — the test connection string could not be parsed: {ex.Message}";
        }

        var database = builder.Database;

        if (string.IsNullOrWhiteSpace(database))
            return $"REFUSED — the {ConnectionEnvVar} connection string names no database. "
                   + "A suite without an explicit schema could truncate the wrong one.";

        if (!AllowedSchemaName.IsMatch(database))
            return $"REFUSED — will not run against schema '{database}'. This suite truncates "
                   + "every table between tests, and the shop's live database (moeez_mobile) is on "
                   + "this same server. Only 'moeez_test' or 'moeez_test_<suffix>' is accepted. "
                   + $"This is a configuration mistake, not an unreachable server — set "
                   + $"{ConnectionEnvVar} to a duplicate database.";

        // utf8mb4 end to end, or the Urdu round-trip tests would prove nothing.
        if (!string.Equals(builder.CharacterSet, "utf8mb4", StringComparison.OrdinalIgnoreCase))
            return $"REFUSED — the test connection must use CharSet=utf8mb4 (found "
                   + $"'{builder.CharacterSet}'). Anything else silently mangles Urdu text and "
                   + "hides the defects these tests exist to catch.";

        return null;
    }

    /// <summary>
    /// True when the MySQL server is reachable. When it is not, the integration tier skips
    /// loudly rather than failing the build for a missing local prerequisite — and never
    /// passes silently.
    /// </summary>
    public static async Task<(bool Available, string? Reason)> ProbeAsync()
    {
        // A refused configuration is reported as itself, never as an unreachable server.
        if (RejectionReason is not null) return (false, RejectionReason);

        try
        {
            var builder = new MySqlConnectionStringBuilder(ConnectionString)
            {
                Database = string.Empty,
                ConnectionTimeout = 5
            };

            await using var conn = new MySqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"MySQL is not reachable at the configured test connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the duplicate schema and applies db/schema.sql. Idempotent and safe to call
    /// from every fixture; the work happens once per test run.
    /// </summary>
    public static async Task EnsureProvisionedAsync()
    {
        if (_provisioned) return;

        await ProvisionLock.WaitAsync();
        try
        {
            if (_provisioned) return;

            var serverOnly = new MySqlConnectionStringBuilder(ConnectionString)
            {
                Database = string.Empty
            }.ConnectionString;

            await using (var server = new MySqlConnection(serverOnly))
            {
                await server.OpenAsync();
                await using var create = server.CreateCommand();
                create.CommandText =
                    $"CREATE DATABASE IF NOT EXISTS `{SchemaName}` "
                    + "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                await create.ExecuteNonQueryAsync();
            }

            await ApplySchemaAsync();
            _provisioned = true;
        }
        finally
        {
            ProvisionLock.Release();
        }
    }

    private static async Task ApplySchemaAsync()
    {
        var schemaSql = await File.ReadAllTextAsync(RepoPath("db", "schema.sql"));

        // schema.sql hardcodes the production database name in its CREATE DATABASE and USE
        // statements. Those two lines are redirected at the duplicate schema; everything
        // else — every column type, collation, index, and constraint — is applied verbatim,
        // which is what makes drift between test and production impossible.
        schemaSql = Regex.Replace(
            schemaSql,
            @"CREATE\s+DATABASE\s+IF\s+NOT\s+EXISTS\s+moeez_mobile",
            $"CREATE DATABASE IF NOT EXISTS `{SchemaName}`",
            RegexOptions.IgnoreCase);

        schemaSql = Regex.Replace(
            schemaSql, @"USE\s+moeez_mobile\s*;", $"USE `{SchemaName}`;", RegexOptions.IgnoreCase);

        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();

        // AllowUserVariables plus a multi-statement script: MySqlConnector runs the batch.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = schemaSql;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Empties every table so each test starts from a known state. Shared mutable state
    /// across tests would make results order-dependent, which the conventions forbid.
    /// </summary>
    public static async Task ResetAsync()
    {
        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();

        var tables = new List<string>();
        await using (var read = conn.CreateCommand())
        {
            read.CommandText =
                "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema "
                + "AND TABLE_TYPE = 'BASE TABLE';";
            read.Parameters.AddWithValue("@schema", SchemaName);

            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }

        if (tables.Count == 0) return;

        await using var truncate = conn.CreateCommand();
        truncate.CommandText =
            "SET FOREIGN_KEY_CHECKS = 0; "
            + string.Concat(tables.Select(t => $"TRUNCATE TABLE `{t}`; "))
            + "SET FOREIGN_KEY_CHECKS = 1;";
        await truncate.ExecuteNonQueryAsync();
    }

    /// <summary>Opens a connection to the duplicate database.</summary>
    public static async Task<MySqlConnection> OpenAsync()
    {
        var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Walks up from the test binary to the repository root so db/schema.sql is found
    /// regardless of build configuration or working directory.
    /// </summary>
    private static string RepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "db", "schema.sql")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate the repository root (searched upward for db/schema.sql).");

        return Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
    }
}
