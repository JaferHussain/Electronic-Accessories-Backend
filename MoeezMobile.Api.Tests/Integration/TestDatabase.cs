using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace MoeezMobile.Api.Tests.Integration;

/// <summary>
/// Resolves and provisions the duplicate database every test runs against.
///
/// Tests run against a real SQL Server schema rather than in-memory substitutes, so that what
/// the suite proves is what SQL Server actually does: row locking, NVARCHAR collation,
/// UPDATE-then-INSERT upsert behaviour, DECIMAL(18,2) rounding on write, and transaction
/// rollback. An in-memory stand-in agrees with none of those.
///
/// The schema is applied from db/schema.sql — the same file production uses — so test and
/// production schemas cannot drift.
/// </summary>
public static class TestDatabase
{
    /// <summary>Environment variable that overrides the default local connection.</summary>
    public const string ConnectionEnvVar = "MOEEZ_TEST_CONNECTION";

    private const string DefaultConnection =
        "Server=.\\MSSQLSERVER2012;Database=moeez_test;Integrated Security=True;"
        + "TrustServerCertificate=True";

    /// <summary>
    /// A database is only usable as a test target if its name says so. The shop's live
    /// database sits on this same SQL Server instance, and a truncating test suite pointed at
    /// it would empty the shop's ledger. This pattern is the thing standing between the two.
    ///
    /// The live database is asynctxc_ElectronicAcces; note that 'asynctxc_ElectronicAcces_test'
    /// is accepted while the live name itself is not, so a test database may sit beside it.
    /// </summary>
    private static readonly Regex AllowedDatabaseName =
        new(@"^(moeez_test(_[a-z0-9]+)?|asynctxc_ElectronicAcces_test(_[a-z0-9]+)?)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly SemaphoreSlim ProvisionLock = new(1, 1);
    private static bool _provisioned;

    private static readonly (string? Resolved, string? Rejection) Guarded = ResolveAndGuard();

    /// <summary>
    /// Why the configured connection was refused, or null when it was accepted.
    ///
    /// Deliberately a value rather than an exception thrown from a static constructor: a
    /// throwing initializer surfaces as TypeInitializationException wrapped inside whatever
    /// catch happens to see it first, and the real reason gets reported as "SQL Server is not
    /// reachable". Someone pointed at the wrong database would then go restart a healthy
    /// server instead of learning what they actually did wrong.
    /// </summary>
    public static string? RejectionReason => Guarded.Rejection;

    /// <summary>Connection string for the duplicate database, after the safety check.</summary>
    public static string ConnectionString =>
        Guarded.Resolved ?? throw new InvalidOperationException(Guarded.Rejection);

    /// <summary>Database name in use, for diagnostics and assertions.</summary>
    public static string SchemaName =>
        Guarded.Resolved is null
            ? "<refused>"
            : new SqlConnectionStringBuilder(Guarded.Resolved).InitialCatalog;

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
        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            return $"REFUSED — the test connection string could not be parsed: {ex.Message}";
        }

