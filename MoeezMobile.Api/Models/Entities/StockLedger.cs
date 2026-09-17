namespace MoeezMobile.Api.Models.Entities;

/// <summary>StockLedger.TxnType values.</summary>
public static class TxnType
{
    public const byte Purchase = 1;
    public const byte Sale = 2;
    public const byte Adjustment = 3;
    public const byte PurchaseReturn = 4;   // used when a purchase invoice is voided
    public const byte SaleReturn = 5;       // used when a sale invoice is voided
}

public class StockLedgerEntry
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public byte TxnType { get; set; }
    public int? ReferenceId { get; set; }
    public string? ReferenceNo { get; set; }
    public int QtyIn { get; set; }
    public int QtyOut { get; set; }
    public int BalanceAfter { get; set; }
    public DateTime TxnDate { get; set; }
    public string? Notes { get; set; }
}
