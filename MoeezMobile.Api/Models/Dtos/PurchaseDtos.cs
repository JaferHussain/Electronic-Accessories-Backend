namespace MoeezMobile.Api.Models.Dtos;

public class CreatePurchaseDto
{
    public int? SupplierId { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal Discount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseItemDto> Items { get; set; } = new();
}

public class CreatePurchaseItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class PurchaseListItemDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance => TotalAmount - PaidAmount;
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public bool IsVoided { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PurchaseItemDetailDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public class PurchaseDetailDto : PurchaseListItemDto
{
    public string? Notes { get; set; }
    public List<PurchaseItemDetailDto> Items { get; set; } = new();
}

public class PurchaseQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? SupplierId { get; set; }
    public bool IncludeVoided { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
