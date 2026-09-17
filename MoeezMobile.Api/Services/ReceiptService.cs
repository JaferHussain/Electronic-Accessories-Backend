using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;

namespace MoeezMobile.Api.Services;

public interface IReceiptService
{
    Task<ReceiptDto> BuildAsync(int saleId);
}

public class ReceiptService : IReceiptService
{
    private readonly ISaleRepository _sales;
    private readonly ILookupRepository _lookups;

    public ReceiptService(ISaleRepository sales, ILookupRepository lookups)
    {
        _sales = sales;
        _lookups = lookups;
    }

    public async Task<ReceiptDto> BuildAsync(int saleId)
    {
        var sale = await _sales.GetByIdAsync(saleId) ?? throw new NotFoundException(MessageKeys.SaleNotFound);
        var settings = await _lookups.GetSettingsAsync();

        string Setting(string key, string fallback) =>
            settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v! : fallback;

        return new ReceiptDto
        {
            ShopName = Setting("ShopName", "معیز موبائل اینڈ کارپوریشن"),
            ShopAddress = Setting("ShopAddress", "ڈانوراں، لودھراں"),
            ShopPhone = Setting("ShopPhone", string.Empty),
            FooterText = Setting("ReceiptFooter", "خریداری کا شکریہ"),
            PaperWidthMm = int.TryParse(Setting("ReceiptWidth", "80"), out var w) ? w : 80,

            InvoiceNo = sale.InvoiceNo,
            SaleDate = sale.SaleDate,
            CreatedAt = sale.CreatedAt,
            CustomerName = sale.CustomerName,
            CustomerPhone = sale.CustomerPhone,
            SaleType = sale.SaleType,
            PaymentMethod = sale.PaymentMethod,
            SalesmanName = sale.CreatedByName,
            IsVoided = sale.IsVoided,

            Lines = sale.Items.Select(i => new ReceiptLineDto
            {
                Name = i.ProductName,
                Model = i.Model,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal
            }).ToList(),

            SubTotal = sale.SubTotal,
            Discount = sale.Discount,
            TotalAmount = sale.TotalAmount,
            TotalQuantity = sale.Items.Sum(i => i.Quantity)
        };
    }
}
