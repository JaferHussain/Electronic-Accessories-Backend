using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;
using MoeezMobile.Api.Tests.Fixtures;

namespace MoeezMobile.Api.Tests.Integration.Repositories;

/// <summary>
/// CHARACTERIZATION (task T035) — pins what <see cref="PurchaseRepository.CreateAsync"/>
/// produces today, before its arithmetic moves into a Domain calculator.
///
/// As with the sale side, these assert current behaviour, not desired behaviour, and must
/// still pass unchanged after the extraction.
/// </summary>
public class PurchaseCharacterizationTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private IPurchaseRepository Purchases => new PurchaseRepository(Connections);
    private ISaleRepository Sales => new SaleRepository(Connections);

    private static CreatePurchaseDto Purchase(
        decimal discount, decimal paid, params CreatePurchaseItemDto[] items) => new()
    {
        PurchaseDate = new DateTime(2026, 8, 31),
        Discount = discount,
        PaidAmount = paid,
        Notes = UrduFixtures.SupplierName,
        Items = items.ToList()
    };

    private static CreatePurchaseItemDto Line(int productId, int qty, decimal unitCost) =>
        new() { ProductId = productId, Quantity = qty, UnitCost = unitCost };

    [Fact]
    public async Task Totals_ForASingleLine_AreSubTotalLessDiscount()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 0);

        var purchaseId = await Purchases.CreateAsync(
            Purchase(0m, 3200m, Line(productId, 10, 320m)), userId);

        var header = await TestData.ReadPurchaseHeaderAsync(conn, purchaseId);
        header.SubTotal.Should().Be(3200m);
        header.TotalAmount.Should().Be(3200m);
        header.PaidAmount.Should().Be(3200m);
    }

    [Fact]
    public async Task Discount_ReducesTheAmountOwed()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 0);

        var purchaseId = await Purchases.CreateAsync(
            Purchase(200m, 3000m, Line(productId, 10, 320m)), userId);

        var header = await TestData.ReadPurchaseHeaderAsync(conn, purchaseId);
        header.SubTotal.Should().Be(3200m);
        header.Discount.Should().Be(200m);
        header.TotalAmount.Should().Be(3000m);
    }

    [Fact]
    public async Task PartialPayment_IsRecordedWithoutAlteringTheTotal()
    {
        // A credit purchase: the shop owes the balance. Nothing should quietly reconcile
        // PaidAmount against TotalAmount.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 0);

        var purchaseId = await Purchases.CreateAsync(
            Purchase(0m, 1000m, Line(productId, 10, 320m)), userId);

        var header = await TestData.ReadPurchaseHeaderAsync(conn, purchaseId);
        header.TotalAmount.Should().Be(3200m);
        header.PaidAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task PaymentExceedingTheTotal_PinsCurrentBehaviour()
    {
        // OPEN BOUNDARY (task T036 / T117): nothing rejects overpayment. Recorded so the
        // decision to allow or reject it is made deliberately.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 0);

        var purchaseId = await Purchases.CreateAsync(
            Purchase(0m, 9999m, Line(productId, 10, 320m)), userId);

        var header = await TestData.ReadPurchaseHeaderAsync(conn, purchaseId);
        header.PaidAmount.Should().Be(9999m, "PINNED, NOT ENDORSED: overpayment is accepted as-is");
        header.TotalAmount.Should().Be(3200m);
    }

    [Fact]
    public async Task StockIsIncreasedByTheQuantityPurchased()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 4);

        await Purchases.CreateAsync(Purchase(0m, 0m, Line(productId, 10, 320m)), userId);

        (await TestData.ReadStockAsync(conn, productId)).Should().Be(14);
    }

    [Fact]
    public async Task PurchaseOverwritesTheProductsCostPrice()
    {
        // Consequential behaviour: a purchase replaces PurchasePrice outright rather than
        // averaging it. Every later sale's profit is computed against this new cost, so a
        // single mistyped unit cost silently re-prices the margin on existing stock.
        // PINNED as current behaviour; whether it should be a weighted average is a business
        // decision, not a refactor decision.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 10);

        await Purchases.CreateAsync(Purchase(0m, 0m, Line(productId, 5, 350m)), userId);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PurchasePrice FROM Products WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", productId);

        ((decimal)(await cmd.ExecuteScalarAsync())!).Should().Be(350m);
    }

    [Fact]
    public async Task NewCostAppliesToProfitOnSubsequentSales()
    {
        // Demonstrates the consequence of the previous test end to end.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 10);

        await Purchases.CreateAsync(Purchase(0m, 0m, Line(productId, 5, 350m)), userId);

        var saleId = await Sales.CreateAsync(new CreateSaleDto
        {
            SaleDate = new DateTime(2026, 8, 31),
            CustomerName = UrduFixtures.CustomerName,
            Discount = 0m,
            Items = [new CreateSaleItemDto { ProductId = productId, Quantity = 1, UnitPrice = 450m }]
        }, userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);
        header.TotalCost.Should().Be(350m, "the sale uses the cost the purchase just wrote");
        header.TotalProfit.Should().Be(100m);
    }

    [Fact]
    public async Task DuplicateLinesForTheSameProduct_AreNotMerged()
    {
        // ASYMMETRY WITH SALES, pinned deliberately: SaleRepository groups duplicate lines by
        // (ProductId, UnitPrice); PurchaseRepository does not. Two lines stay two rows, and
        // because each one overwrites PurchasePrice, the last line's cost wins.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 0);

        var purchaseId = await Purchases.CreateAsync(
            Purchase(0m, 0m, Line(productId, 3, 320m), Line(productId, 2, 340m)), userId);

        (await TestData.CountAsync(conn, "PurchaseItems")).Should().Be(2, "purchase lines are not merged");
        (await TestData.ReadStockAsync(conn, productId)).Should().Be(5);

        var header = await TestData.ReadPurchaseHeaderAsync(conn, purchaseId);
        header.SubTotal.Should().Be(960m + 680m);
    }

    [Fact]
    public async Task NegativeUnitCost_IsRejectedBeforeAnythingIsWritten()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 0);

        var act = async () => await Purchases.CreateAsync(
            Purchase(0m, 0m, Line(productId, 5, -10m)), userId);

        await act.Should().ThrowAsync<Exception>();
        (await TestData.CountAsync(conn, "Purchases")).Should().Be(0);
    }

    [Fact]
    public async Task PurchaseForAMissingProduct_RollsBackEntirely()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var realProduct = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 0);

        var act = async () => await Purchases.CreateAsync(
            Purchase(0m, 0m, Line(realProduct, 5, 320m), Line(999_999, 1, 100m)), userId);

        await act.Should().ThrowAsync<Exception>();

        (await TestData.CountAsync(conn, "Purchases")).Should().Be(0);
        (await TestData.CountAsync(conn, "PurchaseItems")).Should().Be(0);
        (await TestData.CountAsync(conn, "StockLedger")).Should().Be(0);
        (await TestData.ReadStockAsync(conn, realProduct)).Should().Be(0,
            "the first line's stock increase must be rolled back with the rest");
    }

    [Fact]
    public async Task LineTotalRequiringRounding_IsStoredAtTwoDecimalPlaces()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 0);

        var purchaseId = await Purchases.CreateAsync(
            Purchase(0m, 0m, Line(productId, 3, 33.335m)), userId);

        var header = await TestData.ReadPurchaseHeaderAsync(conn, purchaseId);
        header.SubTotal.Should().Be(100.01m);
    }

    [Fact]
    public async Task InvoiceNumber_UsesThePurchasePrefixAndItsOwnDailySequence()
    {
        // Sales and purchases keep independent counters for the same day.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 10);

        var firstId = await Purchases.CreateAsync(Purchase(0m, 0m, Line(productId, 1, 320m)), userId);
        var secondId = await Purchases.CreateAsync(Purchase(0m, 0m, Line(productId, 1, 320m)), userId);

        (await TestData.ReadPurchaseHeaderAsync(conn, firstId)).InvoiceNo.Should().Be("PUR-20260831-001");
        (await TestData.ReadPurchaseHeaderAsync(conn, secondId)).InvoiceNo.Should().Be("PUR-20260831-002");
    }

    [Fact]
    public async Task LedgerRowRecordsTheInboundMovementInUrdu()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 300m, 450m, 2);

        await Purchases.CreateAsync(Purchase(0m, 0m, Line(productId, 8, 320m)), userId);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT QtyIn, QtyOut, BalanceAfter, Notes FROM StockLedger;";
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt32(0).Should().Be(8);
        reader.GetInt32(1).Should().Be(0);
        reader.GetInt32(2).Should().Be(10);
        reader.GetString(3).Should().Be("خریداری");
    }
}
