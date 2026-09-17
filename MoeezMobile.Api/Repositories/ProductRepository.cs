using Dapper;
using MoeezMobile.Api.Data;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Models.Entities;

namespace MoeezMobile.Api.Repositories;

public interface IProductRepository
{
    Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductQuery query);
    Task<ProductListItemDto?> GetByIdAsync(int id);
    Task<Product?> GetEntityAsync(int id);
    Task<IReadOnlyList<ProductSearchItemDto>> SearchAsync(string term, int take = 20);
    Task<ProductSearchItemDto?> GetByBarcodeAsync(string barcode);
    Task<int> CreateAsync(ProductFormDto dto, string? imagePath);
    Task UpdateAsync(int id, ProductFormDto dto, string? imagePath, bool changeImage);
    Task<bool> SoftDeleteAsync(int id);
    Task<int> AdjustStockAsync(int id, int newQuantity, string? reason);
    Task<bool> BarcodeInUseAsync(string barcode, int? excludeProductId = null);
    Task<string?> GetImagePathAsync(int id);
}

public class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _factory;

    public ProductRepository(IDbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Shared projection. TotalPurchased / TotalSold are computed from non-voided invoices only,
    /// so a voided invoice disappears from the product screen exactly as it does from reports.
    /// </summary>
    private const string ListProjection = @"
        SELECT p.Id, p.Code, p.Barcode, p.Name, p.BrandId, b.Name AS BrandName,
               p.CategoryId, c.Name AS CategoryName, p.Model, p.ImagePath,
               p.PurchasePrice, p.WholesalePrice, p.RetailPrice,
               p.QuantityInStock, p.LowStockThreshold, p.IsActive,
               IFNULL(pi.Qty, 0) AS TotalPurchased,
               IFNULL(si.Qty, 0) AS TotalSold
        FROM Products p
        LEFT JOIN Brands     b ON b.Id = p.BrandId
        LEFT JOIN Categories c ON c.Id = p.CategoryId
        LEFT JOIN (
            SELECT x.ProductId, SUM(x.Quantity) AS Qty
            FROM PurchaseItems x
            JOIN Purchases h ON h.Id = x.PurchaseId AND h.IsVoided = 0
            GROUP BY x.ProductId
        ) pi ON pi.ProductId = p.Id
        LEFT JOIN (
            SELECT x.ProductId, SUM(x.Quantity) AS Qty
            FROM SaleItems x
            JOIN Sales h ON h.Id = x.SaleId AND h.IsVoided = 0
            GROUP BY x.ProductId
        ) si ON si.ProductId = p.Id";

    public async Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var where = new List<string>();
        if (!query.IncludeInactive) where.Add("p.IsActive = 1");
        if (query.BrandId.HasValue) where.Add("p.BrandId = @BrandId");
        if (query.CategoryId.HasValue) where.Add("p.CategoryId = @CategoryId");
        if (query.LowStockOnly) where.Add("p.QuantityInStock <= p.LowStockThreshold");
        if (!string.IsNullOrWhiteSpace(query.Search))
            where.Add(@"(p.Name LIKE @Like OR p.Model LIKE @Like OR p.Code LIKE @Like
                         OR p.Barcode LIKE @Like OR b.Name LIKE @Like OR c.Name LIKE @Like)");

        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;

        var parameters = new DynamicParameters();
        parameters.Add("BrandId", query.BrandId);
        parameters.Add("CategoryId", query.CategoryId);
        parameters.Add("Like", $"%{query.Search?.Trim()}%");
        parameters.Add("Take", pageSize);
        parameters.Add("Skip", (page - 1) * pageSize);

        var sql = $@"
            {ListProjection}
            {whereSql}
            ORDER BY (p.QuantityInStock <= p.LowStockThreshold) DESC, p.Name
            LIMIT @Take OFFSET @Skip;

            SELECT COUNT(*)
            FROM Products p
            LEFT JOIN Brands     b ON b.Id = p.BrandId
            LEFT JOIN Categories c ON c.Id = p.CategoryId
            {whereSql};";

        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var grid = await conn.QueryMultipleAsync(sql, parameters);

        var items = (await grid.ReadAsync<ProductListItemDto>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return new PagedResult<ProductListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProductListItemDto?> GetByIdAsync(int id)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<ProductListItemDto>(
            $"{ListProjection} WHERE p.Id = @Id;", new { Id = id });
    }

    public async Task<Product?> GetEntityAsync(int id)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Product>(
            "SELECT * FROM Products WHERE Id = @Id;", new { Id = id });
    }

    /// <summary>
    /// One query across Name, Model, Code, Barcode, Brand and Category.
    /// An exact barcode/code hit sorts first so a scanner always lands on the right row.
    /// </summary>
    public async Task<IReadOnlyList<ProductSearchItemDto>> SearchAsync(string term, int take = 20)
    {
        term = (term ?? string.Empty).Trim();
        if (term.Length == 0) return Array.Empty<ProductSearchItemDto>();

        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<ProductSearchItemDto>(@"
            SELECT p.Id, p.Code, p.Barcode, p.Name, p.Model,
                   b.Name AS BrandName, c.Name AS CategoryName, p.ImagePath,
                   p.PurchasePrice, p.WholesalePrice, p.RetailPrice,
                   p.QuantityInStock, p.LowStockThreshold
            FROM Products p
            LEFT JOIN Brands     b ON b.Id = p.BrandId
            LEFT JOIN Categories c ON c.Id = p.CategoryId
            WHERE p.IsActive = 1
              AND (p.Name LIKE @Like OR p.Model LIKE @Like OR p.Code LIKE @Like
                   OR p.Barcode LIKE @Like OR b.Name LIKE @Like OR c.Name LIKE @Like)
            ORDER BY
                (p.Barcode = @Exact) DESC,
                (p.Code = @Exact) DESC,
                (p.Name LIKE @Prefix) DESC,
                (p.QuantityInStock > 0) DESC,
                p.Name
            LIMIT @Take;",
            new { Like = $"%{term}%", Prefix = $"{term}%", Exact = term, Take = Math.Clamp(take, 1, 100) });

        return rows.ToList();
    }

    public async Task<ProductSearchItemDto?> GetByBarcodeAsync(string barcode)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<ProductSearchItemDto>(@"
            SELECT p.Id, p.Code, p.Barcode, p.Name, p.Model,
                   b.Name AS BrandName, c.Name AS CategoryName, p.ImagePath,
                   p.PurchasePrice, p.WholesalePrice, p.RetailPrice,
                   p.QuantityInStock, p.LowStockThreshold
            FROM Products p
            LEFT JOIN Brands     b ON b.Id = p.BrandId
            LEFT JOIN Categories c ON c.Id = p.CategoryId
            WHERE p.Barcode = @Barcode AND p.IsActive = 1
            LIMIT 1;", new { Barcode = barcode.Trim() });
    }

    public async Task<int> CreateAsync(ProductFormDto dto, string? imagePath)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var code = await InvoiceNumberGenerator.NextGlobalAsync(conn, tx, "PRD");

            var productId = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Products
                    (Code, Barcode, Name, BrandId, CategoryId, Model, ImagePath,
                     PurchasePrice, WholesalePrice, RetailPrice,
                     QuantityInStock, LowStockThreshold, IsActive)
                VALUES
                    (@Code, @Barcode, @Name, @BrandId, @CategoryId, @Model, @ImagePath,
                     @PurchasePrice, @WholesalePrice, @RetailPrice,
                     @QuantityInStock, @LowStockThreshold, 1);
                SELECT LAST_INSERT_ID();",
                new
                {
                    Code = code,
                    Barcode = Normalize(dto.Barcode),
                    Name = dto.Name.Trim(),
                    dto.BrandId,
                    dto.CategoryId,
                    Model = Normalize(dto.Model),
                    ImagePath = imagePath,
                    dto.PurchasePrice,
                    dto.WholesalePrice,
                    dto.RetailPrice,
                    QuantityInStock = Math.Max(0, dto.OpeningQuantity),
                    LowStockThreshold = Math.Max(0, dto.LowStockThreshold)
                }, tx);

            if (dto.OpeningQuantity > 0)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO StockLedger
                        (ProductId, TxnType, ReferenceId, ReferenceNo, QtyIn, QtyOut, BalanceAfter, Notes)
                    VALUES (@ProductId, @TxnType, NULL, 'OPENING', @Qty, 0, @Qty, 'ابتدائی اسٹاک');",
                    new { ProductId = productId, TxnType = TxnType.Adjustment, Qty = dto.OpeningQuantity }, tx);
            }

            await tx.CommitAsync();
            return productId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Updates master data only. QuantityInStock is intentionally not editable here -
    /// stock changes go through purchases, sales, or AdjustStockAsync so they stay audited.
    /// </summary>
    public async Task UpdateAsync(int id, ProductFormDto dto, string? imagePath, bool changeImage)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var affected = await conn.ExecuteAsync($@"
            UPDATE Products SET
                Name = @Name, BrandId = @BrandId, CategoryId = @CategoryId,
                Model = @Model, Barcode = @Barcode,
                PurchasePrice = @PurchasePrice, WholesalePrice = @WholesalePrice,
                RetailPrice = @RetailPrice, LowStockThreshold = @LowStockThreshold
                {(changeImage ? ", ImagePath = @ImagePath" : string.Empty)}
            WHERE Id = @Id;",
            new
            {
                Id = id,
                Name = dto.Name.Trim(),
                dto.BrandId,
                dto.CategoryId,
                Model = Normalize(dto.Model),
                Barcode = Normalize(dto.Barcode),
                dto.PurchasePrice,
                dto.WholesalePrice,
                dto.RetailPrice,
                LowStockThreshold = Math.Max(0, dto.LowStockThreshold),
                ImagePath = imagePath
            });

        if (affected == 0) throw new NotFoundException(MessageKeys.ProductNotFound);
    }

    public async Task<bool> SoftDeleteAsync(int id)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.ExecuteAsync(
            "UPDATE Products SET IsActive = 0 WHERE Id = @Id;", new { Id = id }) > 0;
    }

    /// <summary>Sets stock to a counted figure and records the difference in the ledger.</summary>
    public async Task<int> AdjustStockAsync(int id, int newQuantity, string? reason)
    {
        if (newQuantity < 0) throw new BusinessException(MessageKeys.NegativeQuantity);

        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var current = await conn.ExecuteScalarAsync<int?>(
                "SELECT QuantityInStock FROM Products WHERE Id = @Id FOR UPDATE;", new { Id = id }, tx);

            if (current is null) throw new NotFoundException(MessageKeys.ProductNotFound);

            var diff = newQuantity - current.Value;
            if (diff != 0)
            {
                await conn.ExecuteAsync(
                    "UPDATE Products SET QuantityInStock = @Qty WHERE Id = @Id;",
                    new { Id = id, Qty = newQuantity }, tx);

                await conn.ExecuteAsync(@"
                    INSERT INTO StockLedger
                        (ProductId, TxnType, ReferenceId, ReferenceNo, QtyIn, QtyOut, BalanceAfter, Notes)
                    VALUES (@ProductId, @TxnType, NULL, 'ADJUST', @QtyIn, @QtyOut, @Balance, @Notes);",
                    new
                    {
                        ProductId = id,
                        TxnType = TxnType.Adjustment,
                        QtyIn = diff > 0 ? diff : 0,
                        QtyOut = diff < 0 ? -diff : 0,
                        Balance = newQuantity,
                        Notes = string.IsNullOrWhiteSpace(reason) ? "اسٹاک درستگی" : reason.Trim()
                    }, tx);
            }

            await tx.CommitAsync();
            return newQuantity;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> BarcodeInUseAsync(string barcode, int? excludeProductId = null)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM Products
            WHERE Barcode = @Barcode AND (@ExcludeId IS NULL OR Id <> @ExcludeId);",
            new { Barcode = barcode.Trim(), ExcludeId = excludeProductId }) > 0;
    }

    public async Task<string?> GetImagePathAsync(int id)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<string?>(
            "SELECT ImagePath FROM Products WHERE Id = @Id;", new { Id = id });
    }

    /// <summary>Empty strings become NULL so the UNIQUE barcode index stays usable.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
