using MoeezMobile.Api.Tests.Fixtures;
using MySqlConnector;

namespace MoeezMobile.Api.Tests.Integration;

/// <summary>
/// Proves the duplicate database is real, isolated, and correctly guarded before any test
/// relies on it. If the provisioning is wrong, every downstream integration test is
/// meaningless — so this is checked first and explicitly.
/// </summary>
public class TestDatabaseProvisioningTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public void TargetSchema_IsNeverTheLiveDatabase()
    {
        // The shop's live database is on this same MySQL server, and this suite truncates
        // every table between tests. This assertion is the last line of defence.
        TestDatabase.SchemaName.Should().NotBe("moeez_mobile");
        TestDatabase.SchemaName.Should().StartWith("moeez_test");
    }

    [Fact]
    public async Task Schema_AppliedFromProductionSchemaFile_CreatesEveryTable()
    {
        // Applied from db/schema.sql, the same file production uses, so the two cannot drift.
        await using var conn = await Fixture.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT TABLE_NAME FROM information_schema.TABLES "
            + "WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE';";
        cmd.Parameters.AddWithValue("@schema", TestDatabase.SchemaName);

        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) tables.Add(reader.GetString(0));

        // MySQL on Windows runs with lower_case_table_names=1, so identifiers come back
        // folded to lowercase. Compared case-insensitively here because the schema is the
        // same file either way — but note this differs on Linux CI, where table names are
        // case-sensitive and the folding does not happen.
        tables.Select(t => t.ToLowerInvariant()).Should().Contain(
            ["products", "sales", "saleitems", "purchases", "purchaseitems",
             "stockledger", "users", "documentcounters"]);
    }

    [Fact]
    public async Task Schema_UsesUtf8mb4Collation()
    {
        // utf8 (3-byte) silently truncates 4-byte characters. The Urdu round-trip tests
        // would pass against a utf8 column while production data was being mangled.
        await using var conn = await Fixture.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT DEFAULT_CHARACTER_SET_NAME FROM information_schema.SCHEMATA "
            + "WHERE SCHEMA_NAME = @schema;";
        cmd.Parameters.AddWithValue("@schema", TestDatabase.SchemaName);

        var charset = (string?)await cmd.ExecuteScalarAsync();

        charset.Should().Be("utf8mb4");
    }

    [Fact]
    public async Task Reset_EmptiesEveryTableBetweenTests()
    {
        await using (var conn = await Fixture.OpenAsync())
        {
            await using var insert = conn.CreateCommand();
            insert.CommandText =
                "INSERT INTO Brands (Name, IsActive) VALUES (@name, 1);";
            insert.Parameters.AddWithValue("@name", UrduFixtures.SupplierName);
            await insert.ExecuteNonQueryAsync();
        }

        await TestDatabase.ResetAsync();

        await using var check = await Fixture.OpenAsync();
        await using var count = check.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Brands;";

        var remaining = Convert.ToInt32(await count.ExecuteScalarAsync());
        remaining.Should().Be(0, "each test must start from a known state (order independence)");
    }

    [Fact]
    public async Task UrduText_SurvivesAStoreAndRetrieveCycleUnchanged()
    {
        // The reason this suite uses a real database rather than an in-memory substitute:
        // a fake would return whatever it was handed. Only MySQL can prove the column,
        // the connection charset, and the collation all agree.
        await using var conn = await Fixture.OpenAsync();

        await using (var insert = conn.CreateCommand())
        {
            insert.CommandText = "INSERT INTO Brands (Name, IsActive) VALUES (@name, 1);";
            insert.Parameters.AddWithValue("@name", UrduFixtures.ProductCharger);
            await insert.ExecuteNonQueryAsync();
        }

        await using var read = conn.CreateCommand();
        read.CommandText = "SELECT Name FROM Brands LIMIT 1;";
        var stored = (string?)await read.ExecuteScalarAsync();

        stored.Should().Be(UrduFixtures.ProductCharger);
    }

    [Theory]
    [MemberData(nameof(UrduFixtures.AllEncodingHazards), MemberType = typeof(UrduFixtures))]
    public async Task EncodingHazards_SurviveStorageUnchanged(string hazard)
    {
        await using var conn = await Fixture.OpenAsync();

        await using (var insert = conn.CreateCommand())
        {
            insert.CommandText = "INSERT INTO Brands (Name, IsActive) VALUES (@name, 1);";
            insert.Parameters.AddWithValue("@name", hazard);
            await insert.ExecuteNonQueryAsync();
        }

        await using var read = conn.CreateCommand();
        read.CommandText = "SELECT Name FROM Brands LIMIT 1;";
        var stored = (string?)await read.ExecuteScalarAsync();

        stored.Should().Be(hazard);
    }

    [Fact]
    public async Task DecimalColumns_RoundOnWriteAwayFromZero()
    {
        // Pins how MySQL rounds a DECIMAL(18,2) write. Every C#-side rounding decision has to
        // agree with this, or the stored total and the calculated total silently diverge.
        await using var conn = await Fixture.OpenAsync();

        await using (var insert = conn.CreateCommand())
        {
            insert.CommandText =
                "INSERT INTO Products (Code, Name, PurchasePrice, WholesalePrice, RetailPrice, QuantityInStock) "
                + "VALUES (@code, @name, @price, 0, 0, 0);";
            insert.Parameters.AddWithValue("@code", "PRD-0001");
            insert.Parameters.AddWithValue("@name", UrduFixtures.ProductCharger);
            insert.Parameters.AddWithValue("@price", 33.335m);
            await insert.ExecuteNonQueryAsync();
        }

        await using var read = conn.CreateCommand();
        read.CommandText = "SELECT PurchasePrice FROM Products LIMIT 1;";
        var stored = (decimal)(await read.ExecuteScalarAsync())!;

        stored.Should().Be(33.34m, "MySQL rounds DECIMAL away from zero on write");
    }
}

