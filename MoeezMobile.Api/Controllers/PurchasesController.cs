using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;

namespace MoeezMobile.Api.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize(Policy = Policies.AdminOnly)]
public class PurchasesController : ControllerBase
{
    private readonly IPurchaseRepository _purchases;

    public PurchasesController(IPurchaseRepository purchases) => _purchases = purchases;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseListItemDto>>>> GetPaged([FromQuery] PurchaseQuery query)
        => Ok(ApiResponse<PagedResult<PurchaseListItemDto>>.Ok(await _purchases.GetPagedAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PurchaseDetailDto>>> GetById(int id)
    {
        var purchase = await _purchases.GetByIdAsync(id);
        if (purchase is null) throw new NotFoundException(MessageKeys.PurchaseNotFound);
        return Ok(ApiResponse<PurchaseDetailDto>.Ok(purchase));
    }

    /// <summary>Creates the invoice, increases stock and writes the ledger - all or nothing.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PurchaseDetailDto>>> Create([FromBody] CreatePurchaseDto dto)
    {
        var id = await _purchases.CreateAsync(dto, User.GetUserId());
        var created = await _purchases.GetByIdAsync(id);
        return Ok(ApiResponse<PurchaseDetailDto>.Ok(created!, MessageKeys.PurchaseSaved));
    }

    [HttpPost("{id:int}/void")]
    public async Task<ActionResult<ApiResponse<object?>>> Void(int id)
    {
        await _purchases.VoidAsync(id, User.GetUserId());
        return Ok(ApiResponse.Ok(MessageKeys.InvoiceVoided));
    }
}
