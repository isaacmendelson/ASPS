using Business.Commands;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using WebApi.DTOs;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserDevicesController : ControllerBase
{
    private readonly NetMQClientService _netMQClient;
    private readonly ILogger<UserDevicesController> _logger;

    public UserDevicesController(NetMQClientService netMQClient, ILogger<UserDevicesController> logger)
    {
        _netMQClient = netMQClient;
        _logger = logger;
    }

    /// <summary>
    /// Create a new user device
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateUserDeviceRequest request)
    {
        try
        {
            var command = new CreateUserDeviceCommand
            {
                UserKey = new Key(request.UserKeyType, request.UserKeyValue),
                DeviceType = request.DeviceType,
                DeviceUid = request.DeviceUid,
                PhoneNumber = request.PhoneNumber,
                OperatingSystem = request.OperatingSystem
            };

            var result = await _netMQClient.SendCommandAsync<CreateUserDeviceCommand, CreateUserDeviceCommandResult>(command);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(new { DeviceKey = result.DeviceKey, Message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating device");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update a user device
    /// </summary>
    [HttpPut("{keyType}/{keyValue}")]
    public async Task<ActionResult> Update(string keyType, string keyValue, [FromBody] UpdateUserDeviceRequest request)
    {
        try
        {
            var command = new UpdateUserDeviceCommand
            {
                DeviceKey = new Key(keyType, keyValue),
                MonitoringStatus = request.MonitoringStatus
            };

            var result = await _netMQClient.SendCommandAsync<UpdateUserDeviceCommand, UpdateUserDeviceCommandResult>(command);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(new { Message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a user device (soft delete)
    /// </summary>
    [HttpDelete("{keyType}/{keyValue}")]
    public async Task<ActionResult> Delete(string keyType, string keyValue)
    {
        try
        {
            var command = new DeleteUserDeviceCommand
            {
                DeviceKey = new Key(keyType, keyValue)
            };

            var result = await _netMQClient.SendCommandAsync<DeleteUserDeviceCommand, DeleteUserDeviceCommandResult>(command);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(new { Message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device");
            return StatusCode(500, "Internal server error");
        }
    }
}
