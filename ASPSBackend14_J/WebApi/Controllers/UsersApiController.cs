using Business.Commands;
using Business.Queries;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.DTOs;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// ASPS-646 — Angular Admin: Users REST API.
/// Full server-side paging, search, sort, risk scores, alerts, and devices per user.
/// Route prefix: api/users (parallel to legacy UsersController which uses INetMQClientService).
/// Angular SPA calls these endpoints; Razor pages continue using the legacy controller.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<UsersApiController> _logger;

    public UsersApiController(ICQRSClient cqrsClient, ILogger<UsersApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/users
    /// Server-side paged list of users with device count, optional search and sort.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetAllUsersPagedQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllUsersPagedQueryResult>(query);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged users");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/users/{keyType}/{keyValue}
    /// Single user with full detail.
    /// </summary>
    [HttpGet("{keyType}/{keyValue}")]
    public async Task<IActionResult> GetByKey(string keyType, string keyValue)
    {
        try
        {
            var query = new GetUserByKeyQuery
            {
                UserKey = new Key(keyType, keyValue)
            };

            var result = await _cqrsClient.SendQueryAsync<GetUserByKeyQueryResult>(query);

            if (!result.Success || result.User == null)
            {
                return NotFound(new { message = result.Message ?? "User not found" });
            }

            return Ok(result.User);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/users
    /// Create a new user. Supports address, city, phone (via UpdateUserCommand after create).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        try
        {
            var command = new CreateUserCommand
            {
                KeycloakUserId = request.KeycloakUserId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Role = request.Role
            };

            var result = await _cqrsClient.SendCommandAsync<CreateUserCommandResult>(command);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return CreatedAtAction(
                nameof(GetByKey),
                new { keyType = result.UserKey!.Type, keyValue = result.UserKey.Value },
                new { key = result.UserKey, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/users/{keyType}/{keyValue}
    /// Update user fields: firstName, lastName, address, city, phoneNumber.
    /// </summary>
    [HttpPut("{keyType}/{keyValue}")]
    public async Task<IActionResult> Update(string keyType, string keyValue, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var command = new UpdateUserCommand
            {
                UserKey = new Key(keyType, keyValue),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Address = request.Address,
                City = request.City,
                PhoneNumber = request.PhoneNumber
            };

            var result = await _cqrsClient.SendCommandAsync<UpdateUserCommandResult>(command);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/users/{keyType}/{keyValue}/risk-score
    /// User's latest risk score breakdown.
    /// </summary>
    [HttpGet("{keyType}/{keyValue}/risk-score")]
    public async Task<IActionResult> GetRiskScore(string keyType, string keyValue)
    {
        try
        {
            var query = new GetLatestUserRiskScoreQuery
            {
                UserKey = keyValue
            };

            var result = await _cqrsClient.SendQueryAsync<GetLatestUserRiskScoreQueryResult>(query);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            if (result.Score == null)
            {
                return Ok(new { score = (object?)null, message = "No risk score computed yet" });
            }

            return Ok(result.Score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting risk score for {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/users/{keyType}/{keyValue}/alerts
    /// Paged alerts for all devices belonging to this user.
    /// </summary>
    [HttpGet("{keyType}/{keyValue}/alerts")]
    public async Task<IActionResult> GetAlerts(string keyType, string keyValue, [FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetUserAlertsByKeyQuery
            {
                UserKey = new Key(keyType, keyValue),
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection
            };

            var result = await _cqrsClient.SendQueryAsync<GetUserAlertsByKeyQueryResult>(query);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alerts for user {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/users/{keyType}/{keyValue}/devices
    /// Devices belonging to this user.
    /// </summary>
    [HttpGet("{keyType}/{keyValue}/devices")]
    public async Task<IActionResult> GetDevices(string keyType, string keyValue)
    {
        try
        {
            var query = new GetDevicesByUserQuery
            {
                UserKey = new Key(keyType, keyValue)
            };

            var result = await _cqrsClient.SendQueryAsync<GetDevicesByUserQueryResult>(query);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result.Devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting devices for user {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
