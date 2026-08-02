using Business.Commands;
using Business.Queries;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// ASPS-649 — REST API for Roadmaps (Angular Admin).
/// Route: api/roadmaps
/// </summary>
[ApiController]
[Route("api/roadmaps")]
[Authorize(Roles = "Admin")]
public class RoadmapsApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<RoadmapsApiController> _logger;

    public RoadmapsApiController(ICQRSClient cqrsClient, ILogger<RoadmapsApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/roadmaps
    /// Server-side paged list of roadmaps.
    /// Excludes archived roadmaps by default; pass ?includeArchived=true to include them.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, [FromQuery] bool includeArchived = false)
    {
        request.Normalize();
        try
        {
            var query = new GetAllRoadmapsPagedQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection,
                IncludeArchived = includeArchived
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllRoadmapsPagedQueryResult>(query);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged roadmaps");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/roadmaps
    /// Create a new empty roadmap with a name and optional description.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoadmapRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Name is required" });

            // Resolve caller identity for audit trail
            var createdBy = User?.Identity?.Name;

            var command = new CreateRoadmapCommand
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                CreatedBy = createdBy
            };

            var result = await _cqrsClient.SendCommandAsync<CreateRoadmapCommandResult>(command);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return StatusCode(201, new { id = result.RoadmapId, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating roadmap");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/roadmaps/{id}/archive
    /// Archive a roadmap (soft delete).
    /// </summary>
    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        try
        {
            var updatedBy = User?.Identity?.Name;

            var command = new ArchiveRoadmapCommand
            {
                Id = id,
                UpdatedBy = updatedBy
            };

            var result = await _cqrsClient.SendCommandAsync<ArchiveRoadmapCommandResult>(command);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving roadmap {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}

// =========================================================================
// Request DTOs
// =========================================================================

public class CreateRoadmapRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
