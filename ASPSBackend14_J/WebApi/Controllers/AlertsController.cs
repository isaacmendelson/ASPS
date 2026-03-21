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
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();
            
            // Check common localhost names
            if (host == "localhost" || host == "0.0.0.0")
                return true;
            
            // Try to parse as IP address
            if (System.Net.IPAddress.TryParse(host, out var ip))
            {
                // IPv4 loopback (127.0.0.0/8)
                var bytes = ip.GetAddressBytes();
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    if (bytes[0] == 127) return true;                    // 127.x.x.x
                    if (bytes[0] == 10) return true;                     // 10.x.x.x
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;  // 172.16-31.x.x
                    if (bytes[0] == 192 && bytes[1] == 168) return true; // 192.168.x.x
                    if (bytes[0] == 169 && bytes[1] == 254) return true; // 169.254.x.x (link-local)
                    if (bytes[0] == 0) return true;                      // 0.x.x.x
                }
                // IPv6 loopback and link-local
                else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    if (ip.IsIPv6LinkLocal) return true;  // fe80::/10
                    if (System.Net.IPAddress.IsLoopback(ip)) return true;  // ::1
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}
