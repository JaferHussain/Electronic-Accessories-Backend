using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;
using MoeezMobile.Api.Services;

namespace MoeezMobile.Api.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize(Policy = Policies.AnyStaff)]
public class SalesController : ControllerBase
{
    private readonly ISaleRepository _sales;
    private readonly IReceiptService _receipts;

    public SalesController(ISaleRepository sales, IReceiptService receipts)
    {
        _sales = sales;
        _receipts = receipts;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SaleListItemDto>>>> GetPaged([FromQuery] SaleQuery query)
        => Ok(ApiResponse<PagedResult<SaleListItemDto>>.Ok(await _sales.GetPagedAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SaleDetailDto>>> GetById(int id)
    {
        var sale = await _sales.GetByIdAsync(id);
        if (sale is null) throw new NotFoundException(MessageKeys.SaleNotFound);
        return Ok(ApiResponse<SaleDetailDto>.Ok(sale));
    }

    /// <summary>Validates stock, decreases it, snapshots cost and computes profit - all or nothing.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SaleDetailDto>>> Create([FromBody] CreateSaleDto dto)
    {
        var id = await _sales.CreateAsync(dto, User.GetUserId());
        var created = await _sales.GetByIdAsync(id);
        return Ok(ApiResponse<SaleDetailDto>.Ok(created!, MessageKeys.SaleSaved));
    }

    [HttpPost("{id:int}/void")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<object?>>> Void(int id)
    {
        await _sales.VoidAsync(id, User.GetUserId());
        return Ok(ApiResponse.Ok(MessageKeys.InvoiceVoided));
    }

    /// <summary>Thermal-printer friendly payload (58 mm / 80 mm).</summary>
    [HttpGet("{id:int}/receipt")]
    public async Task<ActionResult<ApiResponse<ReceiptDto>>> Receipt(int id)
        => Ok(ApiResponse<ReceiptDto>.Ok(await _receipts.BuildAsync(id)));
}
