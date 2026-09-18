using Dapper;
using Microsoft.Data.SqlClient;
using MoeezMobile.Api.Data;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Models.Entities;

namespace MoeezMobile.Api.Repositories;

public interface IPurchaseRepository
{
    Task<PagedResult<PurchaseListItemDto>> GetPagedAsync(PurchaseQuery query);
    Task<PurchaseDetailDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(CreatePurchaseDto dto, int userId);
    Task VoidAsync(int id, int userId);
}

public class PurchaseRepository : IPurchaseRepository
{
    private readonly IDbConnectionFactory _factory;

    public PurchaseRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string HeaderProjection = @"
        SELECT h.Id, h.InvoiceNo, h.PurchaseDate, h.SupplierId, s.Name AS SupplierName,
               h.SubTotal, h.Discount, h.TotalAmount, h.PaidAmount, h.Notes,
               h.IsVoided, u.FullName AS CreatedByName, h.CreatedAt,
               ISNULL(i.LineCount, 0) AS ItemCount,
               ISNULL(i.QtySum, 0)    AS TotalQuantity
        FROM Purchases h
        LEFT JOIN Suppliers s ON s.Id = h.SupplierId
        LEFT JOIN Users     u ON u.Id = h.CreatedBy
        LEFT JOIN (
            SELECT PurchaseId, COUNT(*) AS LineCount, SUM(Quantity) AS QtySum
            FROM PurchaseItems GROUP BY PurchaseId
        ) i ON i.PurchaseId = h.Id";

