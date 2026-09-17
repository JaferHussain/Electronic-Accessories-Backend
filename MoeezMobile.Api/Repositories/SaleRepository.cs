using Dapper;
using MoeezMobile.Api.Data;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Models.Entities;

namespace MoeezMobile.Api.Repositories;

public interface ISaleRepository
{
    Task<PagedResult<SaleListItemDto>> GetPagedAsync(SaleQuery query);
    Task<SaleDetailDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(CreateSaleDto dto, int userId);
    Task VoidAsync(int id, int userId);
}

public class SaleRepository : ISaleRepository
{
    private readonly IDbConnectionFactory _factory;

    public SaleRepository(IDbConnectionFactory factory) => _factory = factory;

    private sealed class CostAndBalance
    {
        public decimal PurchasePrice { get; set; }
        public int Balance { get; set; }
    }

    private const string HeaderProjection = @"
        SELECT h.Id, h.InvoiceNo, h.SaleDate, h.CustomerName, h.CustomerPhone, h.SaleType,
               h.SubTotal, h.Discount, h.TotalAmount, h.TotalCost, h.TotalProfit,
               h.PaymentMethod, h.IsVoided, u.FullName AS CreatedByName, h.CreatedAt,
               IFNULL(i.LineCount, 0) AS ItemCount,
               IFNULL(i.QtySum, 0)    AS TotalQuantity
        FROM Sales h
        LEFT JOIN Users u ON u.Id = h.CreatedBy
        LEFT JOIN (
            SELECT SaleId, COUNT(*) AS LineCount, SUM(Quantity) AS QtySum
            FROM SaleItems GROUP BY SaleId
        ) i ON i.SaleId = h.Id";

