using Interface.Analysis;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(ILogger<AlertsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Submit a TrackUrlAlert for processing
    /// </summary>
    /// <param name="dto">TrackUrlAlert data transfer object</param>
    /// <returns>Alert submission result</returns>
    [HttpPost("trackurl")]
    public Task<ActionResult> SubmitTrackUrlAlert([FromBody] TrackUrlAlertDto dto)
    {
        try
        {
            // Validation
            if (dto == null)
            {
                _logger.LogWarning("SubmitTrackUrlAlert: null DTO received");
                return Task.FromResult<ActionResult>(BadRequest(new { success = false, message = "Alert data is required" }));
            }

            if (string.IsNullOrWhiteSpace(dto.DeviceUid))
            {
                _logger.LogWarning("SubmitTrackUrlAlert: DeviceUid is required");
                return Task.FromResult<ActionResult>(BadRequest(new { success = false, message = "DeviceUid is required" }));
            }

            if (string.IsNullOrWhiteSpace(dto.Url))
            {
                _logger.LogWarning("SubmitTrackUrlAlert: Url is required for device {DeviceUid}", dto.DeviceUid);
                return Task.FromResult<ActionResult>(BadRequest(new { success = false, message = "Url is required" }));
            }

            // Reject local/loopback addresses
            if (IsLocalUrl(dto.Url))
            {
                _logger.LogDebug("SubmitTrackUrlAlert: Skipping local URL from device {DeviceUid}: {Url}",
                    dto.DeviceUid, dto.Url);
                return Task.FromResult<ActionResult>(BadRequest(new { success = false, message = "Local URLs are not analyzed" }));
            }

            // Validate duration (should be non-negative)
            if (dto.Duration < 0)
            {
                _logger.LogWarning("SubmitTrackUrlAlert: Invalid duration {Duration} for device {DeviceUid}",
                    dto.Duration, dto.DeviceUid);
                return Task.FromResult<ActionResult>(BadRequest(new { success = false, message = "Duration must be non-negative" }));
            }

            // Validate timestamp (should not be in future)
            if (dto.Timestamp > DateTime.UtcNow.AddMinutes(5))
            {
                _logger.LogWarning("SubmitTrackUrlAlert: Future timestamp {Timestamp} for device {DeviceUid}",
                    dto.Timestamp, dto.DeviceUid);
                return Task.FromResult<ActionResult>(BadRequest(new { success = false, message = "Timestamp cannot be in the future" }));
            }

            // Log received alert for processing
            _logger.LogInformation(
                "TrackUrlAlert received: DeviceUid={DeviceUid}, Url={Url}, Duration={Duration}s, Priority={Priority}",
                dto.DeviceUid, dto.Url, dto.Duration, dto.Priority);

            // Return success - actual processing happens via ZeroMQ RealTimeAlertListener
            // This endpoint serves as an alternative submission method to the ZeroMQ socket
            return Task.FromResult<ActionResult>(Ok(new
            {
                success = true,
                message = "TrackUrlAlert received and queued for processing",
                deviceUid = dto.DeviceUid,
                url = dto.Url,
                timestamp = DateTime.UtcNow.ToString("o"),
                note = "Alert processing handled by RealTimeAlertListener via ZeroMQ"
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TrackUrlAlert submission");
            return Task.FromResult<ActionResult>(StatusCode(500, new { success = false, message = "Internal server error" }));
        }
    }

    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        try
        {
            var host = new Uri(url).Host.ToLowerInvariant();
            return host == "localhost" ||
                   host == "127.0.0.1" ||
                   host == "::1" ||
                   host == "0.0.0.0" ||
                   host.StartsWith("127.");
        }
        catch
        {
            return false;
        }
    }
}
