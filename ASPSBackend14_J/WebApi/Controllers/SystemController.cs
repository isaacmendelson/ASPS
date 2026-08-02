using Business.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SystemController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<SystemController> _logger;

    public SystemController(ICQRSClient cqrsClient, ILogger<SystemController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    /// <summary>
    /// Get system version information
    /// </summary>
    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        return Ok(new
        {
            Version = ThisAssembly.AssemblyInformationalVersion,
            BuildDate = DateTime.UtcNow,
            GitCommitId = typeof(SystemController).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? "unknown",
            IsPrerelease = ThisAssembly.IsPrerelease,
            IsPublicRelease = ThisAssembly.IsPublicRelease
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// POST /api/system/reinitialize-asview
    /// ASPS-649 — Trigger ASView re-initialization.
    /// </summary>
    [HttpPost("reinitialize-asview")]
    public async Task<IActionResult> ReinitializeAsView()
    {
        try
        {
            _logger.LogInformation("ASView re-initialization requested by {User}", User?.Identity?.Name);

            var command = new ReInitializeASViewCommand();
            var result = await _cqrsClient.SendCommandAsync<ReInitializeASViewCommandResult>(command);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-initializing ASView");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
