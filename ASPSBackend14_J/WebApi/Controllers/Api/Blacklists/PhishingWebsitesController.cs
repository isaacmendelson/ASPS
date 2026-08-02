using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Business.Queries;
using Common.Models;
using WebApi.Services;

namespace WebApi.Controllers.Api.Blacklists;

/// <summary>
/// Blacklists API — Phishing Websites.
/// JIRA: ASPS-648
/// </summary>
[ApiController]
[Route("api/blacklists/phishing-websites")]
[Authorize(Roles = "Admin")]
public class PhishingWebsitesController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<PhishingWebsitesController> _logger;

    public PhishingWebsitesController(ICQRSClient cqrsClient, ILogger<PhishingWebsitesController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/blacklists/phishing-websites
    /// Returns a paged list of known phishing websites, with optional free-text search.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetAllPhishingWebsitesQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllPhishingWebsitesQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAllPhishingWebsitesQuery failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            var dtos = result.PhishingWebsites.Select(w => new PhishingWebsiteDto
            {
                Id = w.Id,
                Url = w.Url,
                Domain = w.Domain,
                Source = w.Source,
                DateCreated = w.DateCreated,
                IsActive = w.IsActive,
            }).ToList();

            var pagedResult = new PagedResult<PhishingWebsiteDto>
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
            _logger.LogError(ex, "Error getting phishing websites");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
