using Business.Queries;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// REST API for analysis results — paged list with search, single detail.
/// ASPS-647: Angular Admin Client backend.
/// </summary>
[ApiController]
[Route("api/analysis-results")]
[Authorize(Roles = "Admin")]
public class AnalysisResultsApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<AnalysisResultsApiController> _logger;

    public AnalysisResultsApiController(ICQRSClient cqrsClient, ILogger<AnalysisResultsApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// Server-side paged list of analysis results with search and sort.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest request,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? isFromCache,
        [FromQuery] bool? hasError,
        [FromQuery] string? deviceType)
    {
        request.Normalize();
        try
        {
            var query = new GetAllAnalysisResultsPagedQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection,
                From = from,
                To = to,
                IsFromCache = isFromCache,
                HasError = hasError,
                DeviceType = deviceType
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllAnalysisResultsPagedQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAllAnalysisResults failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged analysis results");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Single analysis result detail by key.
    /// </summary>
    [HttpGet("{keyType}/{keyValue}")]
    public async Task<IActionResult> GetByKey(string keyType, string keyValue)
    {
        try
        {
            var query = new GetAnalysisResultDetailQuery
            {
                AnalysisKey = new Key(keyType, keyValue)
            };

            var result = await _cqrsClient.SendQueryAsync<GetAnalysisResultDetailQueryResult>(query);

            if (!result.Success || result.AnalysisResult == null)
            {
                return NotFound(new { message = result.Message ?? "Analysis result not found" });
            }

            return Ok(result.AnalysisResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis result by key {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
