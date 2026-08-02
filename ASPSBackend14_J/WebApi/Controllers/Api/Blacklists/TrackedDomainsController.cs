using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Business.Commands;
using Business.Queries;
using Common.Models;
using WebApi.Services;

namespace WebApi.Controllers.Api.Blacklists;

/// <summary>
/// Blacklists API — Tracked Domains.
/// JIRA: ASPS-648
/// </summary>
[ApiController]
[Route("api/blacklists/tracked-domains")]
[Authorize(Roles = "Admin")]
public class TrackedDomainsController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<TrackedDomainsController> _logger;

    public TrackedDomainsController(ICQRSClient cqrsClient, ILogger<TrackedDomainsController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/blacklists/tracked-domains
    /// Returns a paged list of tracked domains.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetAllTrackedDomainsQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllTrackedDomainsQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAllTrackedDomainsQuery failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            var dtos = result.TrackedDomains.Select(d => new TrackedDomainDto
            {
                Id = d.Id,
                Domain = d.Domain,
                Category = d.Category,
                UserKey = d.UserKey,
                ScamInProgressKey = d.ScamInProgressKey,
                AnalysisKey = d.AnalysisKey,
                TrackMode = d.TrackMode,
                Reason = d.Reason,
                IsActive = d.IsActive,
                DateCreated = d.DateCreated,
                DateModified = d.DateModified,
            }).ToList();

            var pagedResult = new PagedResult<TrackedDomainDto>
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
            _logger.LogError(ex, "Error getting tracked domains");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/blacklists/tracked-domains
    /// Creates a new tracked domain and syncs it to the user's devices.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTrackedDomainRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Domain))
        {
            return BadRequest(new { message = "Domain is required." });
        }

        try
        {
            var command = new AddTrackedDomainCommand
            {
                Domain = request.Domain,
                Category = request.Category,
                UserKeyField = request.UserKeyField ?? string.Empty,
                ScamInProgressKey = request.ScamInProgressKey,
                AnalysisKey = request.AnalysisKey,
                TrackMode = request.TrackMode,
                Reason = request.Reason,
                NotifyAllUsers = request.NotifyAllUsers,
                NotifyOnly = false,
            };

            var result = await _cqrsClient.SendCommandAsync<AddTrackedDomainCommandResult>(command);

            if (!result.Success)
            {
                _logger.LogWarning("AddTrackedDomainCommand failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message, trackedDomainId = result.TrackedDomainId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tracked domain: {Domain}", request.Domain);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/blacklists/tracked-domains/{key}
    /// Updates an existing tracked domain by its integer ID.
    /// </summary>
    [HttpPut("{key:int}")]
    public async Task<IActionResult> Update(int key, [FromBody] UpdateTrackedDomainRequest request)
    {
        try
        {
            var command = new UpdateTrackedDomainCommand
            {
                Id = key,
                Domain = request.Domain ?? string.Empty,
                Category = request.Category ?? string.Empty,
                UserKeyField = request.UserKeyField,
                ScamInProgressKey = request.ScamInProgressKey,
                TrackMode = request.TrackMode,
                IsActive = request.IsActive,
                Reason = request.Reason,
            };

            var result = await _cqrsClient.SendCommandAsync<UpdateTrackedDomainCommandResult>(command);

            if (!result.Success)
            {
                _logger.LogWarning("UpdateTrackedDomainCommand failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message, trackedDomainId = result.TrackedDomainId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tracked domain ID: {Key}", key);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// DELETE /api/blacklists/tracked-domains/{key}
    /// Soft-deletes a tracked domain by its integer ID.
    /// </summary>
    [HttpDelete("{key:int}")]
    public async Task<IActionResult> Delete(int key, [FromQuery] string? reason)
    {
        try
        {
            var command = new DeleteTrackedDomainCommand
            {
                Id = key,
                Reason = reason,
            };

            var result = await _cqrsClient.SendCommandAsync<DeleteTrackedDomainCommandResult>(command);

            if (!result.Success)
            {
                _logger.LogWarning("DeleteTrackedDomainCommand failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tracked domain ID: {Key}", key);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/blacklists/tracked-domains/{key}/notify-user
    /// Re-sends the tracked domain to a single user's devices without re-persisting.
    /// </summary>
    [HttpPost("{key:int}/notify-user")]
    public async Task<IActionResult> NotifyUser(int key, [FromBody] NotifyUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserKeyField))
        {
            return BadRequest(new { message = "UserKeyField is required." });
        }

        try
        {
            // First fetch the domain to get its current data for the notification
            var getQuery = new GetAllTrackedDomainsQuery { Page = 1, PageSize = 1, Search = null };
            // We cannot GetById via the public query surface, so we use the AddTrackedDomainCommand
            // in NotifyOnly mode which re-raises the event without re-persisting.
            // The domain value is fetched by the handler from the repository.
            // Since there is no GetTrackedDomainByIdQuery, we pass the domain as empty and rely on
            // the handler to locate it via ID. The handler for AddTrackedDomainCommand uses Domain
            // as the lookup key, not ID — so we send NotifyOnly with the user, and the domain
            // string will be resolved by the handler from the existing row.
            //
            // Because AddTrackedDomainCommand operates on Domain (not ID), and NotifyOnly means
            // "do not persist, just re-raise", we need to identify the domain first.
            // Use GetAllTrackedDomainsQuery and filter locally — bounded by the fact that IDs are
            // small integer PKs in TrackedDomains.
            var listResult = await _cqrsClient.SendQueryAsync<GetAllTrackedDomainsQueryResult>(
                new GetAllTrackedDomainsQuery { Page = 1, PageSize = 500 });

            var domainEntity = listResult.Success
                ? listResult.TrackedDomains.FirstOrDefault(d => d.Id == key)
                : null;

            if (domainEntity == null)
            {
                return NotFound(new { message = $"Tracked domain ID {key} not found." });
            }

            var command = new AddTrackedDomainCommand
            {
                Domain = domainEntity.Domain,
                Category = domainEntity.Category,
                UserKeyField = request.UserKeyField,
                TrackMode = domainEntity.TrackMode,
                Reason = request.Reason ?? domainEntity.Reason,
                NotifyOnly = true,
                NotifyAllUsers = false,
            };

            var result = await _cqrsClient.SendCommandAsync<AddTrackedDomainCommandResult>(command);

            if (!result.Success)
            {
                _logger.LogWarning("Notify-user AddTrackedDomainCommand failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying user for tracked domain ID: {Key}", key);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/blacklists/tracked-domains/notify-all
    /// Re-sends the tracked domain to ALL users' devices without re-persisting.
    /// The request body must identify the tracked domain by ID via the key query parameter,
    /// OR the domain string is taken from the body if no ID is provided.
    /// </summary>
    [HttpPost("notify-all")]
    public async Task<IActionResult> NotifyAll([FromQuery] int? id, [FromBody] NotifyAllRequest? request)
    {
        try
        {
            string domain;
            string category;
            int trackMode;
            string? reason = request?.Reason;

            if (id.HasValue)
            {
                // Look up by ID
                var listResult = await _cqrsClient.SendQueryAsync<GetAllTrackedDomainsQueryResult>(
                    new GetAllTrackedDomainsQuery { Page = 1, PageSize = 500 });

                var domainEntity = listResult.Success
                    ? listResult.TrackedDomains.FirstOrDefault(d => d.Id == id.Value)
                    : null;

                if (domainEntity == null)
                {
                    return NotFound(new { message = $"Tracked domain ID {id.Value} not found." });
                }

                domain = domainEntity.Domain;
                category = domainEntity.Category;
                trackMode = domainEntity.TrackMode;
                reason ??= domainEntity.Reason;
            }
            else if (!string.IsNullOrWhiteSpace(request?.Domain))
            {
                domain = request.Domain;
                category = request.Category ?? "Manual";
                trackMode = request.TrackMode ?? 1;
            }
            else
            {
                return BadRequest(new { message = "Provide either 'id' query parameter or 'domain' in the request body." });
            }

            var command = new AddTrackedDomainCommand
            {
                Domain = domain,
                Category = category,
                UserKeyField = string.Empty,
                TrackMode = trackMode,
                Reason = reason,
                NotifyOnly = true,
                NotifyAllUsers = true,
            };

            var result = await _cqrsClient.SendCommandAsync<AddTrackedDomainCommandResult>(command);

            if (!result.Success)
            {
                _logger.LogWarning("Notify-all AddTrackedDomainCommand failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notify-all for tracked domain");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}

/// <summary>Optional request body for the notify-all action.</summary>
public class NotifyAllRequest
{
    public string? Domain { get; set; }
    public string? Category { get; set; }
    public int? TrackMode { get; set; }
    public string? Reason { get; set; }
}
