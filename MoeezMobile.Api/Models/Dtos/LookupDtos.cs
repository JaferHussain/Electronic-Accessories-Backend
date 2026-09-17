using System.ComponentModel.DataAnnotations;

namespace MoeezMobile.Api.Models.Dtos;

public class LookupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int ProductCount { get; set; }
}

public class NameDto
{
    [Required(ErrorMessage = "نام درج کریں")]
    public string Name { get; set; } = string.Empty;
}

public class SupplierDto
{
    public int Id { get; set; }
    [Required(ErrorMessage = "نام درج کریں")]
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SettingDto
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
