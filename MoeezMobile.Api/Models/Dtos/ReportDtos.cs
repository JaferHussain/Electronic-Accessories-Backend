namespace MoeezMobile.Api.Models.Dtos;

/// <summary>Every report returns a summary strip plus rows.</summary>
public class ReportResult<TRow>
{
    public string Title { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Dictionary<string, decimal> Summary { get; set; } = new();
    public List<TRow> Rows { get; set; } = new();
}

public class DailySalesRowDto
{
    public int SaleId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CustomerName { get; set; }
    public int TotalQuantity { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public byte PaymentMethod { get; set; }
    public string PaymentMethodText { get; set; } = string.Empty;
}

public class MonthlySalesRowDto
{
    public string Period { get; set; } = string.Empty;   // yyyy-MM or a date for a day breakdown
    public int InvoiceCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
}

public class PurchaseSummaryRowDto
{
    public int PurchaseId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public string? SupplierName { get; set; }
    public int TotalQuantity { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
}

public class ProfitByProductRowDto
{
    public int ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? BrandName { get; set; }
    public string? CategoryName { get; set; }
    public int QuantitySold { get; set; }
    public decimal SaleAmount { get; set; }
    public decimal CostAmount { get; set; }
    public decimal Profit { get; set; }
}

public class CurrentStockRowDto
{
    public int ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? BrandName { get; set; }
    public string? CategoryName { get; set; }
    public int QuantityInStock { get; set; }
    public int LowStockThreshold { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal StockValueAtCost { get; set; }
    public decimal StockValueAtRetail { get; set; }
}

public class StockHistoryRowDto
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public DateTime TxnDate { get; set; }
    public byte TxnType { get; set; }
    public string TxnTypeText { get; set; } = string.Empty;
    public string? ReferenceNo { get; set; }
    public int QtyIn { get; set; }
    public int QtyOut { get; set; }
    public int BalanceAfter { get; set; }
    public string? Notes { get; set; }
}
