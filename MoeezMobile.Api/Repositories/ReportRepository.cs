using Dapper;
using MoeezMobile.Api.Data;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;

namespace MoeezMobile.Api.Repositories;

public interface IReportRepository
{
    Task<ReportResult<DailySalesRowDto>> DailySalesAsync(DateTime date);
    Task<ReportResult<MonthlySalesRowDto>> MonthlySalesAsync(int year, int month);
    Task<ReportResult<DailySalesRowDto>> SalesSummaryAsync(DateTime from, DateTime to);
    Task<ReportResult<PurchaseSummaryRowDto>> PurchaseSummaryAsync(DateTime from, DateTime to);
    Task<ReportResult<ProfitByProductRowDto>> ProfitByProductAsync(DateTime from, DateTime to);
    Task<ReportResult<CurrentStockRowDto>> CurrentStockAsync();
    Task<ReportResult<CurrentStockRowDto>> LowStockAsync();
    Task<ReportResult<StockHistoryRowDto>> StockHistoryAsync(int? productId, DateTime? from, DateTime? to);
}

public class ReportRepository : IReportRepository
{
    private readonly IDbConnectionFactory _factory;

    public ReportRepository(IDbConnectionFactory factory) => _factory = factory;

    // ---------------- Sales ----------------

    public async Task<ReportResult<DailySalesRowDto>> DailySalesAsync(DateTime date)
        => await SalesRowsAsync(date.Date, date.Date, MessageKeys.ReportDailySales);

    public async Task<ReportResult<DailySalesRowDto>> SalesSummaryAsync(DateTime from, DateTime to)
        => await SalesRowsAsync(from.Date, to.Date, MessageKeys.ReportSalesSummary);

