using Microsoft.Data.SqlClient;

namespace MoeezMobile.Api.Tests.Fixtures;

/// <summary>
/// Seeds rows into the duplicate database. Writes real SQL against the real schema rather
/// than returning objects from memory, so a column that has drifted, a constraint that has
/// tightened, or a collation that has changed shows up here instead of in the shop.
/// </summary>
public static class TestData
{
    /// <summary>Inserts a product and returns its id.</summary>
    public static async Task<int> InsertProductAsync(
        SqlConnection conn,
        string name,
        decimal purchasePrice,
        decimal retailPrice,
        int stock,
        string? code = null,
        SqlTransaction? tx = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO Products
                (Code, Name, PurchasePrice, WholesalePrice, RetailPrice, QuantityInStock, IsActive)
            VALUES
                (@code, @name, @purchase, @retail, @retail, @stock, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        cmd.Parameters.AddWithValue("@code", code ?? $"PRD-{Guid.NewGuid().ToString("N")[..8]}");
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@purchase", purchasePrice);
        cmd.Parameters.AddWithValue("@retail", retailPrice);
        cmd.Parameters.AddWithValue("@stock", stock);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>Inserts a user and returns its id.</summary>
    public static async Task<int> InsertUserAsync(
        SqlConnection conn,
        string username = "owner",
        string role = "Admin",
        string? fullName = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users (Username, FullName, PasswordHash, Role, IsActive)
            VALUES (@username, @fullName, @hash, @role, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@fullName", fullName ?? UrduFixtures.CustomerName);
        // A real BCrypt hash of "ShopOwner1", so auth tests exercise real verification.
        cmd.Parameters.AddWithValue("@hash", BCrypt.Net.BCrypt.HashPassword("ShopOwner1", 11));
        cmd.Parameters.AddWithValue("@role", role);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>Reads back the stored header figures for a sale.</summary>
    public static async Task<SaleHeaderRow> ReadSaleHeaderAsync(SqlConnection conn, int saleId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT InvoiceNo, SubTotal, Discount, TotalAmount, TotalCost, TotalProfit, IsVoided
            FROM Sales WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", saleId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException($"No sale row with Id {saleId}.");

        return new SaleHeaderRow(
            reader.GetString(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetBoolean(6));
    }

    /// <summary>Reads the current stock balance for a product.</summary>
    public static async Task<int> ReadStockAsync(SqlConnection conn, int productId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT QuantityInStock FROM Products WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", productId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>Counts rows in a table, for asserting that a rollback left nothing behind.</summary>
    public static async Task<int> CountAsync(SqlConnection conn, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM `{table}`;";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public static async Task<PurchaseHeaderRow> ReadPurchaseHeaderAsync(SqlConnection conn, int purchaseId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT InvoiceNo, SubTotal, Discount, TotalAmount, PaidAmount, IsVoided
            FROM Purchases WHERE Id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", purchaseId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException($"No purchase row with Id {purchaseId}.");

        return new PurchaseHeaderRow(
            reader.GetString(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetBoolean(5));
    }
}

public readonly record struct SaleHeaderRow(
    string InvoiceNo,
    decimal SubTotal,
    decimal Discount,
    decimal TotalAmount,
    decimal TotalCost,
    decimal TotalProfit,
    bool IsVoided);

public readonly record struct PurchaseHeaderRow(
    string InvoiceNo,
    decimal SubTotal,
    decimal Discount,
    decimal TotalAmount,
    decimal PaidAmount,
    bool IsVoided);
