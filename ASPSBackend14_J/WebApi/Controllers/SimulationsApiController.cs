using Business.Commands;
using Business.Queries;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// ASPS-649 — API endpoints for Simulations (CRUD, run, and autocomplete helpers).
/// </summary>
[ApiController]
[Route("api/simulations")]
[Authorize(Roles = "Admin")]
public class SimulationsApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<SimulationsApiController> _logger;

    public SimulationsApiController(ICQRSClient cqrsClient, ILogger<SimulationsApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    // =========================================================================
    // CRUD + run (ASPS-649)
    // =========================================================================

    /// <summary>
    /// GET /api/simulations
    /// Server-side paged list of simulations with optional search and sort.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetAllSimulationsPagedQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllSimulationsPagedQueryResult>(query);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged simulations");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/simulations/{keyField}
    /// Get a single simulation by its KeyField.
    /// </summary>
    [HttpGet("{keyField}")]
    public async Task<IActionResult> GetByKey(string keyField)
    {
        try
        {
            var query = new GetSimulationByKeyFieldQuery { KeyField = keyField };
            var result = await _cqrsClient.SendQueryAsync<GetSimulationByKeyFieldQueryResult>(query);

            if (!result.Success || result.Simulation == null)
                return NotFound(new { message = result.Message ?? "Simulation not found" });

            return Ok(result.Simulation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting simulation {KeyField}", keyField);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/simulations
    /// Create a new simulation.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSimulationRequest request)
    {
        try
        {
            var command = new CreateSimulationCommand
            {
                Name = request.Name,
                Description = request.Description,
                CreatorKey = new Key("User", request.CreatorKeyField),
                Steps = request.Steps ?? Array.Empty<SimulationStep>()
            };

            var result = await _cqrsClient.SendCommandAsync<CreateSimulationCommandResult>(command);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return CreatedAtAction(
                nameof(GetByKey),
                new { keyField = result.SimulationKey!.Value },
                new { key = result.SimulationKey, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating simulation");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/simulations/{keyField}
    /// Update an existing simulation.
    /// </summary>
    [HttpPut("{keyField}")]
    public async Task<IActionResult> Update(string keyField, [FromBody] UpdateSimulationRequest request)
    {
        try
        {
            var command = new UpdateSimulationCommand
            {
                SimulationKey = new Key("Simulation", keyField),
                Name = request.Name,
                Description = request.Description,
                Steps = request.Steps ?? Array.Empty<SimulationStep>()
            };

            var result = await _cqrsClient.SendCommandAsync<UpdateSimulationCommandResult>(command);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating simulation {KeyField}", keyField);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// DELETE /api/simulations/{keyField}
    /// Soft-delete a simulation.
    /// </summary>
    [HttpDelete("{keyField}")]
    public async Task<IActionResult> Delete(string keyField)
    {
        try
        {
            var command = new DeleteSimulationCommand
            {
                SimulationKey = new Key("Simulation", keyField)
            };

            var result = await _cqrsClient.SendCommandAsync<DeleteSimulationCommandResult>(command);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting simulation {KeyField}", keyField);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/simulations/{keyField}/run
    /// Execute/run a simulation.
    /// </summary>
    [HttpPost("{keyField}/run")]
    public async Task<IActionResult> Run(string keyField, [FromBody] RunSimulationRequest? request)
    {
        try
        {
            var requestorKeyField = request?.RequestorKeyField ?? string.Empty;

            var command = new RunSimulationCommand
            {
                SimulationKey = new Key("Simulation", keyField),
                RequestorKey = new Key("User", requestorKeyField)
            };

            var result = await _cqrsClient.SendCommandAsync<RunSimulationCommandResult>(command);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new
            {
                message = result.Message,
                totalSteps = result.TotalSteps,
                executedSteps = result.ExecutedSteps,
                startedAt = result.StartedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running simulation {KeyField}", keyField);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    // =========================================================================
    // Autocomplete helpers (pre-existing, kept as-is)
    // =========================================================================

    /// <summary>
    /// GET /api/simulations/users
    /// Search users for autocomplete (by name, email, phone).
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? search)
    {
        try
        {
            _logger.LogInformation("Searching users with term: {Search}", search);

            var query = new GetSimulationUsersQuery { SearchText = search };
            var result = await _cqrsClient.SendQueryAsync<GetSimulationUsersQueryResult>(query);

            if (result.Success)
                return Ok(result.Users);

            _logger.LogWarning("Failed to search users: {Message}", result.Message);
            return BadRequest(new { error = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/simulations/devices
    /// Search devices for autocomplete (by UID, MAC, OS, type).
    /// </summary>
    [HttpGet("devices")]
    public async Task<IActionResult> SearchDevices([FromQuery] string? search, [FromQuery] string? userKeyField)
    {
        try
        {
            _logger.LogInformation("Searching devices with term: {Search}, user: {UserKey}", search, userKeyField);

            var query = new GetSimulationDevicesQuery
            {
                SearchText = search,
                UserKey = !string.IsNullOrEmpty(userKeyField) ? new Key("User", userKeyField) : null
            };
            var result = await _cqrsClient.SendQueryAsync<GetSimulationDevicesQueryResult>(query);

            if (result.Success)
                return Ok(result.Devices);

            _logger.LogWarning("Failed to search devices: {Message}", result.Message);
            return BadRequest(new { error = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching devices");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/simulations/users/{userKeyField}/devices
    /// Get devices for a specific user.
    /// </summary>
    [HttpGet("users/{userKeyField}/devices")]
    public async Task<IActionResult> GetUserDevices(string userKeyField)
    {
        try
        {
            _logger.LogInformation("Getting devices for user: {UserKey}", userKeyField);

            var query = new GetSimulationUserDevicesQuery
            {
                UserKey = new Key("User", userKeyField)
            };
            var result = await _cqrsClient.SendQueryAsync<GetSimulationUserDevicesQueryResult>(query);

            if (result.Success)
                return Ok(result.Devices);

            _logger.LogWarning("Failed to get user devices: {Message}", result.Message);
            return BadRequest(new { error = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user devices");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

// =========================================================================
// Request DTOs (ASPS-649)
// =========================================================================

public class CreateSimulationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatorKeyField { get; set; } = string.Empty;
    public SimulationStep[]? Steps { get; set; }
}

public class UpdateSimulationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SimulationStep[]? Steps { get; set; }
}

public class RunSimulationRequest
{
    public string? RequestorKeyField { get; set; }
}
