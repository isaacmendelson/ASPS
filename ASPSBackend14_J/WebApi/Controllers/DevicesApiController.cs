using Business.Queries;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// REST API for devices — server-side paged list, detail by key and UID, device alerts.
/// ASPS-647: Angular Admin Client backend.
/// </summary>
[ApiController]
[Route("api/devices")]
[Authorize(Roles = "Admin")]
public class DevicesApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<DevicesApiController> _logger;

    public DevicesApiController(ICQRSClient cqrsClient, ILogger<DevicesApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// Server-side paged list of devices with optional search, sort and status filter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, [FromQuery] string? status)
    {
        request.Normalize();
        try
        {
            var query = new GetAllDevicesPagedQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection,
                Status = status
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllDevicesPagedQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetAllDevices failed: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged devices");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Single device detail by key (keyType/keyValue).
    /// </summary>
    [HttpGet("{keyType}/{keyValue}")]
    public async Task<IActionResult> GetByKey(string keyType, string keyValue)
    {
        try
        {
            var query = new GetDeviceByKeyQuery
            {
                DeviceKey = new Key(keyType, keyValue)
            };

            var result = await _cqrsClient.SendQueryAsync<GetDeviceByKeyQueryResult>(query);

            if (!result.Success || result.Device == null)
            {
                return NotFound(new { message = result.Message ?? "Device not found" });
            }

            return Ok(result.Device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device by key {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Lookup device by UID string.
    /// </summary>
    [HttpGet("uid/{uid}")]
    public async Task<IActionResult> GetByUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return BadRequest(new { message = "UID is required" });

        try
        {
            var query = new GetDeviceByUidQuery { DeviceUid = uid };
            var result = await _cqrsClient.SendQueryAsync<GetDeviceByUidQueryResult>(query);

            if (!result.Success || result.Device == null)
            {
                return NotFound(new { message = result.Message ?? "Device not found" });
            }

            return Ok(result.Device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device by UID {Uid}", uid);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Paged alerts for a specific device (by device keyField value).
    /// </summary>
    [HttpGet("{keyType}/{keyValue}/alerts")]
    public async Task<IActionResult> GetAlerts(string keyType, string keyValue, [FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetDeviceAlertsPagedQuery
            {
                DeviceKeyField = keyValue,
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection
            };

            var result = await _cqrsClient.SendQueryAsync<GetDeviceAlertsPagedQueryResult>(query);

            if (!result.Success)
            {
                _logger.LogWarning("GetDeviceAlerts failed for {KeyType}/{KeyValue}: {Message}", keyType, keyValue, result.Message);
                return BadRequest(new { message = result.Message });
            }

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged alerts for device {KeyType}/{KeyValue}", keyType, keyValue);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