/// <summary>
/// The safety guard is a pure decision about a connection string, so it is unit-testable
/// with no server. These call the real production guard rather than a copy of its rule — a
/// mirrored copy would keep passing after someone loosened the original.
/// </summary>
[Trait(TestCategories.Name, TestCategories.Unit)]
public class TestDatabaseGuardTests
{
    [Theory]
    [InlineData("moeez_mobile")]      // the live shop database
    [InlineData("mysql")]
    [InlineData("production")]
    [InlineData("moeez")]
    [InlineData("moeez_testing")]     // close, but not the accepted shape
    public void SchemaNamesOutsideTheTestPattern_AreRejected(string database)
    {
        var reason = TestDatabase.Reject(
            $"Server=localhost;Database={database};User ID=root;Password=;CharSet=utf8mb4;");

        reason.Should().NotBeNull(
            "the suite truncates every table, and the live database is on the same server");
        reason.Should().Contain(database, "the message must name the schema that was refused");
    }

    [Fact]
    public void RefusingTheLiveDatabase_SaysItIsAConfigurationMistakeNotAnOutage()
    {
        // The first version of this guard threw from a static constructor, so the refusal
        // surfaced as "MySQL is not reachable". Someone aimed at the live schema would have
        // gone off to restart a perfectly healthy server. The message has to distinguish
        // the two.
        var reason = TestDatabase.Reject(
            "Server=localhost;Database=moeez_mobile;User ID=root;Password=;CharSet=utf8mb4;");

        reason.Should().Contain("REFUSED");
        reason.Should().Contain("not an unreachable server");
        reason.Should().NotContain("not reachable at the configured");
    }

    [Theory]
    [InlineData("moeez_test")]
    [InlineData("moeez_test_ci")]
    [InlineData("MOEEZ_TEST")]
    public void TestSchemaNames_AreAccepted(string database)
    {
        TestDatabase.Reject(
            $"Server=localhost;Database={database};User ID=root;Password=;CharSet=utf8mb4;")
            .Should().BeNull();
    }

    [Fact]
    public void AConnectionWithNoDatabaseNamed_IsRejected()
    {
        TestDatabase.Reject("Server=localhost;User ID=root;Password=;CharSet=utf8mb4;")
            .Should().NotBeNull("an unnamed schema could resolve to anything");
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("latin1")]
    public void AConnectionThatIsNotUtf8mb4_IsRejected(string charset)
    {
        // utf8 is 3-byte in MySQL and silently truncates 4-byte characters. The Urdu
        // round-trip tests would pass while production data was being mangled.
        var reason = TestDatabase.Reject(
            $"Server=localhost;Database=moeez_test;User ID=root;Password=;CharSet={charset};");

        reason.Should().NotBeNull();
        reason.Should().Contain("utf8mb4");
    }
}
