using Dapper;
using MoeezMobile.Api.Data;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Models.Entities;

namespace MoeezMobile.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(int id);
    Task<IReadOnlyList<UserDto>> GetAllAsync();
    Task<int> CreateAsync(string username, string? fullName, string passwordHash, string role);
    Task<bool> UpdateAsync(int id, string? fullName, string? passwordHash, string? role, bool isActive);
    Task<bool> UsernameExistsAsync(string username);
}

public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _factory;

    public UserRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SelectColumns =
        "Id, Username, FullName, PasswordHash, Role, IsActive, CreatedAt";

    public async Task<User?> GetByUsernameAsync(string username)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"SELECT {SelectColumns} FROM Users WHERE Username = @Username LIMIT 1;",
            new { Username = username });
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"SELECT {SelectColumns} FROM Users WHERE Id = @Id;", new { Id = id });
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync()
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<UserDto>(
            "SELECT Id, Username, FullName, Role, IsActive, CreatedAt FROM Users ORDER BY Username;");
        return rows.ToList();
    }

    public async Task<int> CreateAsync(string username, string? fullName, string passwordHash, string role)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Users (Username, FullName, PasswordHash, Role, IsActive)
            VALUES (@Username, @FullName, @PasswordHash, @Role, 1);
            SELECT LAST_INSERT_ID();",
            new { Username = username, FullName = fullName, PasswordHash = passwordHash, Role = role });
    }

    public async Task<bool> UpdateAsync(int id, string? fullName, string? passwordHash, string? role, bool isActive)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        var affected = await conn.ExecuteAsync(@"
            UPDATE Users SET
                FullName     = IFNULL(@FullName, FullName),
                PasswordHash = IFNULL(@PasswordHash, PasswordHash),
                Role         = IFNULL(@Role, Role),
                IsActive     = @IsActive
            WHERE Id = @Id;",
            new { Id = id, FullName = fullName, PasswordHash = passwordHash, Role = role, IsActive = isActive ? 1 : 0 });
        return affected > 0;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Users WHERE Username = @Username;", new { Username = username }) > 0;
    }
}
