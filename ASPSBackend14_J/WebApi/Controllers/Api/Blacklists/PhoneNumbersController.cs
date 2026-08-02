using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Business.Queries;
using Common.Models;
using WebApi.Services;

namespace WebApi.Controllers.Api.Blacklists;

/// <summary>
/// Blacklists API — Blacklisted Phone Numbers.
/// JIRA: ASPS-648
/// </summary>
[ApiController]
[Route("api/blacklists/phone-numbers")]
[Authorize(Roles = "Admin")]
public class PhoneNumbersController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<PhoneNumbersController> _logger;

    public PhoneNumbersController(ICQRSClient cqrsClient, ILogger<PhoneNumbersController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/blacklists/phone-numbers
    /// Returns a paged list of blacklisted phone numbers, with optional free-text search.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetAllBlacklistedPhoneNumbersQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllBlacklistedPhoneNumbersQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAllBlacklistedPhoneNumbersQuery failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            var dtos = result.PhoneNumbers.Select(p => new PhoneNumberDto
            {
                Id = p.Id,
                PhoneNumber = p.PhoneNumber,
                Source = p.Source,
                Notes = p.Notes,
                DateCreated = p.DateCreated,
                IsDeleted = p.IsDeleted,
            }).ToList();

            var pagedResult = new PagedResult<PhoneNumberDto>
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
            _logger.LogError(ex, "Error getting blacklisted phone numbers");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
