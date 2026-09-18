using Microsoft.Data.SqlClient;
using MoeezMobile.Api.Tests.Fixtures;

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
        // The shop's live database is on this same SQL Server instance, and this suite empties
        // every table between tests. This assertion is the last line of defence.
        TestDatabase.SchemaName.Should().NotBe("asynctxc_ElectronicAcces");
        TestDatabase.SchemaName.Should().EndWith("test");
    }

    [Fact]
    public async Task Schema_AppliedFromProductionSchemaFile_CreatesEveryTable()
    {
        // Applied from db/schema.sql, the same file production uses, so the two cannot drift.
        await using var conn = await Fixture.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES "
            + "WHERE TABLE_CATALOG = @db AND TABLE_TYPE = 'BASE TABLE';";
        cmd.Parameters.AddWithValue("@db", TestDatabase.SchemaName);

        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) tables.Add(reader.GetString(0));

        // SQL Server identifier comparison follows the database collation, which is
        // case-insensitive by default. Folded here so the assertion does not depend on that
        // default holding.
        tables.Select(t => t.ToLowerInvariant()).Should().Contain(
            ["products", "sales", "saleitems", "purchases", "purchaseitems",
             "stockledger", "users", "documentcounters"]);
    }

    [Fact]
    public async Task UrduColumns_AreNVarchar()
    {
        // The MySQL equivalent of this test asserted the schema's default charset was utf8mb4.
        // SQL Server has no per-database charset knob; what matters instead is that text
        // columns are NVARCHAR rather than VARCHAR. A VARCHAR column silently folds Urdu to
        // the server code page, which is the same defect in a different shape.
        await using var conn = await Fixture.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS "
            + "WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'Name';";

        var dataType = (string?)await cmd.ExecuteScalarAsync();

        dataType.Should().Be("nvarchar", "Urdu product names cannot survive a VARCHAR column");
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
        // a fake would return whatever it was handed. Only SQL Server can prove the column
        // type, the parameter type, and the collation all agree.
        await using var conn = await Fixture.OpenAsync();

        await using (var insert = conn.CreateCommand())
        {
            insert.CommandText = "INSERT INTO Brands (Name, IsActive) VALUES (@name, 1);";
            insert.Parameters.AddWithValue("@name", UrduFixtures.ProductCharger);
            await insert.ExecuteNonQueryAsync();
        }

        await using var read = conn.CreateCommand();
        read.CommandText = "SELECT TOP (1) Name FROM Brands;";
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
        read.CommandText = "SELECT TOP (1) Name FROM Brands;";
        var stored = (string?)await read.ExecuteScalarAsync();

        stored.Should().Be(hazard);
    }

    [Fact]
    public async Task DecimalColumns_RoundOnWriteAwayFromZero()
    {
        // Pins how SQL Server rounds a DECIMAL(18,2) write. Every C#-side rounding decision
        // has to agree with this, or the stored total and the calculated total silently
        // diverge. SQL Server rounds half away from zero on a decimal-to-decimal narrowing,
        // which is the same behaviour MySQL had here.
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
        read.CommandText = "SELECT TOP (1) PurchasePrice FROM Products;";
        var stored = (decimal)(await read.ExecuteScalarAsync())!;

        stored.Should().Be(33.34m, "SQL Server rounds DECIMAL away from zero on write");
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
    private static string Conn(string database) =>
        $"Server=.\\MSSQLSERVER2012;Database={database};User Id=Electronic;Password=x;"
        + "TrustServerCertificate=True";

    [Theory]
    [InlineData("asynctxc_ElectronicAcces")]   // the live shop database
    [InlineData("master")]
    [InlineData("production")]
    [InlineData("moeez")]
    [InlineData("moeez_testing")]              // close, but not the accepted shape
    public void SchemaNamesOutsideTheTestPattern_AreRejected(string database)
    {
        var reason = TestDatabase.Reject(Conn(database));

        reason.Should().NotBeNull(
            "the suite empties every table, and the live database is on the same server");
        reason.Should().Contain(database, "the message must name the database that was refused");
    }

    [Fact]
    public void RefusingTheLiveDatabase_SaysItIsAConfigurationMistakeNotAnOutage()
    {
        // The first version of this guard threw from a static constructor, so the refusal
        // surfaced as "SQL Server is not reachable". Someone aimed at the live database would
        // have gone off to restart a perfectly healthy server. The message has to distinguish
        // the two.
        var reason = TestDatabase.Reject(Conn("asynctxc_ElectronicAcces"));

        reason.Should().Contain("REFUSED");
        reason.Should().Contain("not an unreachable server");
        reason.Should().NotContain("not reachable at the configured");
    }

    [Theory]
    [InlineData("moeez_test")]
    [InlineData("moeez_test_ci")]
    [InlineData("MOEEZ_TEST")]
    [InlineData("asynctxc_ElectronicAcces_test")]
    [InlineData("asynctxc_ElectronicAcces_test_ci")]
    public void TestSchemaNames_AreAccepted(string database)
    {
        TestDatabase.Reject(Conn(database)).Should().BeNull();
    }

    [Fact]
    public void TheLiveDatabaseAndItsTestTwin_AreTreatedDifferently()
    {
        // The two names differ by one suffix. Getting this pair wrong in either direction is
        // the mistake with the worst consequence in the whole suite, so it is pinned directly.
        TestDatabase.Reject(Conn("asynctxc_ElectronicAcces")).Should().NotBeNull();
        TestDatabase.Reject(Conn("asynctxc_ElectronicAcces_test")).Should().BeNull();
    }

    [Fact]
    public void AConnectionWithNoDatabaseNamed_IsRejected()
    {
        TestDatabase.Reject("Server=.\\MSSQLSERVER2012;User Id=Electronic;Password=x;")
            .Should().NotBeNull("an unnamed database could resolve to anything");
    }
}
