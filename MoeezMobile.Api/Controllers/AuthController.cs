using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;

namespace MoeezMobile.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public AuthController(IUserRepository users, IPasswordHasher hasher, IJwtTokenGenerator jwt)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login([FromBody] LoginDto dto)
    {
        var user = await _users.GetByUsernameAsync(dto.Username.Trim());

        if (user is null || !_hasher.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(ApiResponse<LoginResultDto>.Fail(MessageKeys.BadCredentials));

        if (!user.IsActive)
            return Unauthorized(ApiResponse<LoginResultDto>.Fail(MessageKeys.AccountDisabled));

        var (token, expiresAt) = _jwt.Generate(user);

        return Ok(ApiResponse<LoginResultDto>.Ok(new LoginResultDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role
        }, MessageKeys.Welcome));
    }

    /// <summary>Returns the caller's identity - used by the SPA to restore a session.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me()
    {
        var user = await _users.GetByIdAsync(User.GetUserId());
        if (user is null) return Unauthorized(ApiResponse<UserDto>.Fail(MessageKeys.SessionExpired));

        return Ok(ApiResponse<UserDto>.Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        }));
    }
}
