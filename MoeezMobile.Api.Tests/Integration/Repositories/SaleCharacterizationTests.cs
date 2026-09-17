using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;
using MoeezMobile.Api.Tests.Fixtures;

namespace MoeezMobile.Api.Tests.Integration.Repositories;

/// <summary>
/// CHARACTERIZATION (task T034) — pins what <see cref="SaleRepository.CreateAsync"/> produces
/// today, before any of its arithmetic is extracted into Domain calculators.
///
/// These tests deliberately assert current behaviour rather than desired behaviour. Their job
/// is to fail loudly if the extraction changes any stored figure. After the move they must
/// still pass, unchanged — that is the proof that the refactor preserved behaviour, and it is
/// the bounded exception to test-first recorded in plan.md § Complexity Tracking.
///
/// Where the pinned behaviour looks wrong, it is called out in a comment rather than
/// corrected here. Fixing it is a separate, stated change under FR-020 (task T117).
/// </summary>
public class SaleCharacterizationTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private ISaleRepository Sales => new SaleRepository(Connections);

    private static CreateSaleDto Sale(decimal discount, params CreateSaleItemDto[] items) => new()
    {
        SaleDate = new DateTime(2026, 8, 31),
        CustomerName = UrduFixtures.CustomerName,
        SaleType = 1,
        Discount = discount,
        PaymentMethod = 1,
        Items = items.ToList()
    };

    private static CreateSaleItemDto Line(int productId, int qty, decimal unitPrice) =>
        new() { ProductId = productId, Quantity = qty, UnitPrice = unitPrice };

    [Fact]
    public async Task Totals_ForASingleLine_AreSubTotalLessDiscount()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(
            conn, UrduFixtures.ProductCharger, purchasePrice: 320m, retailPrice: 450m, stock: 10);

        var saleId = await Sales.CreateAsync(Sale(0m, Line(productId, 2, 450m)), userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);
        header.SubTotal.Should().Be(900m);
        header.TotalAmount.Should().Be(900m);
        header.TotalCost.Should().Be(640m);
        header.TotalProfit.Should().Be(260m);
    }

    [Fact]
    public async Task Discount_ReducesBothTheAmountTakenAndTheProfit()
    {
        // PINNED: the discount comes out of profit as well as out of the amount charged —
        // the shop absorbs it rather than passing it to cost. This is deliberate existing
        // behaviour and the extraction must reproduce it exactly.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(
            conn, UrduFixtures.ProductCharger, purchasePrice: 320m, retailPrice: 450m, stock: 10);

        var saleId = await Sales.CreateAsync(Sale(100m, Line(productId, 2, 450m)), userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);
        header.SubTotal.Should().Be(900m, "subtotal is before discount");
        header.TotalAmount.Should().Be(800m);
        header.TotalCost.Should().Be(640m, "cost is unaffected by the discount");
        header.TotalProfit.Should().Be(160m, "260 profit less the 100 discount");
    }

    [Fact]
    public async Task Totals_AcrossMultipleLines_SumEachLine()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var charger = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);
        var handsFree = await TestData.InsertProductAsync(conn, UrduFixtures.ProductHandsFree, 150m, 250m, 10);

        var saleId = await Sales.CreateAsync(
            Sale(0m, Line(charger, 2, 450m), Line(handsFree, 3, 250m)), userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);
        header.SubTotal.Should().Be(1650m);
        header.TotalCost.Should().Be(1090m);
        header.TotalProfit.Should().Be(560m);
    }

    [Fact]
    public async Task DuplicateLinesForTheSameProductAndPrice_AreMerged()
    {
        // The repository groups by (ProductId, UnitPrice) so the stock guard sees the true
        // total. Two lines of 3 become one line of 6.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);

        var saleId = await Sales.CreateAsync(
            Sale(0m, Line(productId, 3, 450m), Line(productId, 3, 450m)), userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);
        header.SubTotal.Should().Be(2700m);
        (await TestData.ReadStockAsync(conn, productId)).Should().Be(4);
        (await TestData.CountAsync(conn, "SaleItems")).Should().Be(1, "the two lines merged into one");
    }

    [Fact]
    public async Task SaleAtAPriceBelowCost_RecordsNegativeProfitWithoutClamping()
    {
        // A loss-making sale is a real business event. Profit must be reported negative,
        // never floored at zero, or the shop's reports would overstate earnings.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);

        var saleId = await Sales.CreateAsync(Sale(0m, Line(productId, 1, 300m)), userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);
        header.TotalProfit.Should().Be(-20m);
    }

    [Fact]
    public async Task LineTotalRequiringRounding_IsStoredAtTwoDecimalPlaces()
    {
        // 3 × 33.335 = 100.005. MySQL rounds DECIMAL(18,2) away from zero on write, so the
        // stored value is 100.01. The extracted calculator has to agree with this exactly,
        // or the receipt and the report will disagree by a paisa.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 20m, 33.335m, 10);

        var saleId = await Sales.CreateAsync(Sale(0m, Line(productId, 3, 33.335m)), userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);
        header.SubTotal.Should().Be(100.01m);
        header.TotalAmount.Should().Be(100.01m);
    }

    [Fact]
    public async Task StockIsReducedByTheQuantitySold()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);

        await Sales.CreateAsync(Sale(0m, Line(productId, 4, 450m)), userId);

        (await TestData.ReadStockAsync(conn, productId)).Should().Be(6);
    }

    [Fact]
    public async Task SaleForExactlyTheRemainingStock_IsAllowedAndEmptiesIt()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 5);

        await Sales.CreateAsync(Sale(0m, Line(productId, 5, 450m)), userId);

        (await TestData.ReadStockAsync(conn, productId)).Should().Be(0);
    }

    [Fact]
    public async Task SaleForOneUnitBeyondStock_IsRejectedAndLeavesNothingBehind()
    {
        // The boundary that matters most, and the one the atomic SQL guard exists for.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 5);

        var act = async () => await Sales.CreateAsync(Sale(0m, Line(productId, 6, 450m)), userId);

        await act.Should().ThrowAsync<Exception>();

        (await TestData.ReadStockAsync(conn, productId)).Should().Be(5, "stock is untouched");
        (await TestData.CountAsync(conn, "Sales")).Should().Be(0, "the transaction rolled back");
        (await TestData.CountAsync(conn, "SaleItems")).Should().Be(0);
        (await TestData.CountAsync(conn, "StockLedger")).Should().Be(0);
    }

    [Fact]
    public async Task EveryLineWritesAStockLedgerRowCarryingTheBalanceAfter()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);

        await Sales.CreateAsync(Sale(0m, Line(productId, 3, 450m)), userId);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT QtyOut, BalanceAfter, Notes FROM StockLedger;";
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue("a ledger row must exist for the movement");
        reader.GetInt32(0).Should().Be(3);
        reader.GetInt32(1).Should().Be(7);
        reader.GetString(2).Should().Be(UrduFixtures.LedgerNoteSale, "the ledger note is Urdu text");
    }

    [Fact]
    public async Task InvoiceNumber_FollowsTheDailySequenceFormat()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);

        var firstId = await Sales.CreateAsync(Sale(0m, Line(productId, 1, 450m)), userId);
        var secondId = await Sales.CreateAsync(Sale(0m, Line(productId, 1, 450m)), userId);

        var first = await TestData.ReadSaleHeaderAsync(conn, firstId);
        var second = await TestData.ReadSaleHeaderAsync(conn, secondId);

        first.InvoiceNo.Should().Be("SAL-20260831-001");
        second.InvoiceNo.Should().Be("SAL-20260831-002");
    }

    [Fact]
    public async Task DiscountExceedingSubTotal_PinsCurrentBehaviour()
    {
        // OPEN BOUNDARY (task T036 / T117): nothing currently rejects a discount larger than
        // the subtotal. This test records what actually happens so the extraction reproduces
        // it, and so the decision to keep or reject it is made deliberately rather than by
        // accident. data-model.md flags this as undecided.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);

        var saleId = await Sales.CreateAsync(Sale(1000m, Line(productId, 1, 450m)), userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);

        header.SubTotal.Should().Be(450m);
        header.TotalAmount.Should().Be(-550m,
            "PINNED, NOT ENDORSED: an over-discount currently yields a negative amount due");
        header.TotalProfit.Should().Be(-870m);
    }

    [Fact]
    public async Task UrduCustomerName_SurvivesTheWriteUnchanged()
    {
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);

        var saleId = await Sales.CreateAsync(Sale(0m, Line(productId, 1, 450m)), userId);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CustomerName FROM Sales WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", saleId);

        ((string?)await cmd.ExecuteScalarAsync()).Should().Be(UrduFixtures.CustomerName);
    }

    [Fact]
    public async Task InvoiceSequence_PastTheThreeDigitFormat_PinsCurrentBehaviour()
    {
        // OPEN BOUNDARY (task T036 / T117): the daily format is SAL-yyyyMMdd-NNN with three
        // digits of padding, and nobody has ever exercised what happens on the 1000th sale
        // of a single day. This test establishes the answer rather than leaving it to be
        // discovered on a busy Eid weekend.
        await using var conn = await Fixture.OpenAsync();
        var userId = await TestData.InsertUserAsync(conn);
        var productId = await TestData.InsertProductAsync(conn, UrduFixtures.ProductCharger, 320m, 450m, 10);

        // Advance the counter to 999 so the next allocation crosses the boundary.
        await using (var seed = conn.CreateCommand())
        {
            seed.CommandText = """
                INSERT INTO DocumentCounters (DocType, CounterDate, LastNumber)
                VALUES ('SAL', @date, 999)
                ON DUPLICATE KEY UPDATE LastNumber = 999;
                """;
            seed.Parameters.AddWithValue("@date", new DateTime(2026, 8, 31));
            await seed.ExecuteNonQueryAsync();
        }

        var saleId = await Sales.CreateAsync(Sale(0m, Line(productId, 1, 450m)), userId);

        var header = await TestData.ReadSaleHeaderAsync(conn, saleId);

        // PINNED: D3 formatting does not truncate — it widens. The invoice number grows to
        // four digits rather than wrapping or colliding, so uniqueness holds. Column width is
        // VARCHAR(30), so there is ample room.
        header.InvoiceNo.Should().Be("SAL-20260831-1000");
    }
}
