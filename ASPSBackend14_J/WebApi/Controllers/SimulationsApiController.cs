using Microsoft.AspNetCore.Mvc;
using WebApi.Services;
using Business.Queries;
using Common.Models;

namespace WebApi.Controllers;

/// <summary>
/// API endpoints for Simulations UI (user/device autocomplete)
/// </summary>
[ApiController]
[Route("api/simulations")]
public class SimulationsApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<SimulationsApiController> _logger;

    public SimulationsApiController(ICQRSClient cqrsClient, ILogger<SimulationsApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// Search users for autocomplete (by name, email, phone)
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
            {
                return Ok(result.Users);
            }

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
    /// Get devices for a specific user
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
            {
                return Ok(result.Devices);
            }

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
