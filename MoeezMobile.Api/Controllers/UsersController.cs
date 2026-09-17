using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;

namespace MoeezMobile.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.AdminOnly)]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public UsersController(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> GetAll()
        => Ok(ApiResponse<IReadOnlyList<UserDto>>.Ok(await _users.GetAllAsync()));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create([FromBody] CreateUserDto dto)
    {
        var username = dto.Username.Trim();
        if (await _users.UsernameExistsAsync(username))
            throw new BusinessException(MessageKeys.UserExists);

        if (dto.Role != Roles.Admin && dto.Role != Roles.Salesman)
            throw new BusinessException(MessageKeys.InvalidRole);

        var id = await _users.CreateAsync(username, dto.FullName, _hasher.Hash(dto.Password), dto.Role);

        return Ok(ApiResponse<UserDto>.Ok(
            new UserDto { Id = id, Username = username, FullName = dto.FullName, Role = dto.Role, IsActive = true },
            MessageKeys.UserAdded));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<object?>>> Update(int id, [FromBody] UpdateUserDto dto)
    {
        if (dto.Role is not null && dto.Role != Roles.Admin && dto.Role != Roles.Salesman)
            throw new BusinessException(MessageKeys.InvalidRole);

        // Don't let an admin lock themselves out of their own session.
        if (id == User.GetUserId() && !dto.IsActive)
            throw new BusinessException(MessageKeys.CannotDisableSelf);

        var hash = string.IsNullOrWhiteSpace(dto.Password) ? null : _hasher.Hash(dto.Password);

        if (!await _users.UpdateAsync(id, dto.FullName, hash, dto.Role, dto.IsActive))
            throw new NotFoundException(MessageKeys.NotFound);

        return Ok(ApiResponse.Ok(MessageKeys.UserUpdated));
    }
}
