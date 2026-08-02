using Business.Queries;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// REST API for device alerts — paged list with time-range and severity filters, detail, linked analysis.
/// ASPS-647: Angular Admin Client backend.
/// </summary>
[ApiController]
[Route("api/alerts")]
[Authorize(Roles = "Admin")]
public class AlertsApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<AlertsApiController> _logger;

    public AlertsApiController(ICQRSClient cqrsClient, ILogger<AlertsApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// Server-side paged list of alerts.
    /// Supports time-range filter (from/to), severity filter and free-text search.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest request,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? severity)
    {
        request.Normalize();
        try
        {
            var query = new GetAllAlertsPagedQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection,
                From = from,
                To = to,
                Severity = severity
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllAlertsPagedQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAllAlerts failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged alerts");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Single alert detail with related device and user info.
    /// </summary>
    [HttpGet("{keyType}/{keyValue}")]
    public async Task<IActionResult> GetByKey(string keyType, string keyValue)
    {
        try
        {
            var query = new GetAlertDetailQuery
            {
                AlertKey = new Key(keyType, keyValue)
            };

            var result = await _cqrsClient.SendQueryAsync<GetAlertDetailQueryResult>(query);

            if (!result.Success || result.Alert == null)
            {
                return NotFound(new { message = result.Message ?? "Alert not found" });
            }

            return Ok(result.Alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert by key {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Analysis result(s) linked to a specific alert.
    /// </summary>
    [HttpGet("{keyType}/{keyValue}/analysis")]
    public async Task<IActionResult> GetAnalysis(string keyType, string keyValue)
    {
        try
        {
            var query = new GetAnalysisResultByAlertKeyQuery
            {
                DeviceAlertKeyField = keyValue
            };

            var result = await _cqrsClient.SendQueryAsync<GetAnalysisResultByAlertKeyQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAnalysisByAlert failed for {KeyType}/{KeyValue}: {Message}", keyType, keyValue, result.Message);
                return BadRequest(new { message = result.Message });
            }

            // Return null-safe: no analysis is a valid state (not every alert triggers analysis)
            return Ok(result.AnalysisResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis for alert {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
