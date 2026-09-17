using MoeezMobile.Api.Data;
using MySqlConnector;

namespace MoeezMobile.Api.Tests.Integration;

/// <summary>
/// Provisions the duplicate database once per test run and resets it between tests.
///
/// Every test that touches data uses this. There are no in-memory repository substitutes in
/// this suite: a fake agrees with whatever the fake's author believed, while the duplicate
/// database agrees with MySQL. Locking, collation, rounding on write, and rollback are only
/// real against a real server.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    /// <summary>Set when the server could not be reached; tests report it and skip.</summary>
    public string? UnavailableReason { get; private set; }

    public bool IsAvailable => UnavailableReason is null;

    public async ValueTask InitializeAsync()
    {
        var (available, reason) = await TestDatabase.ProbeAsync();
        if (!available)
        {
            UnavailableReason = reason;
            return;
        }

        try
        {
            await TestDatabase.EnsureProvisionedAsync();
        }
        catch (Exception ex)
        {
            UnavailableReason = $"Could not provision schema '{TestDatabase.SchemaName}': {ex.Message}";
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Fails the test with a clear message when the database is missing, rather than letting
    /// it pass vacuously. A silently skipped test is indistinguishable from a passing one on
    /// a build summary, which is exactly how untested code reaches the shop.
    /// </summary>
    public void RequireDatabase()
    {
        Assert.SkipWhen(
            !IsAvailable,
            $"SKIPPED — integration tier needs MySQL. {UnavailableReason} "
            + $"Start MySQL, or set {TestDatabase.ConnectionEnvVar} to a duplicate database "
            + $"named 'moeez_test'.");
    }

    /// <summary>Empties every table. Call at the start of each test.</summary>
    public async Task ResetAsync()
    {
        RequireDatabase();
        await TestDatabase.ResetAsync();
    }

    public Task<MySqlConnection> OpenAsync() => TestDatabase.OpenAsync();

    /// <summary>
    /// Connection factory pointed at the duplicate database, so the real repository classes
    /// can be constructed and exercised exactly as they run in production.
    /// </summary>
    public IDbConnectionFactory ConnectionFactory { get; } = new TestConnectionFactory();

    private sealed class TestConnectionFactory : IDbConnectionFactory
    {
        public MySqlConnection CreateConnection() => new(TestDatabase.ConnectionString);

        public async Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
        {
            var conn = new MySqlConnection(TestDatabase.ConnectionString);
            await conn.OpenAsync(ct);
            return conn;
        }
    }
}

/// <summary>
/// Shares one provisioned database across all integration tests. Provisioning runs once;
/// per-test isolation comes from <see cref="DatabaseFixture.ResetAsync"/>, not from
/// rebuilding the schema each time.
/// </summary>
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}

/// <summary>
/// Base class for tests that use the duplicate database. Handles the skip check and the
/// per-test reset so no individual test can forget either.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait(TestCategories.Name, TestCategories.Integration)]
public abstract class DatabaseTestBase : IAsyncLifetime
{
    protected DatabaseTestBase(DatabaseFixture fixture) => Fixture = fixture;

    protected DatabaseFixture Fixture { get; }

    protected IDbConnectionFactory Connections => Fixture.ConnectionFactory;

    public async ValueTask InitializeAsync() => await Fixture.ResetAsync();

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
