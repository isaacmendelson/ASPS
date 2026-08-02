using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Business.Queries;
using Common.Models;
using WebApi.Services;

namespace WebApi.Controllers.Api.Blacklists;

/// <summary>
/// Blacklists API — Bank Websites.
/// JIRA: ASPS-648
/// </summary>
[ApiController]
[Route("api/blacklists/bank-websites")]
[Authorize(Roles = "Admin")]
public class BankWebsitesController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<BankWebsitesController> _logger;

    public BankWebsitesController(ICQRSClient cqrsClient, ILogger<BankWebsitesController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/blacklists/bank-websites
    /// Returns a paged list of known bank websites.
    /// Supports optional free-text search and IsActive filter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, [FromQuery] bool? isActive)
    {
        request.Normalize();
        try
        {
            var query = new GetAllBankWebsitesQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                IsActive = isActive,
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllBankWebsitesQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAllBankWebsitesQuery failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            var dtos = result.BankWebsites.Select(b => new BankWebsiteDto
            {
                Id = b.Id,
                Domain = b.Domain,
                BankName = b.BankName,
                Country = b.Country,
                IsActive = b.IsActive,
                DateCreated = b.DateCreated,
                DateModified = b.DateModified,
            }).ToList();

            var pagedResult = new PagedResult<BankWebsiteDto>
            {
                Items = dtos,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize,
            };

            return Ok(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank websites");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
