using Dapper;
using MoeezMobile.Api.Data;
using MoeezMobile.Api.Models.Dtos;

namespace MoeezMobile.Api.Repositories;

public interface IDashboardRepository
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<IReadOnlyList<LowStockItemDto>> GetLowStockAsync(int take);
    Task<IReadOnlyList<RecentSaleDto>> GetRecentSalesAsync(int take);
    Task<IReadOnlyList<SalesChartPointDto>> GetSalesChartAsync(int days);
}

public class DashboardRepository : IDashboardRepository
{
    private readonly IDbConnectionFactory _factory;

    public DashboardRepository(IDbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// One round trip, several SELECTs, read back in order. Voided invoices are excluded
    /// everywhere so these numbers always agree with the reports for the same date.
    /// </summary>
    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        const string sql = @"
            SELECT COUNT(*) FROM Products WHERE IsActive = 1;

            SELECT IFNULL(SUM(QuantityInStock), 0) FROM Products WHERE IsActive = 1;

            SELECT IFNULL(SUM(QuantityInStock * PurchasePrice), 0) FROM Products WHERE IsActive = 1;

            SELECT IFNULL(SUM(TotalAmount), 0) FROM Purchases
             WHERE PurchaseDate = CURDATE() AND IsVoided = 0;

            SELECT IFNULL(SUM(TotalAmount), 0) FROM Sales
             WHERE SaleDate = CURDATE() AND IsVoided = 0;

            SELECT IFNULL(SUM(TotalProfit), 0) FROM Sales
             WHERE SaleDate = CURDATE() AND IsVoided = 0;

            SELECT IFNULL(SUM(TotalAmount), 0) FROM Sales
             WHERE DATE_FORMAT(SaleDate, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m') AND IsVoided = 0;

            SELECT IFNULL(SUM(TotalProfit), 0) FROM Sales
             WHERE DATE_FORMAT(SaleDate, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m') AND IsVoided = 0;

            SELECT COUNT(*) FROM Products
             WHERE IsActive = 1 AND QuantityInStock <= LowStockThreshold;";

        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var grid = await conn.QueryMultipleAsync(sql);

        return new DashboardSummaryDto
        {
            TotalProducts = await grid.ReadSingleAsync<int>(),
            TotalStockQty = await grid.ReadSingleAsync<int>(),
            StockValueAtCost = await grid.ReadSingleAsync<decimal>(),
            TodayPurchaseAmount = await grid.ReadSingleAsync<decimal>(),
            TodaySaleAmount = await grid.ReadSingleAsync<decimal>(),
            TodayProfit = await grid.ReadSingleAsync<decimal>(),
            MonthSaleAmount = await grid.ReadSingleAsync<decimal>(),
            MonthProfit = await grid.ReadSingleAsync<decimal>(),
            LowStockCount = await grid.ReadSingleAsync<int>()
        };
    }

    public async Task<IReadOnlyList<LowStockItemDto>> GetLowStockAsync(int take)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<LowStockItemDto>(@"
            SELECT p.Id, p.Code, p.Name, p.Model, b.Name AS BrandName, p.ImagePath,
                   p.QuantityInStock, p.LowStockThreshold
            FROM Products p
            LEFT JOIN Brands b ON b.Id = p.BrandId
            WHERE p.IsActive = 1 AND p.QuantityInStock <= p.LowStockThreshold
            ORDER BY p.QuantityInStock, p.Name
            LIMIT @Take;", new { Take = Math.Clamp(take, 1, 200) });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<RecentSaleDto>> GetRecentSalesAsync(int take)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<RecentSaleDto>(@"
            SELECT h.Id, h.InvoiceNo, h.SaleDate, h.CreatedAt, h.CustomerName,
                   IFNULL((SELECT SUM(Quantity) FROM SaleItems WHERE SaleId = h.Id), 0) AS TotalQuantity,
                   h.TotalAmount, h.TotalProfit
            FROM Sales h
            WHERE h.IsVoided = 0
            ORDER BY h.Id DESC
            LIMIT @Take;", new { Take = Math.Clamp(take, 1, 200) });
        return rows.ToList();
    }

    /// <summary>
    /// One point per calendar day for the last N days, including days with no sales
    /// (the chart should show the gap, not skip it).
    /// </summary>
    public async Task<IReadOnlyList<SalesChartPointDto>> GetSalesChartAsync(int days)
    {
        days = Math.Clamp(days, 1, 90);

        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = (await conn.QueryAsync<SalesChartPointDto>(@"
            SELECT SaleDate AS Date,
                   SUM(TotalAmount) AS SaleAmount,
                   SUM(TotalProfit) AS Profit,
                   COUNT(*)         AS InvoiceCount
            FROM Sales
            WHERE IsVoided = 0
              AND SaleDate > DATE_SUB(CURDATE(), INTERVAL @Days DAY)
              AND SaleDate <= CURDATE()
            GROUP BY SaleDate;", new { Days = days })).ToDictionary(r => r.Date.Date);

        var today = DateTime.Today;
        var points = new List<SalesChartPointDto>(days);
        for (var i = days - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            points.Add(rows.TryGetValue(date, out var found)
                ? found
                : new SalesChartPointDto { Date = date });
        }

        return points;
    }
}
