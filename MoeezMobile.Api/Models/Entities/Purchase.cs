namespace MoeezMobile.Api.Models.Entities;

public class Purchase
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Notes { get; set; }
    public bool IsVoided { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}
