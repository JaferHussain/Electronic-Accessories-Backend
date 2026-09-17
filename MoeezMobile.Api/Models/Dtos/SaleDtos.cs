namespace MoeezMobile.Api.Models.Dtos;

public class CreateSaleDto
{
    public DateTime? SaleDate { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public byte SaleType { get; set; } = 1;       // 1 = Retail, 2 = Wholesale
    public decimal Discount { get; set; }
    public byte PaymentMethod { get; set; } = 1;  // 1 = Cash, 2 = Easypaisa/JazzCash, 3 = Udhaar
    public List<CreateSaleItemDto> Items { get; set; } = new();
}

public class CreateSaleItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class SaleListItemDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public byte SaleType { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public byte PaymentMethod { get; set; }
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public bool IsVoided { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaleItemDetailDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public decimal LineProfit { get; set; }
}

public class SaleDetailDto : SaleListItemDto
{
    public List<SaleItemDetailDto> Items { get; set; } = new();
}

public class SaleQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public byte? PaymentMethod { get; set; }
    public bool IncludeVoided { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Flat, thermal-printer friendly receipt payload.</summary>
public class ReceiptDto
{
    public string ShopName { get; set; } = string.Empty;
    public string ShopAddress { get; set; } = string.Empty;
    public string ShopPhone { get; set; } = string.Empty;
    public string FooterText { get; set; } = string.Empty;
    public int PaperWidthMm { get; set; } = 80;

    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }

    // Raw values - the client renders the label in whichever language it is showing.
    public byte SaleType { get; set; }
    public byte PaymentMethod { get; set; }

    public string? SalesmanName { get; set; }
    public bool IsVoided { get; set; }

    public List<ReceiptLineDto> Lines { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
}

public class ReceiptLineDto
{
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