        var database = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(database))
            return $"REFUSED — the {ConnectionEnvVar} connection string names no database. "
                   + "A suite without an explicit database could truncate the wrong one.";

        if (!AllowedDatabaseName.IsMatch(database))
            return $"REFUSED — will not run against database '{database}'. This suite deletes "
                   + "every row between tests, and the shop's live database "
                   + "(asynctxc_ElectronicAcces) is on this same server. Only 'moeez_test', "
                   + "'asynctxc_ElectronicAcces_test' or a '_<suffix>' variant of either is "
                   + $"accepted. This is a configuration mistake, not an unreachable server — "
                   + $"set {ConnectionEnvVar} to a duplicate database.";

        return null;
    }

    /// <summary>
    /// True when the SQL Server instance is reachable. When it is not, the integration tier
    /// skips loudly rather than failing the build for a missing local prerequisite — and never
    /// passes silently.
    /// </summary>
    public static async Task<(bool Available, string? Reason)> ProbeAsync()
    {
        // A refused configuration is reported as itself, never as an unreachable server.
        if (RejectionReason is not null) return (false, RejectionReason);

        try
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master",
                ConnectTimeout = 5
            };

            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"SQL Server is not reachable at the configured test connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the duplicate database and applies db/schema.sql. Idempotent and safe to call
    /// from every fixture; the work happens once per test run.
    /// </summary>
    public static async Task EnsureProvisionedAsync()
    {
        if (_provisioned) return;

        await ProvisionLock.WaitAsync();
        try
        {
            if (_provisioned) return;

            var masterOnly = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;

            await using (var server = new SqlConnection(masterOnly))
            {
                await server.OpenAsync();
                await using var create = server.CreateCommand();
                // The name is already constrained by AllowedDatabaseName, so bracket-quoting it
                // is sufficient here; it can never be arbitrary caller input.
                create.CommandText =
                    $"IF DB_ID(N'{SchemaName}') IS NULL CREATE DATABASE [{SchemaName}];";
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
        // statements. Those two lines are redirected at the duplicate database; everything
        // else — every column type, collation, index, and constraint — is applied verbatim,
        // which is what makes drift between test and production impossible.
        schemaSql = Regex.Replace(
            schemaSql,
            @"IF\s+DB_ID\(N'asynctxc_ElectronicAcces'\)\s+IS\s+NULL\s+CREATE\s+DATABASE\s+\[asynctxc_ElectronicAcces\]\s*;",
            $"IF DB_ID(N'{SchemaName}') IS NULL CREATE DATABASE [{SchemaName}];",
            RegexOptions.IgnoreCase);

        schemaSql = Regex.Replace(
            schemaSql, @"USE\s+\[asynctxc_ElectronicAcces\]\s*;", $"USE [{SchemaName}];",
            RegexOptions.IgnoreCase);

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        // SqlClient has no batch parser, so GO separators are split here the way sqlcmd would.
        foreach (var batch in SplitOnGo(schemaSql))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = batch;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Splits a script on its GO separators. GO is a client directive, not T-SQL: sending a
    /// script containing it straight to the server is a syntax error.
    /// </summary>
    private static IEnumerable<string> SplitOnGo(string script)
    {
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return batches.Where(b => !string.IsNullOrWhiteSpace(b));
    }

    /// <summary>
    /// Empties every table so each test starts from a known state. Shared mutable state
    /// across tests would make results order-dependent, which the conventions forbid.
    /// </summary>
    public static async Task ResetAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var tables = new List<string>();
        await using (var read = conn.CreateCommand())
        {
            read.CommandText =
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES "
                + "WHERE TABLE_CATALOG = @db AND TABLE_TYPE = 'BASE TABLE';";
            read.Parameters.AddWithValue("@db", SchemaName);

            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }

        if (tables.Count == 0) return;

        // TRUNCATE is refused on a table referenced by a foreign key even with constraints
        // disabled, so rows are deleted and the identity counters reset explicitly — which is
        // what TRUNCATE was providing under MySQL.
        await using var wipe = conn.CreateCommand();
        wipe.CommandText =
            string.Concat(tables.Select(t => $"ALTER TABLE [{t}] NOCHECK CONSTRAINT ALL; "))
            + string.Concat(tables.Select(t => $"DELETE FROM [{t}]; "))
            + string.Concat(tables.Select(t =>
                $"IF OBJECTPROPERTY(OBJECT_ID('[{t}]'), 'TableHasIdentity') = 1 "
                + $"DBCC CHECKIDENT('[{t}]', RESEED, 0) WITH NO_INFOMSGS; "))
            + string.Concat(tables.Select(t => $"ALTER TABLE [{t}] WITH CHECK CHECK CONSTRAINT ALL; "));
        await wipe.ExecuteNonQueryAsync();
    }

    /// <summary>Opens a connection to the duplicate database.</summary>
    public static async Task<SqlConnection> OpenAsync()
    {
        var conn = new SqlConnection(ConnectionString);
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
