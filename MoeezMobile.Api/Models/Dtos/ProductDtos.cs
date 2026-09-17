using Microsoft.AspNetCore.Http;

namespace MoeezMobile.Api.Models.Dtos;

public class ProductListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? BrandId { get; set; }
    public string? BrandName { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Model { get; set; }
    public string? ImagePath { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal WholesalePrice { get; set; }
    public decimal RetailPrice { get; set; }
    public int QuantityInStock { get; set; }
    public int LowStockThreshold { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Units purchased across all non-voided purchase invoices.</summary>
    public int TotalPurchased { get; set; }

    /// <summary>Units sold across all non-voided sale invoices.</summary>
    public int TotalSold { get; set; }

    /// <summary>Profit per unit at today's prices (RetailPrice - PurchasePrice).</summary>
    public decimal ProfitPerUnit => RetailPrice - PurchasePrice;

    public bool IsLowStock => QuantityInStock <= LowStockThreshold;
}

/// <summary>Compact shape used by the sale/purchase screen search box.</summary>
public class ProductSearchItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? BrandName { get; set; }
    public string? CategoryName { get; set; }
    public string? ImagePath { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal WholesalePrice { get; set; }
    public decimal RetailPrice { get; set; }
    public int QuantityInStock { get; set; }
    public int LowStockThreshold { get; set; }
}

public class ProductQuery
{
    public string? Search { get; set; }
    public int? BrandId { get; set; }
    public int? CategoryId { get; set; }
    public bool LowStockOnly { get; set; }
    public bool IncludeInactive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Multipart form payload for create/update. Image is optional.</summary>
public class ProductFormDto
{
    public string Name { get; set; } = string.Empty;
    public int? BrandId { get; set; }
    public int? CategoryId { get; set; }
    public string? Model { get; set; }
    public string? Barcode { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal WholesalePrice { get; set; }
    public decimal RetailPrice { get; set; }

    /// <summary>Opening quantity. Applied on create only; ignored on update
    /// (use /adjust-stock so the movement is audited).</summary>
    public int OpeningQuantity { get; set; }

    public int LowStockThreshold { get; set; } = 5;
    public IFormFile? Image { get; set; }

    /// <summary>Set true on update to clear the existing image.</summary>
    public bool RemoveImage { get; set; }
}

public class AdjustStockDto
{
    /// <summary>The physical count the stock should be set to.</summary>
    public int NewQuantity { get; set; }
    public string? Reason { get; set; }
}