    private async Task<ReportResult<DailySalesRowDto>> SalesRowsAsync(DateTime from, DateTime to, string title)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = (await conn.QueryAsync<DailySalesRowDto>(@"
            SELECT h.Id AS SaleId, h.InvoiceNo, h.SaleDate, h.CreatedAt, h.CustomerName,
                   ISNULL((SELECT SUM(Quantity) FROM SaleItems WHERE SaleId = h.Id), 0) AS TotalQuantity,
                   h.SubTotal, h.Discount, h.TotalAmount, h.TotalCost, h.TotalProfit,
                   h.PaymentMethod
            FROM Sales h
            WHERE h.IsVoided = 0 AND h.SaleDate BETWEEN @From AND @To
            ORDER BY h.SaleDate, h.Id;", new { From = from, To = to })).ToList();

        // PaymentMethodText / TxnTypeText are filled in by the controller, which knows the
        // caller's language. The repository stays language-agnostic.
        return new ReportResult<DailySalesRowDto>
        {
            Title = title,
            From = from,
            To = to,
            Rows = rows,
            Summary = new Dictionary<string, decimal>
            {
                ["invoiceCount"] = rows.Count,
                ["totalQuantity"] = rows.Sum(r => r.TotalQuantity),
                ["totalAmount"] = rows.Sum(r => r.TotalAmount),
                ["totalCost"] = rows.Sum(r => r.TotalCost),
                ["totalProfit"] = rows.Sum(r => r.TotalProfit),
                ["totalDiscount"] = rows.Sum(r => r.Discount)
            }
        };
    }

    /// <summary>Day-by-day breakdown of one month.</summary>
    public async Task<ReportResult<MonthlySalesRowDto>> MonthlySalesAsync(int year, int month)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = (await conn.QueryAsync<MonthlySalesRowDto>(@"
            SELECT CONVERT(char(10), h.SaleDate, 23) AS Period,
                   COUNT(*) AS InvoiceCount,
                   ISNULL(SUM((SELECT SUM(Quantity) FROM SaleItems WHERE SaleId = h.Id)), 0) AS TotalQuantity,
                   SUM(h.TotalAmount) AS TotalAmount,
                   SUM(h.TotalCost)   AS TotalCost,
                   SUM(h.TotalProfit) AS TotalProfit
            FROM Sales h
            WHERE h.IsVoided = 0 AND h.SaleDate BETWEEN @From AND @To
            GROUP BY CONVERT(char(10), h.SaleDate, 23)
            ORDER BY Period;", new { From = from, To = to })).ToList();

        return new ReportResult<MonthlySalesRowDto>
        {
            Title = MessageKeys.ReportMonthlySales,
            From = from,
            To = to,
            Rows = rows,
            Summary = new Dictionary<string, decimal>
            {
                ["invoiceCount"] = rows.Sum(r => r.InvoiceCount),
                ["totalQuantity"] = rows.Sum(r => r.TotalQuantity),
                ["totalAmount"] = rows.Sum(r => r.TotalAmount),
                ["totalCost"] = rows.Sum(r => r.TotalCost),
                ["totalProfit"] = rows.Sum(r => r.TotalProfit)
            }
        };
    }

    // ---------------- Purchases ----------------

    public async Task<ReportResult<PurchaseSummaryRowDto>> PurchaseSummaryAsync(DateTime from, DateTime to)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = (await conn.QueryAsync<PurchaseSummaryRowDto>(@"
            SELECT h.Id AS PurchaseId, h.InvoiceNo, h.PurchaseDate, s.Name AS SupplierName,
                   ISNULL((SELECT SUM(Quantity) FROM PurchaseItems WHERE PurchaseId = h.Id), 0) AS TotalQuantity,
                   h.SubTotal, h.Discount, h.TotalAmount, h.PaidAmount
            FROM Purchases h
            LEFT JOIN Suppliers s ON s.Id = h.SupplierId
            WHERE h.IsVoided = 0 AND h.PurchaseDate BETWEEN @From AND @To
            ORDER BY h.PurchaseDate, h.Id;",
            new { From = from.Date, To = to.Date })).ToList();

        return new ReportResult<PurchaseSummaryRowDto>
        {
            Title = MessageKeys.ReportPurchaseSummary,
            From = from.Date,
            To = to.Date,
            Rows = rows,
            Summary = new Dictionary<string, decimal>
            {
                ["invoiceCount"] = rows.Count,
                ["totalQuantity"] = rows.Sum(r => r.TotalQuantity),
                ["totalAmount"] = rows.Sum(r => r.TotalAmount),
                ["totalPaid"] = rows.Sum(r => r.PaidAmount),
                ["balance"] = rows.Sum(r => r.TotalAmount - r.PaidAmount)
            }
        };
    }

    // ---------------- Profit ----------------

    /// <summary>
    /// Profit per product from the frozen SaleItem snapshots, so changing a product's
    /// purchase price today never rewrites yesterday's figures.
    /// </summary>
    public async Task<ReportResult<ProfitByProductRowDto>> ProfitByProductAsync(DateTime from, DateTime to)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = (await conn.QueryAsync<ProfitByProductRowDto>(@"
            SELECT p.Id AS ProductId, p.Code, p.Name, p.Model,
                   b.Name AS BrandName, c.Name AS CategoryName,
                   SUM(si.Quantity)                  AS QuantitySold,
                   SUM(si.LineTotal)                 AS SaleAmount,
                   SUM(si.UnitCost * si.Quantity)    AS CostAmount,
                   SUM(si.LineProfit)                AS Profit
            FROM SaleItems si
            JOIN Sales h    ON h.Id = si.SaleId AND h.IsVoided = 0
            JOIN Products p ON p.Id = si.ProductId
            LEFT JOIN Brands     b ON b.Id = p.BrandId
            LEFT JOIN Categories c ON c.Id = p.CategoryId
            WHERE h.SaleDate BETWEEN @From AND @To
            GROUP BY p.Id, p.Code, p.Name, p.Model, b.Name, c.Name
            ORDER BY Profit DESC;",
            new { From = from.Date, To = to.Date })).ToList();

        return new ReportResult<ProfitByProductRowDto>
        {
            Title = MessageKeys.ReportProfitByProduct,
            From = from.Date,
            To = to.Date,
            Rows = rows,
            Summary = new Dictionary<string, decimal>
            {
                ["productCount"] = rows.Count,
                ["quantitySold"] = rows.Sum(r => r.QuantitySold),
                ["saleAmount"] = rows.Sum(r => r.SaleAmount),
                ["costAmount"] = rows.Sum(r => r.CostAmount),
                ["profit"] = rows.Sum(r => r.Profit)
            }
        };
    }

