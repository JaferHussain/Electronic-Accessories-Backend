using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Middleware;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;
using MoeezMobile.Api.Services;

namespace MoeezMobile.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.AdminOnly)]
public class ReportsController : ControllerBase
{
    private const string Money = "#,##0.00";

    private readonly IReportRepository _reports;
    private readonly IExcelExportService _excel;

    public ReportsController(IReportRepository reports, IExcelExportService excel)
    {
        _reports = reports;
        _excel = excel;
    }

    // ---------------- Sales ----------------

    [HttpGet("daily-sales")]
    public async Task<IActionResult> DailySales([FromQuery] DateTime? date, [FromQuery] string? export)
        => Respond(await _reports.DailySalesAsync(date ?? DateTime.Today), SalesColumns, export, "daily-sales");

    [HttpGet("sales-summary")]
    public async Task<IActionResult> SalesSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? export)
        => Respond(await _reports.SalesSummaryAsync(From(from), To(to)), SalesColumns, export, "sales-summary");

    [HttpGet("monthly-sales")]
    public async Task<IActionResult> MonthlySales([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? export)
    {
        var y = year ?? DateTime.Today.Year;
        var m = month ?? DateTime.Today.Month;
        if (m < 1 || m > 12) throw new BusinessException(MessageKeys.InvalidMonth);

        return Respond(await _reports.MonthlySalesAsync(y, m), MonthlyColumns, export, $"monthly-sales-{y}-{m:D2}");
    }

    // ---------------- Purchases ----------------

    [HttpGet("purchase-summary")]
    public async Task<IActionResult> PurchaseSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? export)
        => Respond(await _reports.PurchaseSummaryAsync(From(from), To(to)), PurchaseColumns, export, "purchase-summary");

    // ---------------- Profit ----------------

    [HttpGet("profit-by-product")]
    public async Task<IActionResult> ProfitByProduct([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? export)
        => Respond(await _reports.ProfitByProductAsync(From(from), To(to)), ProfitColumns, export, "profit-by-product");

    // ---------------- Stock ----------------

    [HttpGet("current-stock")]
    public async Task<IActionResult> CurrentStock([FromQuery] string? export)
        => Respond(await _reports.CurrentStockAsync(), StockColumns, export, "current-stock");

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock([FromQuery] string? export)
        => Respond(await _reports.LowStockAsync(), StockColumns, export, "low-stock");

    [HttpGet("stock-history")]
    public async Task<IActionResult> StockHistory(
        [FromQuery] int? productId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? export)
        => Respond(await _reports.StockHistoryAsync(productId, from, to), StockHistoryColumns, export, "stock-history");

    // ---------------- Shared plumbing ----------------

    private static DateTime From(DateTime? from) => (from ?? DateTime.Today.AddDays(-29)).Date;
    private static DateTime To(DateTime? to) => (to ?? DateTime.Today).Date;

    /// <summary>JSON by default; an .xlsx download when ?export=excel is present.</summary>
    private IActionResult Respond<TRow>(
        ReportResult<TRow> result, IReadOnlyList<ExcelColumn<TRow>> columns, string? export, string fileStem)
    {
        // Repositories are language-agnostic; the title and the enum label columns are
        // resolved here, where the caller's Accept-Language is known.
        var language = RequestLanguage.Of(HttpContext);
        result.Title = Messages.Resolve(result.Title, language);
        LocalizeRows(result.Rows, language);

        if (!string.Equals(export, "excel", StringComparison.OrdinalIgnoreCase))
            return Ok(ApiResponse<ReportResult<TRow>>.Ok(result));

        var range = result.From.HasValue || result.To.HasValue
            ? $"{result.From:yyyy-MM-dd} - {result.To:yyyy-MM-dd}"
            : DateTime.Today.ToString("yyyy-MM-dd");

        var bytes = _excel.Build(result.Title, result.Rows, columns, result.Summary, range);
        return File(bytes, ExcelExportService.ContentType, $"{fileStem}-{DateTime.Today:yyyyMMdd}.xlsx");
    }

    private static void LocalizeRows<TRow>(IEnumerable<TRow> rows, string language)
    {
        foreach (var row in rows)
        {
            switch (row)
            {
                case DailySalesRowDto sale:
                    sale.PaymentMethodText = Messages.PaymentLabel(sale.PaymentMethod, language);
                    break;
                case StockHistoryRowDto ledger:
                    ledger.TxnTypeText = Messages.TxnTypeLabel(ledger.TxnType, language);
                    break;
            }
        }
    }

    // ---------------- Column maps ----------------

    private static readonly IReadOnlyList<ExcelColumn<DailySalesRowDto>> SalesColumns = new[]
    {
        new ExcelColumn<DailySalesRowDto>("Invoice No", r => r.InvoiceNo),
        new ExcelColumn<DailySalesRowDto>("Date", r => r.SaleDate),
        new ExcelColumn<DailySalesRowDto>("Customer", r => r.CustomerName),
        new ExcelColumn<DailySalesRowDto>("Payment", r => r.PaymentMethodText),
        new ExcelColumn<DailySalesRowDto>("Qty", r => r.TotalQuantity),
        new ExcelColumn<DailySalesRowDto>("Sub Total", r => r.SubTotal, Money),
        new ExcelColumn<DailySalesRowDto>("Discount", r => r.Discount, Money),
        new ExcelColumn<DailySalesRowDto>("Total", r => r.TotalAmount, Money),
        new ExcelColumn<DailySalesRowDto>("Cost", r => r.TotalCost, Money),
        new ExcelColumn<DailySalesRowDto>("Profit", r => r.TotalProfit, Money)
    };

    private static readonly IReadOnlyList<ExcelColumn<MonthlySalesRowDto>> MonthlyColumns = new[]
    {
        new ExcelColumn<MonthlySalesRowDto>("Period", r => r.Period),
        new ExcelColumn<MonthlySalesRowDto>("Invoices", r => r.InvoiceCount),
        new ExcelColumn<MonthlySalesRowDto>("Qty", r => r.TotalQuantity),
        new ExcelColumn<MonthlySalesRowDto>("Sales", r => r.TotalAmount, Money),
        new ExcelColumn<MonthlySalesRowDto>("Cost", r => r.TotalCost, Money),
        new ExcelColumn<MonthlySalesRowDto>("Profit", r => r.TotalProfit, Money)
    };

    private static readonly IReadOnlyList<ExcelColumn<PurchaseSummaryRowDto>> PurchaseColumns = new[]
    {
        new ExcelColumn<PurchaseSummaryRowDto>("Invoice No", r => r.InvoiceNo),
        new ExcelColumn<PurchaseSummaryRowDto>("Date", r => r.PurchaseDate),
        new ExcelColumn<PurchaseSummaryRowDto>("Supplier", r => r.SupplierName),
        new ExcelColumn<PurchaseSummaryRowDto>("Qty", r => r.TotalQuantity),
        new ExcelColumn<PurchaseSummaryRowDto>("Sub Total", r => r.SubTotal, Money),
        new ExcelColumn<PurchaseSummaryRowDto>("Discount", r => r.Discount, Money),
        new ExcelColumn<PurchaseSummaryRowDto>("Total", r => r.TotalAmount, Money),
        new ExcelColumn<PurchaseSummaryRowDto>("Paid", r => r.PaidAmount, Money),
        new ExcelColumn<PurchaseSummaryRowDto>("Balance", r => r.TotalAmount - r.PaidAmount, Money)
    };

    private static readonly IReadOnlyList<ExcelColumn<ProfitByProductRowDto>> ProfitColumns = new[]
    {
        new ExcelColumn<ProfitByProductRowDto>("Code", r => r.Code),
        new ExcelColumn<ProfitByProductRowDto>("Product", r => r.Name),
        new ExcelColumn<ProfitByProductRowDto>("Model", r => r.Model),
        new ExcelColumn<ProfitByProductRowDto>("Brand", r => r.BrandName),
        new ExcelColumn<ProfitByProductRowDto>("Category", r => r.CategoryName),
        new ExcelColumn<ProfitByProductRowDto>("Qty Sold", r => r.QuantitySold),
        new ExcelColumn<ProfitByProductRowDto>("Sales", r => r.SaleAmount, Money),
        new ExcelColumn<ProfitByProductRowDto>("Cost", r => r.CostAmount, Money),
        new ExcelColumn<ProfitByProductRowDto>("Profit", r => r.Profit, Money)
    };

    private static readonly IReadOnlyList<ExcelColumn<CurrentStockRowDto>> StockColumns = new[]
    {
        new ExcelColumn<CurrentStockRowDto>("Code", r => r.Code),
        new ExcelColumn<CurrentStockRowDto>("Product", r => r.Name),
        new ExcelColumn<CurrentStockRowDto>("Model", r => r.Model),
        new ExcelColumn<CurrentStockRowDto>("Brand", r => r.BrandName),
        new ExcelColumn<CurrentStockRowDto>("Category", r => r.CategoryName),
        new ExcelColumn<CurrentStockRowDto>("In Stock", r => r.QuantityInStock),
        new ExcelColumn<CurrentStockRowDto>("Alert Level", r => r.LowStockThreshold),
        new ExcelColumn<CurrentStockRowDto>("Purchase Price", r => r.PurchasePrice, Money),
        new ExcelColumn<CurrentStockRowDto>("Retail Price", r => r.RetailPrice, Money),
        new ExcelColumn<CurrentStockRowDto>("Value at Cost", r => r.StockValueAtCost, Money),
        new ExcelColumn<CurrentStockRowDto>("Value at Retail", r => r.StockValueAtRetail, Money)
    };

    private static readonly IReadOnlyList<ExcelColumn<StockHistoryRowDto>> StockHistoryColumns = new[]
    {
        new ExcelColumn<StockHistoryRowDto>("Date", r => r.TxnDate),
        new ExcelColumn<StockHistoryRowDto>("Code", r => r.ProductCode),
        new ExcelColumn<StockHistoryRowDto>("Product", r => r.ProductName),
        new ExcelColumn<StockHistoryRowDto>("Type", r => r.TxnTypeText),
        new ExcelColumn<StockHistoryRowDto>("Reference", r => r.ReferenceNo),
        new ExcelColumn<StockHistoryRowDto>("In", r => r.QtyIn),
        new ExcelColumn<StockHistoryRowDto>("Out", r => r.QtyOut),
        new ExcelColumn<StockHistoryRowDto>("Balance", r => r.BalanceAfter),
        new ExcelColumn<StockHistoryRowDto>("Notes", r => r.Notes)
    };
}
