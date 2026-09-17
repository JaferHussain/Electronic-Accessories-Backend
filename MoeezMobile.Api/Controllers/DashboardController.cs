using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;

namespace MoeezMobile.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Policies.AnyStaff)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardRepository _dashboard;

    public DashboardController(IDashboardRepository dashboard) => _dashboard = dashboard;

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> Summary()
        => Ok(ApiResponse<DashboardSummaryDto>.Ok(await _dashboard.GetSummaryAsync()));

    [HttpGet("low-stock")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LowStockItemDto>>>> LowStock([FromQuery] int take = 10)
        => Ok(ApiResponse<IReadOnlyList<LowStockItemDto>>.Ok(await _dashboard.GetLowStockAsync(take)));

    [HttpGet("recent-sales")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecentSaleDto>>>> RecentSales([FromQuery] int take = 10)
        => Ok(ApiResponse<IReadOnlyList<RecentSaleDto>>.Ok(await _dashboard.GetRecentSalesAsync(take)));

    [HttpGet("sales-chart")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SalesChartPointDto>>>> SalesChart([FromQuery] int days = 7)
        => Ok(ApiResponse<IReadOnlyList<SalesChartPointDto>>.Ok(await _dashboard.GetSalesChartAsync(days)));
}
