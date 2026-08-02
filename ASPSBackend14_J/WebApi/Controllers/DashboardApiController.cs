using Business.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// ASPS-646 — Angular Admin: Dashboard summary API.
/// GET /api/dashboard/summary returns KPI counts for the Angular admin dashboard.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Admin")]
public class DashboardApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<DashboardApiController> _logger;

    public DashboardApiController(ICQRSClient cqrsClient, ILogger<DashboardApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// Returns KPI counts: total users, total devices, active alerts (last 24 h),
    /// and analysis results count.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        try
        {
            var result = await _cqrsClient.SendQueryAsync<GetDashboardSummaryQueryResult>(
                new GetDashboardSummaryQuery());

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new
            {
                totalUsers = result.TotalUsers,
                totalDevices = result.TotalDevices,
                activeAlerts24h = result.ActiveAlerts24h,
                analysisResultsCount = result.AnalysisResultsCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard summary");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
