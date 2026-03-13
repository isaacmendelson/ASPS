using Business.Commands;
using Business.Queries;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using WebApi.DTOs;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly INetMQClientService _netMQClient;
    private readonly ILogger<UsersController> _logger;

    public UsersController(INetMQClientService netMQClient, ILogger<UsersController> logger)
    {
        _netMQClient = netMQClient;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        try
        {
            var query = new GetAllUsersQuery();
            var result = await _netMQClient.SendQueryAsync<GetAllUsersQuery, GetAllUsersQueryResult>(query);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            var users = result.Users.Select(u => new UserResponse
            {
                Key = u.Key,
                KeycloakUserId = u.KeycloakUserId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                DateCreated = u.DateCreated
            });

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get user by key
    /// </summary>
    [HttpGet("{keyType}/{keyValue}")]
    public async Task<ActionResult<UserResponse>> GetByKey(string keyType, string keyValue)
    {
        try
        {
            var query = new GetUserByKeyQuery
            {
                UserKey = new Key(keyType, keyValue)
            };
            var result = await _netMQClient.SendQueryAsync<GetUserByKeyQuery, GetUserByKeyQueryResult>(query);

            if (!result.Success || result.User == null)
            {
                return NotFound(result.Message);
            }

            var response = new UserResponse
            {
                Key = result.User.Key,
                KeycloakUserId = result.User.KeycloakUserId,
                FirstName = result.User.FirstName,
                LastName = result.User.LastName,
                Role = result.User.Role,
                DateCreated = result.User.DateCreated
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get user with full details (devices and accounts)
    /// </summary>
    [HttpGet("{keyType}/{keyValue}/details")]
    public async Task<ActionResult<UserDetailsResponse>> GetDetails(string keyType, string keyValue)
    {
        try
        {
            var query = new GetUserDetailsQuery
            {
                UserKey = new Key(keyType, keyValue)
            };
            var result = await _netMQClient.SendQueryAsync<GetUserDetailsQuery, GetUserDetailsQueryResult>(query);

            if (!result.Success || result.User == null)
            {
                return NotFound(result.Message);
            }

            // Fetch devices and accounts separately via queries
            var devicesQuery = new GetUserDevicesQuery { UserKey = result.User.Key };
            var accountsQuery = new GetUserAccountsQuery { UserKey = result.User.Key };
            
            var devicesResult = await _netMQClient.SendQueryAsync<GetUserDevicesQuery, GetUserDevicesQueryResult>(devicesQuery);
            var accountsResult = await _netMQClient.SendQueryAsync<GetUserAccountsQuery, GetUserAccountsQueryResult>(accountsQuery);
            
            var response = new UserDetailsResponse
            {
                Key = result.User.Key,
                KeycloakUserId = result.User.KeycloakUserId,
                FirstName = result.User.FirstName,
                LastName = result.User.LastName,
                Role = result.User.Role,
                DateCreated = result.User.DateCreated,
                Devices = (devicesResult?.Devices ?? Enumerable.Empty<Common.Entities.UserDevice>()).Select(d => new UserDeviceResponse
                {
                    Key = d.Key,
                    DeviceType = d.DeviceType,
                    DeviceUid = d.DeviceUid,
                    MonitoringStatus = d.MonitoringStatus,
                    DateCreated = d.DateCreated
                }).ToList(),
                Accounts = (accountsResult?.Accounts ?? Enumerable.Empty<Common.Entities.UserAccount>()).Select(a => new UserAccountResponse
                {
                    Key = a.Key,
                    AccountType = a.AccountType,
                    UserName = a.UserName,
                    LoginUrl = a.LoginUrl,
                    DateCreated = a.DateCreated
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user details");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
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

            var result = await _netMQClient.SendCommandAsync<CreateUserCommand, CreateUserCommandResult>(command);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return CreatedAtAction(
                nameof(GetByKey),
                new { keyType = result.UserKey!.Type, keyValue = result.UserKey.Value },
                new { Key = result.UserKey, Message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update a user
    /// </summary>
    [HttpPut("{keyType}/{keyValue}")]
    public async Task<ActionResult> Update(string keyType, string keyValue, [FromBody] UpdateUserRequest request)
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

            var result = await _netMQClient.SendCommandAsync<UpdateUserCommand, UpdateUserCommandResult>(command);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(new { Message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a user (soft delete)
    /// </summary>
    [HttpDelete("{keyType}/{keyValue}")]
    public async Task<ActionResult> Delete(string keyType, string keyValue)
    {
        try
        {
            var command = new DeleteUserCommand
            {
                UserKey = new Key(keyType, keyValue)
            };

            var result = await _netMQClient.SendCommandAsync<DeleteUserCommand, DeleteUserCommandResult>(command);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(new { Message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            return StatusCode(500, "Internal server error");
        }
    }
}
