using Dapper;
using MoeezMobile.Api.Data;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;

namespace MoeezMobile.Api.Repositories;

public interface ILookupRepository
{
    Task<IReadOnlyList<LookupDto>> GetBrandsAsync(bool includeInactive = false);
    Task<IReadOnlyList<LookupDto>> GetCategoriesAsync(bool includeInactive = false);

    /// <summary>Creates the brand, or returns the id of the existing one with that name.</summary>
    Task<int> EnsureBrandAsync(string name);
    Task<int> EnsureCategoryAsync(string name);
    Task<bool> SetBrandActiveAsync(int id, bool isActive);
    Task<bool> SetCategoryActiveAsync(int id, bool isActive);

    Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(bool includeInactive = false);
    Task<int> CreateSupplierAsync(SupplierDto dto);
    Task<bool> UpdateSupplierAsync(SupplierDto dto);

    Task<Dictionary<string, string?>> GetSettingsAsync();
    Task<string?> GetSettingAsync(string key, string? fallback = null);
    Task SaveSettingsAsync(IEnumerable<SettingDto> settings);
}

public class LookupRepository : ILookupRepository
{
    private sealed class SettingRow
    {
        public string SettingKey { get; set; } = string.Empty;
        public string? SettingValue { get; set; }
    }

    private readonly IDbConnectionFactory _factory;

    public LookupRepository(IDbConnectionFactory factory) => _factory = factory;

    // ---------------- Brands / Categories ----------------

    public async Task<IReadOnlyList<LookupDto>> GetBrandsAsync(bool includeInactive = false)
        => await GetLookupAsync("Brands", "BrandId", includeInactive);

    public async Task<IReadOnlyList<LookupDto>> GetCategoriesAsync(bool includeInactive = false)
        => await GetLookupAsync("Categories", "CategoryId", includeInactive);

    private async Task<IReadOnlyList<LookupDto>> GetLookupAsync(string table, string productColumn, bool includeInactive)
    {
        // table/productColumn are compile-time constants from the two callers above - never user input.
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<LookupDto>($@"
            SELECT l.Id, l.Name, l.IsActive,
                   (SELECT COUNT(*) FROM Products p
                     WHERE p.{productColumn} = l.Id AND p.IsActive = 1) AS ProductCount
            FROM {table} l
            WHERE (@IncludeInactive = 1 OR l.IsActive = 1)
            ORDER BY l.Name;", new { IncludeInactive = includeInactive ? 1 : 0 });
        return rows.ToList();
    }

    public Task<int> EnsureBrandAsync(string name) => EnsureLookupAsync("Brands", name);
    public Task<int> EnsureCategoryAsync(string name) => EnsureLookupAsync("Categories", name);

    private async Task<int> EnsureLookupAsync(string table, string name)
    {
        name = name.Trim();
        if (name.Length == 0) throw new BusinessException(MessageKeys.NameRequired);

        await using var conn = await _factory.CreateOpenConnectionAsync();

        var existing = await conn.ExecuteScalarAsync<int?>(
            $"SELECT TOP (1) Id FROM {table} WHERE Name = @Name;", new { Name = name });
        if (existing.HasValue)
        {
            // Re-activate a previously hidden entry rather than creating a duplicate.
            await conn.ExecuteAsync($"UPDATE {table} SET IsActive = 1 WHERE Id = @Id;", new { Id = existing.Value });
            return existing.Value;
        }

        return await conn.ExecuteScalarAsync<int>(
            $"INSERT INTO {table} (Name, IsActive) VALUES (@Name, 1); SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { Name = name });
    }

    public Task<bool> SetBrandActiveAsync(int id, bool isActive) => SetLookupActiveAsync("Brands", id, isActive);
    public Task<bool> SetCategoryActiveAsync(int id, bool isActive) => SetLookupActiveAsync("Categories", id, isActive);

    private async Task<bool> SetLookupActiveAsync(string table, int id, bool isActive)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var affected = await conn.ExecuteAsync(
            $"UPDATE {table} SET IsActive = @IsActive WHERE Id = @Id;",
            new { Id = id, IsActive = isActive ? 1 : 0 });
        return affected > 0;
    }

    // ---------------- Suppliers ----------------

    public async Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(bool includeInactive = false)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<SupplierDto>(@"
            SELECT Id, Name, Phone, Address, OpeningBalance, IsActive
            FROM Suppliers
            WHERE (@IncludeInactive = 1 OR IsActive = 1)
            ORDER BY Name;", new { IncludeInactive = includeInactive ? 1 : 0 });
        return rows.ToList();
    }

    public async Task<int> CreateSupplierAsync(SupplierDto dto)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Suppliers (Name, Phone, Address, OpeningBalance, IsActive)
            VALUES (@Name, @Phone, @Address, @OpeningBalance, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { Name = dto.Name.Trim(), dto.Phone, dto.Address, dto.OpeningBalance });
    }

    public async Task<bool> UpdateSupplierAsync(SupplierDto dto)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var affected = await conn.ExecuteAsync(@"
            UPDATE Suppliers
            SET Name = @Name, Phone = @Phone, Address = @Address,
                OpeningBalance = @OpeningBalance, IsActive = @IsActive
            WHERE Id = @Id;",
            new { dto.Id, Name = dto.Name.Trim(), dto.Phone, dto.Address, dto.OpeningBalance, IsActive = dto.IsActive ? 1 : 0 });
        return affected > 0;
    }

    // ---------------- Settings ----------------

    public async Task<Dictionary<string, string?>> GetSettingsAsync()
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<SettingRow>(
            "SELECT SettingKey, SettingValue FROM AppSettings;");
        return rows.ToDictionary(r => r.SettingKey, r => r.SettingValue, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string?> GetSettingAsync(string key, string? fallback = null)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var value = await conn.ExecuteScalarAsync<string?>(
            "SELECT SettingValue FROM AppSettings WHERE SettingKey = @Key;", new { Key = key });
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public async Task SaveSettingsAsync(IEnumerable<SettingDto> settings)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        foreach (var s in settings)
        {
            if (string.IsNullOrWhiteSpace(s.Key)) continue;
            await conn.ExecuteAsync(@"
                UPDATE AppSettings WITH (UPDLOCK, HOLDLOCK)
                SET SettingValue = @Value WHERE SettingKey = @Key;

                IF @@ROWCOUNT = 0
                    INSERT INTO AppSettings (SettingKey, SettingValue) VALUES (@Key, @Value);",
                new { Key = s.Key.Trim(), s.Value });
        }
    }
}