    public async Task<PagedResult<SaleListItemDto>> GetPagedAsync(SaleQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var where = new List<string>();
        if (query.From.HasValue) where.Add("h.SaleDate >= @From");
        if (query.To.HasValue) where.Add("h.SaleDate <= @To");
        if (query.PaymentMethod.HasValue) where.Add("h.PaymentMethod = @PaymentMethod");
        if (!query.IncludeVoided) where.Add("h.IsVoided = 0");
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;

        var parameters = new
        {
            From = query.From?.Date,
            To = query.To?.Date,
            query.PaymentMethod,
            Take = pageSize,
            Skip = (page - 1) * pageSize
        };

        var sql = $@"
            {HeaderProjection}
            {whereSql}
            ORDER BY h.SaleDate DESC, h.Id DESC
            LIMIT @Take OFFSET @Skip;

            SELECT COUNT(*) FROM Sales h {whereSql};";

        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var grid = await conn.QueryMultipleAsync(sql, parameters);

        var items = (await grid.ReadAsync<SaleListItemDto>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return new PagedResult<SaleListItemDto>
        { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<SaleDetailDto?> GetByIdAsync(int id)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var grid = await conn.QueryMultipleAsync($@"
            {HeaderProjection}
            WHERE h.Id = @Id;

            SELECT si.Id, si.ProductId, p.Name AS ProductName, p.Code AS ProductCode, p.Model,
                   si.Quantity, si.UnitPrice, si.UnitCost, si.LineTotal, si.LineProfit
            FROM SaleItems si
            JOIN Products p ON p.Id = si.ProductId
            WHERE si.SaleId = @Id
            ORDER BY si.Id;", new { Id = id });

        var header = await grid.ReadSingleOrDefaultAsync<SaleDetailDto>();
        if (header is null) return null;

        header.Items = (await grid.ReadAsync<SaleItemDetailDto>()).ToList();
        return header;
    }

    /// <summary>
    /// Header + items + guarded stock decrement + ledger + totals, in one InnoDB transaction.
    /// UnitCost is snapshotted from the product's PurchasePrice at this moment, which is what
    /// freezes historical profit against later price changes.
    /// </summary>
    public async Task<int> CreateAsync(CreateSaleDto dto, int userId)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            throw new BusinessException(MessageKeys.CartEmpty);

        foreach (var item in dto.Items)
        {
            if (item.Quantity <= 0) throw new BusinessException(MessageKeys.QuantityTooLow);
            if (item.UnitPrice < 0) throw new BusinessException(MessageKeys.NegativePrice);
        }

        // Merge duplicate lines for the same product so the stock guard sees the true total.
        var lines = dto.Items
            .GroupBy(i => new { i.ProductId, i.UnitPrice })
            .Select(g => new CreateSaleItemDto
            {
                ProductId = g.Key.ProductId,
                UnitPrice = g.Key.UnitPrice,
                Quantity = g.Sum(x => x.Quantity)
            })
            .OrderBy(i => i.ProductId)   // deterministic lock order
            .ToList();

        var saleDate = (dto.SaleDate ?? DateTime.Today).Date;

        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var invoiceNo = await InvoiceNumberGenerator.NextDailyAsync(conn, tx, "SAL", saleDate);

            var saleId = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Sales
                    (InvoiceNo, SaleDate, CustomerName, CustomerPhone, SaleType,
                     SubTotal, Discount, TotalAmount, TotalCost, TotalProfit,
                     PaymentMethod, IsVoided, CreatedBy)
                VALUES
                    (@InvoiceNo, @SaleDate, @CustomerName, @CustomerPhone, @SaleType,
                     0, @Discount, 0, 0, 0, @PaymentMethod, 0, @UserId);
                SELECT LAST_INSERT_ID();",
                new
                {
                    InvoiceNo = invoiceNo,
                    SaleDate = saleDate,
                    dto.CustomerName,
                    dto.CustomerPhone,
                    dto.SaleType,
                    dto.Discount,
                    dto.PaymentMethod,
                    UserId = userId
                }, tx);

            foreach (var item in lines)
            {
                // Atomic and guarded: the WHERE clause makes overselling impossible even
                // if two tills sell the last unit at the same instant.
                var affected = await conn.ExecuteAsync(@"
                    UPDATE Products
                    SET QuantityInStock = QuantityInStock - @Qty
                    WHERE Id = @Id AND QuantityInStock >= @Qty;",
                    new { Id = item.ProductId, Qty = item.Quantity }, tx);

                if (affected == 0)
                {
                    var name = await conn.ExecuteScalarAsync<string?>(
                        "SELECT Name FROM Products WHERE Id = @Id;", new { Id = item.ProductId }, tx);

                    throw name is null
                        ? new BusinessException(MessageKeys.ProductUnavailable)
                        : new BusinessException(MessageKeys.InsufficientStock, name);
                }

                var product = await conn.QuerySingleAsync<CostAndBalance>(
                    "SELECT PurchasePrice, QuantityInStock AS Balance FROM Products WHERE Id = @Id;",
                    new { Id = item.ProductId }, tx);

                var unitCost = product.PurchasePrice;
                var lineTotal = item.UnitPrice * item.Quantity;
                var lineProfit = (item.UnitPrice - unitCost) * item.Quantity;

                await conn.ExecuteAsync(@"
                    INSERT INTO SaleItems
                        (SaleId, ProductId, Quantity, UnitPrice, UnitCost, LineTotal, LineProfit)
                    VALUES (@SaleId, @ProductId, @Quantity, @UnitPrice, @UnitCost, @LineTotal, @LineProfit);",
                    new
                    {
                        SaleId = saleId,
                        item.ProductId,
                        item.Quantity,
                        item.UnitPrice,
                        UnitCost = unitCost,
                        LineTotal = lineTotal,
                        LineProfit = lineProfit
                    }, tx);

                await conn.ExecuteAsync(@"
                    INSERT INTO StockLedger
                        (ProductId, TxnType, ReferenceId, ReferenceNo, QtyIn, QtyOut, BalanceAfter, Notes)
                    VALUES (@ProductId, @TxnType, @ReferenceId, @ReferenceNo, 0, @QtyOut, @Balance, 'فروخت');",
                    new
                    {
                        item.ProductId,
                        TxnType = TxnType.Sale,
                        ReferenceId = saleId,
                        ReferenceNo = invoiceNo,
                        QtyOut = item.Quantity,
                        Balance = product.Balance
                    }, tx);
            }

            // Totals recomputed from the stored lines. The discount reduces the amount taken,
            // so it comes out of profit too.
            await conn.ExecuteAsync(@"
                UPDATE Sales h
                SET h.SubTotal    = (SELECT IFNULL(SUM(LineTotal), 0)  FROM SaleItems WHERE SaleId = h.Id),
                    h.TotalCost   = (SELECT IFNULL(SUM(UnitCost * Quantity), 0) FROM SaleItems WHERE SaleId = h.Id),
                    h.TotalAmount = (SELECT IFNULL(SUM(LineTotal), 0)  FROM SaleItems WHERE SaleId = h.Id) - h.Discount,
                    h.TotalProfit = (SELECT IFNULL(SUM(LineProfit), 0) FROM SaleItems WHERE SaleId = h.Id) - h.Discount
                WHERE h.Id = @Id;", new { Id = saleId }, tx);

            await tx.CommitAsync();
            return saleId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>Restores stock, writes SaleReturn ledger rows and flags the invoice voided.</summary>
    public async Task VoidAsync(int id, int userId)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var header = await conn.QuerySingleOrDefaultAsync<Sale>(
                "SELECT * FROM Sales WHERE Id = @Id FOR UPDATE;", new { Id = id }, tx);

            if (header is null) throw new NotFoundException(MessageKeys.SaleNotFound);
            if (header.IsVoided) throw new BusinessException(MessageKeys.AlreadyVoided);

            var items = (await conn.QueryAsync<SaleItem>(
                "SELECT * FROM SaleItems WHERE SaleId = @Id ORDER BY ProductId;",
                new { Id = id }, tx)).ToList();

            foreach (var item in items)
            {
                await conn.ExecuteAsync(
                    "UPDATE Products SET QuantityInStock = QuantityInStock + @Qty WHERE Id = @Id;",
                    new { Id = item.ProductId, Qty = item.Quantity }, tx);

                var balance = await conn.ExecuteScalarAsync<int>(
                    "SELECT QuantityInStock FROM Products WHERE Id = @Id;", new { Id = item.ProductId }, tx);

                await conn.ExecuteAsync(@"
                    INSERT INTO StockLedger
                        (ProductId, TxnType, ReferenceId, ReferenceNo, QtyIn, QtyOut, BalanceAfter, Notes)
                    VALUES (@ProductId, @TxnType, @ReferenceId, @ReferenceNo, @QtyIn, 0, @Balance, 'فروخت منسوخ');",
                    new
                    {
                        item.ProductId,
                        TxnType = TxnType.SaleReturn,
                        ReferenceId = id,
                        ReferenceNo = header.InvoiceNo,
                        QtyIn = item.Quantity,
                        Balance = balance
                    }, tx);
            }

            await conn.ExecuteAsync("UPDATE Sales SET IsVoided = 1 WHERE Id = @Id;", new { Id = id }, tx);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