    public async Task<PagedResult<PurchaseListItemDto>> GetPagedAsync(PurchaseQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var where = new List<string>();
        if (query.From.HasValue) where.Add("h.PurchaseDate >= @From");
        if (query.To.HasValue) where.Add("h.PurchaseDate <= @To");
        if (query.SupplierId.HasValue) where.Add("h.SupplierId = @SupplierId");
        if (!query.IncludeVoided) where.Add("h.IsVoided = 0");
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;

        var parameters = new
        {
            From = query.From?.Date,
            To = query.To?.Date,
            query.SupplierId,
            Take = pageSize,
            Skip = (page - 1) * pageSize
        };

        var sql = $@"
            {HeaderProjection}
            {whereSql}
            ORDER BY h.PurchaseDate DESC, h.Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

            SELECT COUNT(*) FROM Purchases h {whereSql};";

        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var grid = await conn.QueryMultipleAsync(sql, parameters);

        var items = (await grid.ReadAsync<PurchaseListItemDto>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return new PagedResult<PurchaseListItemDto>
        { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PurchaseDetailDto?> GetByIdAsync(int id)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var grid = await conn.QueryMultipleAsync($@"
            {HeaderProjection}
            WHERE h.Id = @Id;

            SELECT pi.Id, pi.ProductId, p.Name AS ProductName, p.Code AS ProductCode, p.Model,
                   pi.Quantity, pi.UnitCost, pi.LineTotal
            FROM PurchaseItems pi
            JOIN Products p ON p.Id = pi.ProductId
            WHERE pi.PurchaseId = @Id
            ORDER BY pi.Id;", new { Id = id });

        var header = await grid.ReadSingleOrDefaultAsync<PurchaseDetailDto>();
        if (header is null) return null;

        header.Items = (await grid.ReadAsync<PurchaseItemDetailDto>()).ToList();
        return header;
    }

    /// <summary>
    /// Header + items + stock increase + ledger, all in one InnoDB transaction.
    /// Also refreshes Product.PurchasePrice to the latest cost paid (business rule 6).
    /// </summary>
    public async Task<int> CreateAsync(CreatePurchaseDto dto, int userId)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            throw new BusinessException(MessageKeys.NoItems);

        foreach (var item in dto.Items)
        {
            if (item.Quantity <= 0) throw new BusinessException(MessageKeys.QuantityTooLow);
            if (item.UnitCost < 0) throw new BusinessException(MessageKeys.NegativePrice);
        }

        var purchaseDate = (dto.PurchaseDate ?? DateTime.Today).Date;

        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            var invoiceNo = await InvoiceNumberGenerator.NextDailyAsync(conn, tx, "PUR", purchaseDate);

            var purchaseId = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Purchases
                    (InvoiceNo, SupplierId, PurchaseDate, SubTotal, Discount, TotalAmount,
                     PaidAmount, Notes, IsVoided, CreatedBy)
                VALUES
                    (@InvoiceNo, @SupplierId, @PurchaseDate, 0, @Discount, 0,
                     @PaidAmount, @Notes, 0, @UserId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    InvoiceNo = invoiceNo,
                    dto.SupplierId,
                    PurchaseDate = purchaseDate,
                    dto.Discount,
                    dto.PaidAmount,
                    dto.Notes,
                    UserId = userId
                }, tx);

            // Deterministic order avoids deadlocks when two invoices touch the same products.
            foreach (var item in dto.Items.OrderBy(i => i.ProductId))
            {
                var affected = await conn.ExecuteAsync(@"
                    UPDATE Products
                    SET QuantityInStock = QuantityInStock + @Qty,
                        PurchasePrice   = @UnitCost
                    WHERE Id = @Id AND IsActive = 1;",
                    new { Id = item.ProductId, Qty = item.Quantity, item.UnitCost }, tx);

                if (affected == 0)
                    throw new BusinessException(MessageKeys.ProductMissing);

                var balance = await conn.ExecuteScalarAsync<int>(
                    "SELECT QuantityInStock FROM Products WHERE Id = @Id;", new { Id = item.ProductId }, tx);

                await conn.ExecuteAsync(@"
                    INSERT INTO PurchaseItems (PurchaseId, ProductId, Quantity, UnitCost, LineTotal)
                    VALUES (@PurchaseId, @ProductId, @Quantity, @UnitCost, @LineTotal);",
                    new
                    {
                        PurchaseId = purchaseId,
                        item.ProductId,
                        item.Quantity,
                        item.UnitCost,
                        LineTotal = item.UnitCost * item.Quantity
                    }, tx);

                await conn.ExecuteAsync(@"
                    INSERT INTO StockLedger
                        (ProductId, TxnType, ReferenceId, ReferenceNo, QtyIn, QtyOut, BalanceAfter, Notes)
                    VALUES (@ProductId, @TxnType, @ReferenceId, @ReferenceNo, @QtyIn, 0, @Balance, N'خریداری');",
                    new
                    {
                        item.ProductId,
                        TxnType = TxnType.Purchase,
                        ReferenceId = purchaseId,
                        ReferenceNo = invoiceNo,
                        QtyIn = item.Quantity,
                        Balance = balance
                    }, tx);
            }

            // Totals are recalculated from the stored lines, never trusted from the client.
            await conn.ExecuteAsync(@"
                UPDATE h
                SET SubTotal    = (SELECT ISNULL(SUM(LineTotal), 0) FROM PurchaseItems WHERE PurchaseId = h.Id),
                    TotalAmount = (SELECT ISNULL(SUM(LineTotal), 0) FROM PurchaseItems WHERE PurchaseId = h.Id) - h.Discount
                FROM Purchases h
                WHERE h.Id = @Id;", new { Id = purchaseId }, tx);

            await tx.CommitAsync();
            return purchaseId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Reverses the stock this invoice added. Product.PurchasePrice is deliberately left alone -
    /// the last cost actually paid is still the most useful figure for costing.
    /// </summary>
    public async Task VoidAsync(int id, int userId)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            var header = await conn.QuerySingleOrDefaultAsync<Purchase>(
                "SELECT * FROM Purchases WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id;", new { Id = id }, tx);

            if (header is null) throw new NotFoundException(MessageKeys.PurchaseNotFound);
            if (header.IsVoided) throw new BusinessException(MessageKeys.AlreadyVoided);

            var items = (await conn.QueryAsync<PurchaseItem>(
                "SELECT * FROM PurchaseItems WHERE PurchaseId = @Id ORDER BY ProductId;",
                new { Id = id }, tx)).ToList();

            foreach (var item in items)
            {
                // Guarded: refuse to void if the goods have already been sold on.
                var affected = await conn.ExecuteAsync(@"
                    UPDATE Products
                    SET QuantityInStock = QuantityInStock - @Qty
                    WHERE Id = @Id AND QuantityInStock >= @Qty;",
                    new { Id = item.ProductId, Qty = item.Quantity }, tx);

                if (affected == 0)
                    throw new BusinessException(MessageKeys.CannotVoidStockSold);

                var balance = await conn.ExecuteScalarAsync<int>(
                    "SELECT QuantityInStock FROM Products WHERE Id = @Id;", new { Id = item.ProductId }, tx);

                await conn.ExecuteAsync(@"
                    INSERT INTO StockLedger
                        (ProductId, TxnType, ReferenceId, ReferenceNo, QtyIn, QtyOut, BalanceAfter, Notes)
                    VALUES (@ProductId, @TxnType, @ReferenceId, @ReferenceNo, 0, @QtyOut, @Balance, N'خریداری منسوخ');",
                    new
                    {
                        item.ProductId,
                        TxnType = TxnType.PurchaseReturn,
                        ReferenceId = id,
                        ReferenceNo = header.InvoiceNo,
                        QtyOut = item.Quantity,
                        Balance = balance
                    }, tx);
            }

            await conn.ExecuteAsync("UPDATE Purchases SET IsVoided = 1 WHERE Id = @Id;", new { Id = id }, tx);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