    // ---------------- Stock ----------------

    public Task<ReportResult<CurrentStockRowDto>> CurrentStockAsync() => StockAsync(false);
    public Task<ReportResult<CurrentStockRowDto>> LowStockAsync() => StockAsync(true);

    private async Task<ReportResult<CurrentStockRowDto>> StockAsync(bool lowOnly)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = (await conn.QueryAsync<CurrentStockRowDto>($@"
            SELECT p.Id AS ProductId, p.Code, p.Name, p.Model,
                   b.Name AS BrandName, c.Name AS CategoryName,
                   p.QuantityInStock, p.LowStockThreshold,
                   p.PurchasePrice, p.RetailPrice,
                   (p.QuantityInStock * p.PurchasePrice) AS StockValueAtCost,
                   (p.QuantityInStock * p.RetailPrice)   AS StockValueAtRetail
            FROM Products p
            LEFT JOIN Brands     b ON b.Id = p.BrandId
            LEFT JOIN Categories c ON c.Id = p.CategoryId
            WHERE p.IsActive = 1
              {(lowOnly ? "AND p.QuantityInStock <= p.LowStockThreshold" : string.Empty)}
            ORDER BY {(lowOnly ? "p.QuantityInStock, p.Name" : "p.Name")};")).ToList();

        return new ReportResult<CurrentStockRowDto>
        {
            Title = lowOnly ? MessageKeys.ReportLowStock : MessageKeys.ReportCurrentStock,
            Rows = rows,
            Summary = new Dictionary<string, decimal>
            {
                ["productCount"] = rows.Count,
                ["totalQuantity"] = rows.Sum(r => r.QuantityInStock),
                ["valueAtCost"] = rows.Sum(r => r.StockValueAtCost),
                ["valueAtRetail"] = rows.Sum(r => r.StockValueAtRetail),
                ["potentialProfit"] = rows.Sum(r => r.StockValueAtRetail - r.StockValueAtCost)
            }
        };
    }

    public async Task<ReportResult<StockHistoryRowDto>> StockHistoryAsync(int? productId, DateTime? from, DateTime? to)
    {
        var where = new List<string>();
        if (productId.HasValue) where.Add("l.ProductId = @ProductId");
        if (from.HasValue) where.Add("l.TxnDate >= @From");
        if (to.HasValue) where.Add("l.TxnDate < @ToExclusive");
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;

        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = (await conn.QueryAsync<StockHistoryRowDto>($@"
            SELECT TOP (5000)
                   l.Id, l.ProductId, p.Name AS ProductName, p.Code AS ProductCode,
                   l.TxnDate, l.TxnType, l.ReferenceNo, l.QtyIn, l.QtyOut, l.BalanceAfter, l.Notes
            FROM StockLedger l
            JOIN Products p ON p.Id = l.ProductId
            {whereSql}
            ORDER BY l.TxnDate DESC, l.Id DESC;",
            new { ProductId = productId, From = from?.Date, ToExclusive = to?.Date.AddDays(1) })).ToList();

        return new ReportResult<StockHistoryRowDto>
        {
            Title = MessageKeys.ReportStockHistory,
            From = from,
            To = to,
            Rows = rows,
            Summary = new Dictionary<string, decimal>
            {
                ["rowCount"] = rows.Count,
                ["totalIn"] = rows.Sum(r => r.QtyIn),
                ["totalOut"] = rows.Sum(r => r.QtyOut)
            }
        };
    }
}
