using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;

namespace MoeezMobile.Api.Controllers;

[ApiController]
[Authorize(Policy = Policies.AnyStaff)]
public class LookupsController : ControllerBase
{
    private readonly ILookupRepository _lookups;

    public LookupsController(ILookupRepository lookups) => _lookups = lookups;

    // ---------------- Brands ----------------

    [HttpGet("api/brands")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LookupDto>>>> GetBrands([FromQuery] bool includeInactive = false)
        => Ok(ApiResponse<IReadOnlyList<LookupDto>>.Ok(await _lookups.GetBrandsAsync(includeInactive)));

    [HttpPost("api/brands")]
    public async Task<ActionResult<ApiResponse<LookupDto>>> CreateBrand([FromBody] NameDto dto)
    {
        var id = await _lookups.EnsureBrandAsync(dto.Name);
        return Ok(ApiResponse<LookupDto>.Ok(new LookupDto { Id = id, Name = dto.Name.Trim() }, MessageKeys.BrandSaved));
    }

    [HttpDelete("api/brands/{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteBrand(int id)
    {
        if (!await _lookups.SetBrandActiveAsync(id, false)) throw new NotFoundException(MessageKeys.BrandNotFound);
        return Ok(ApiResponse.Ok(MessageKeys.BrandDeleted));
    }

    // ---------------- Categories ----------------

    [HttpGet("api/categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LookupDto>>>> GetCategories([FromQuery] bool includeInactive = false)
        => Ok(ApiResponse<IReadOnlyList<LookupDto>>.Ok(await _lookups.GetCategoriesAsync(includeInactive)));

    [HttpPost("api/categories")]
    public async Task<ActionResult<ApiResponse<LookupDto>>> CreateCategory([FromBody] NameDto dto)
    {
        var id = await _lookups.EnsureCategoryAsync(dto.Name);
        return Ok(ApiResponse<LookupDto>.Ok(new LookupDto { Id = id, Name = dto.Name.Trim() }, MessageKeys.CategorySaved));
    }

    [HttpDelete("api/categories/{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteCategory(int id)
    {
        if (!await _lookups.SetCategoryActiveAsync(id, false)) throw new NotFoundException(MessageKeys.CategoryNotFound);
        return Ok(ApiResponse.Ok(MessageKeys.CategoryDeleted));
    }

    // ---------------- Suppliers ----------------

    [HttpGet("api/suppliers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SupplierDto>>>> GetSuppliers([FromQuery] bool includeInactive = false)
        => Ok(ApiResponse<IReadOnlyList<SupplierDto>>.Ok(await _lookups.GetSuppliersAsync(includeInactive)));

    [HttpPost("api/suppliers")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> CreateSupplier([FromBody] SupplierDto dto)
    {
        dto.Id = await _lookups.CreateSupplierAsync(dto);
        return Ok(ApiResponse<SupplierDto>.Ok(dto, MessageKeys.SupplierSaved));
    }

    [HttpPut("api/suppliers/{id:int}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> UpdateSupplier(int id, [FromBody] SupplierDto dto)
    {
        dto.Id = id;
        if (!await _lookups.UpdateSupplierAsync(dto)) throw new NotFoundException(MessageKeys.SupplierNotFound);
        return Ok(ApiResponse<SupplierDto>.Ok(dto, MessageKeys.SupplierUpdated));
    }

    // ---------------- Shop settings ----------------

    [HttpGet("api/settings")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string?>>>> GetSettings()
        => Ok(ApiResponse<Dictionary<string, string?>>.Ok(await _lookups.GetSettingsAsync()));

    [HttpPut("api/settings")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<object?>>> SaveSettings([FromBody] List<SettingDto> settings)
    {
        await _lookups.SaveSettingsAsync(settings);
        return Ok(ApiResponse.Ok(MessageKeys.SettingsSaved));
    }
}
