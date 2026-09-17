using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;
using MoeezMobile.Api.Services;

namespace MoeezMobile.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Policy = Policies.AnyStaff)]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _products;
    private readonly IProductService _service;

    public ProductsController(IProductRepository products, IProductService service)
    {
        _products = products;
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductListItemDto>>>> GetPaged([FromQuery] ProductQuery query)
    {
        var result = await _products.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<ProductListItemDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductListItemDto>>> GetById(int id)
    {
        var product = await _products.GetByIdAsync(id);
        if (product is null) throw new NotFoundException(MessageKeys.ProductNotFound);
        return Ok(ApiResponse<ProductListItemDto>.Ok(product));
    }

    /// <summary>Fast lookup for the sale / purchase screens.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProductSearchItemDto>>>> Search(
        [FromQuery] string q, [FromQuery] int take = 20)
    {
        var items = await _products.SearchAsync(q, take);
        return Ok(ApiResponse<IReadOnlyList<ProductSearchItemDto>>.Ok(items));
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<ApiResponse<ProductSearchItemDto>>> GetByBarcode(string barcode)
    {
        var product = await _products.GetByBarcodeAsync(barcode);
        if (product is null) throw new NotFoundException("اس بارکوڈ کی کوئی پروڈکٹ نہیں ملی");
        return Ok(ApiResponse<ProductSearchItemDto>.Ok(product));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ProductListItemDto>>> Create(
        [FromForm] ProductFormDto dto, CancellationToken ct)
    {
        var id = await _service.CreateAsync(dto, ct);
        var created = await _products.GetByIdAsync(id);
        return Ok(ApiResponse<ProductListItemDto>.Ok(created!, MessageKeys.ProductAdded));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ProductListItemDto>>> Update(
        int id, [FromForm] ProductFormDto dto, CancellationToken ct)
    {
        await _service.UpdateAsync(id, dto, ct);
        var updated = await _products.GetByIdAsync(id);
        return Ok(ApiResponse<ProductListItemDto>.Ok(updated!, MessageKeys.ProductUpdated));
    }

    /// <summary>Soft delete - the row stays for history but disappears from lists.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok(MessageKeys.ProductDeleted));
    }

    /// <summary>Manual stock correction. Writes an Adjustment row to the ledger.</summary>
    [HttpPost("{id:int}/adjust-stock")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<ProductListItemDto>>> AdjustStock(int id, [FromBody] AdjustStockDto dto)
    {
        await _products.AdjustStockAsync(id, dto.NewQuantity, dto.Reason);
        var updated = await _products.GetByIdAsync(id);
        return Ok(ApiResponse<ProductListItemDto>.Ok(updated!, MessageKeys.StockAdjusted));
    }
}
