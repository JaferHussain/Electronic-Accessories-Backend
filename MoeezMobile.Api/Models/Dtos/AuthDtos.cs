using System.ComponentModel.DataAnnotations;

namespace MoeezMobile.Api.Models.Dtos;

public class LoginDto
{
    [Required(ErrorMessage = "یوزر نیم درج کریں")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "پاس ورڈ درج کریں")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResultDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class CreateUserDto
{
    [Required] public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    [Required, MinLength(4, ErrorMessage = "پاس ورڈ کم از کم 4 حروف کا ہو")]
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Salesman";
}

public class UpdateUserDto
{
    public string? FullName { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
