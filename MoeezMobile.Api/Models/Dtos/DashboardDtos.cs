namespace MoeezMobile.Api.Models.Dtos;

public class DashboardSummaryDto
{
    public int TotalProducts { get; set; }
    public int TotalStockQty { get; set; }
    public decimal StockValueAtCost { get; set; }
    public decimal TodayPurchaseAmount { get; set; }
    public decimal TodaySaleAmount { get; set; }
    public decimal TodayProfit { get; set; }
    public decimal MonthSaleAmount { get; set; }
    public decimal MonthProfit { get; set; }
    public int LowStockCount { get; set; }
}

public class LowStockItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? BrandName { get; set; }
    public string? ImagePath { get; set; }
    public int QuantityInStock { get; set; }
    public int LowStockThreshold { get; set; }
}

public class RecentSaleDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CustomerName { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalProfit { get; set; }
}

public class SalesChartPointDto
{
    public DateTime Date { get; set; }
    public decimal SaleAmount { get; set; }
    public decimal Profit { get; set; }
    public int InvoiceCount { get; set; }
}
